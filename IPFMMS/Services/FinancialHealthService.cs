using IPFMMS.Data;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class FinancialHealthService(ApplicationDbContext db)
{
    // Refresh completed months on demand; the current month remains a preview.
    public async Task RefreshHistoryAsync(string owner, DateTime today, IdGeneratorService ids, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("An owner is required.");
        var current = new DateTime(today.Year, today.Month, 1);
        await new BudgetRolloverService(db, new IdGeneratorService(db)).EnsureMonthAsync(owner, current.AddMonths(-1), ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var users = await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (users.Count == 0) throw new InvalidOperationException("User no longer exists.");
        var records = await db.FinancialHealthRecords.Where(r => r.userId == owner).ToListAsync(ct);
        var months = await db.Budgets.AsNoTracking()
            .Where(b => b.UserId == owner && b.BudgetMonth >= new DateTime(1000, 4, 1) && b.BudgetMonth < current)
            .Select(b => b.BudgetMonth).Distinct().ToListAsync(ct);
        // Include saved months so deleting their source data cannot leave a misleading score.
        months = months.Union(records.Select(r => r.recordMonth)).OrderBy(m => m).ToList();
        foreach (var month in months)
        {
            var record = records.SingleOrDefault(r => r.recordMonth == month);
            if (month < new DateTime(1000, 4, 1) || month >= current)
            {
                if (record != null) db.FinancialHealthRecords.Remove(record);
                continue;
            }
            var model = await BuildAsync(owner, month, today, ct: ct, includeHistory: false);
            if (!model.CanSave)
            {
                if (record != null) db.FinancialHealthRecords.Remove(record);
                continue;
            }
            if (record != null && record.savingsRatioScore == model.SavingsScore &&
                record.budgetAdherenceScore == model.BudgetScore && record.spendingStabilityScore == model.StabilityScore &&
                record.healthScore == model.Overall && record.rating == model.Rating &&
                record.formulaVersion == FinancialHealthCalculator.Version) continue;
            if (record == null)
            {
                record = new() { healthRecordId = await ids.GenerateIdAsync("FHR"), userId = owner, recordMonth = month };
                db.FinancialHealthRecords.Add(record);
            }
            record.savingsRatioScore = model.SavingsScore!.Value;
            record.budgetAdherenceScore = model.BudgetScore!.Value;
            record.spendingStabilityScore = model.StabilityScore!.Value;
            record.healthScore = model.Overall!.Value;
            record.rating = model.Rating;
            record.calculatedDate = DateTime.UtcNow;
            record.formulaVersion = FinancialHealthCalculator.Version;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<FinancialHealthViewModel> BuildAsync(string owner,DateTime month,DateTime today,int page=1,CancellationToken ct=default,bool includeHistory=true)
    {
        var current=new DateTime(today.Year,today.Month,1);
        if(string.IsNullOrWhiteSpace(owner)||month<new DateTime(1000,4,1)||month>current||month.Day!=1||month.TimeOfDay!=TimeSpan.Zero)
            throw new ArgumentException("Choose a valid past or current month.");
        await new BudgetRolloverService(db,new IdGeneratorService(db)).EnsureMonthAsync(owner,month,ct);
        bool provisional=month==current;
        var until=provisional?today.Date.AddDays(1):month.AddMonths(1);
        var stabilityEnd=provisional?month:month.AddMonths(1);
        var stabilityStart=stabilityEnd.AddMonths(-3);
        var income=await db.Incomes.AsNoTracking().Where(i=>i.UserId==owner&&i.IncomeDate>=month&&i.IncomeDate<until)
            .GroupBy(i=>1).Select(g=>new{Amount=g.Sum(i=>i.Amount),Count=g.Count()}).SingleOrDefaultAsync(ct);
        var expenses=await db.Expenses.AsNoTracking().Where(e=>e.UserId==owner&&e.ExpenseDate>=stabilityStart&&e.ExpenseDate<until)
            .GroupBy(e=>new{e.ExpenseDate.Year,e.ExpenseDate.Month,e.ExpenseCategoryId})
            .Select(g=>new{g.Key.Year,g.Key.Month,g.Key.ExpenseCategoryId,Amount=g.Sum(e=>e.Amount),Count=g.Count()}).ToListAsync(ct);
        var selected=expenses.Where(e=>e.Year==month.Year&&e.Month==month.Month).ToList();
        var savings=await db.SavingsContributions.AsNoTracking().Where(c=>c.SavingsGoal!.UserId==owner&&c.ContributionDate>=month&&c.ContributionDate<until)
            .SumAsync(c=>(decimal?)c.Amount,ct)??0m;
        savings += await SharedSavingsAllocations.NetAsync(db,owner,month,until,ct);
        var budgets=await db.Budgets.AsNoTracking().Where(b=>b.UserId==owner&&b.BudgetMonth==month)
            .Select(b=>new{b.ExpenseCategoryId,b.BudgetType,b.BudgetAmount,b.BudgetPercentage}).ToListAsync(ct);
        var totalIncome=income?.Amount??0m;
        var model=new FinancialHealthViewModel{Month=month,AsOfDate=until.AddDays(-1),IsProvisional=provisional,Income=totalIncome,IncomeCount=income?.Count??0,
            Expenses=selected.Sum(e=>e.Amount),Savings=savings,BudgetCount=budgets.Count};
        foreach(var b in budgets){
            decimal limit=b.BudgetType=="Percentage"?decimal.Round(totalIncome*(b.BudgetPercentage??0)/100m,2,MidpointRounding.AwayFromZero):b.BudgetAmount??0m;
            if(selected.Where(e=>e.ExpenseCategoryId==b.ExpenseCategoryId).Sum(e=>e.Amount)<=limit)model.BudgetsWithinLimit++;
        }
        model.UnbudgetedCategories=selected.Count(e=>!budgets.Any(b=>b.ExpenseCategoryId==e.ExpenseCategoryId));
        for(var m=stabilityStart;m<stabilityEnd;m=m.AddMonths(1)){
            var entries=expenses.Where(e=>e.Year==m.Year&&e.Month==m.Month).ToList();
            model.StabilityMonths.Add(new(){Month=m,Amount=entries.Sum(e=>e.Amount),Count=entries.Sum(e=>e.Count)});
        }
        FinancialHealthCalculator.Calculate(model);
        if(!includeHistory)return model;
        model.SavedRecord=await db.FinancialHealthRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.userId==owner&&r.recordMonth==month,ct);
        model.HistoryCount=await db.FinancialHealthRecords.CountAsync(r=>r.userId==owner,ct);
        model.HistoryPage=Math.Clamp(page,1,model.HistoryPages);
        model.History=await db.FinancialHealthRecords.AsNoTracking().Where(r=>r.userId==owner).OrderByDescending(r=>r.recordMonth)
            .Skip((model.HistoryPage-1)*12).Take(12).ToListAsync(ct);
        return model;
    }
}
