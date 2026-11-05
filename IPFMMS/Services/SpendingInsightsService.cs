using IPFMMS.Data;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public sealed class SpendingInsightsService(ApplicationDbContext db)
{
    public async Task<SpendingInsightsViewModel> BuildAsync(string owner,DateTime month,DateTime today,CancellationToken ct=default)
    {
        var current=new DateTime(today.Year,today.Month,1);
        if(string.IsNullOrWhiteSpace(owner))throw new ArgumentException("An owner is required.");
        if(month<new DateTime(1000,2,1)||month>current||month.Day!=1||month.TimeOfDay!=TimeSpan.Zero)throw new ArgumentException("Choose a past or current valid month.");
        bool isCurrent=month==current;
        var end=isCurrent?today.Date.AddDays(1):month.AddMonths(1);
        var prior=month.AddMonths(-1);
        int matchedDays=isCurrent?Math.Min(today.Day,DateTime.DaysInMonth(prior.Year,prior.Month)):DateTime.DaysInMonth(prior.Year,prior.Month);
        var priorEnd=prior.AddDays(matchedDays);
        var comparedEnd=isCurrent?month.AddDays(matchedDays):end;
        var expenses=db.Expenses.AsNoTracking().Where(e=>e.UserId==owner&&e.ExpenseDate>=prior&&e.ExpenseDate<end&&
            (e.ExpenseDate>=month||e.ExpenseDate<priorEnd));
        var daily=await expenses.Join(db.ExpenseCategories.AsNoTracking(),e=>e.ExpenseCategoryId,c=>c.ExpenseCategoryId,
            (e,c)=>new{e.ExpenseDate,e.Amount,e.ExpenseCategoryId,c.CategoryName})
            .GroupBy(e=>new{Date=e.ExpenseDate.Date,e.ExpenseCategoryId,e.CategoryName})
            .Select(g=>new{g.Key.Date,g.Key.ExpenseCategoryId,g.Key.CategoryName,Amount=g.Sum(e=>e.Amount),Count=g.Count()}).ToListAsync(ct);
        var selected=daily.Where(e=>e.Date>=month).ToList();
        var previous=daily.Where(e=>e.Date<month).ToList();
        var compared=selected.Where(e=>e.Date<comparedEnd).ToList();
        var income=await db.Incomes.AsNoTracking().Where(i=>i.UserId==owner&&i.IncomeDate>=month&&i.IncomeDate<end)
            .GroupBy(i=>1).Select(g=>new{Amount=g.Sum(i=>i.Amount),Count=g.Count()}).SingleOrDefaultAsync(ct);
        var budgets=await db.Budgets.AsNoTracking().Where(b=>b.UserId==owner&&b.BudgetMonth==month)
            .OrderBy(b=>b.ExpenseCategory!.DisplayOrder).ThenBy(b=>b.ExpenseCategory!.CategoryName)
            .Select(b=>new{b.BudgetId,b.ExpenseCategoryId,b.ExpenseCategory!.CategoryName,b.BudgetType,b.BudgetAmount,b.BudgetPercentage}).ToListAsync(ct);
        var model=new SpendingInsightsViewModel{
            Month=month,PeriodEnd=end.AddDays(-1),IsCurrentMonth=isCurrent,ComparisonStart=prior,ComparisonEnd=priorEnd.AddDays(-1),CurrentComparisonEnd=comparedEnd.AddDays(-1),
            TotalExpenses=selected.Sum(e=>e.Amount),TransactionCount=selected.Sum(e=>e.Count),ActiveDays=selected.Select(e=>e.Date).Distinct().Count(),
            Income=income?.Amount??0m,IncomeCount=income?.Count??0,ComparableCurrentExpenses=compared.Sum(e=>e.Amount),ComparableCurrentCount=compared.Sum(e=>e.Count),
            PreviousExpenses=previous.Sum(e=>e.Amount),PreviousCount=previous.Sum(e=>e.Count)};
        var categoryBudgets=budgets.Select(b=>b.ExpenseCategoryId).ToHashSet();
        model.Categories=selected.GroupBy(e=>new{e.ExpenseCategoryId,e.CategoryName}).Select(g=>new SpendingCategoryInsight{
            Id=g.Key.ExpenseCategoryId,Name=g.Key.CategoryName,Amount=g.Sum(e=>e.Amount),Count=g.Sum(e=>e.Count),ActiveDays=g.Count(),
            PreviousAmount=previous.Where(e=>e.ExpenseCategoryId==g.Key.ExpenseCategoryId).Sum(e=>e.Amount),HasBudget=categoryBudgets.Contains(g.Key.ExpenseCategoryId),
            Share=model.TotalExpenses>0?g.Sum(e=>e.Amount)/model.TotalExpenses*100m:0m}).OrderByDescending(c=>c.Amount).ThenBy(c=>c.Name).ToList();
        model.Budgets=budgets.Select(b=>new BudgetSummaryViewModel{BudgetId=b.BudgetId,CategoryName=b.CategoryName,BudgetType=b.BudgetType,BudgetAmount=b.BudgetAmount,
            BudgetPercentage=b.BudgetPercentage,MonthlyIncome=model.Income,AmountSpent=selected.Where(e=>e.ExpenseCategoryId==b.ExpenseCategoryId).Sum(e=>e.Amount)}).ToList();
        foreach(var group in previous.Where(e=>!model.Categories.Any(c=>c.Id==e.ExpenseCategoryId)).GroupBy(e=>new{e.ExpenseCategoryId,e.CategoryName}))
            model.Categories.Add(new(){Id=group.Key.ExpenseCategoryId,Name=group.Key.CategoryName,PreviousAmount=group.Sum(e=>e.Amount),HasBudget=categoryBudgets.Contains(group.Key.ExpenseCategoryId)});
        foreach(var category in model.Categories)category.ComparableAmount=compared.Where(e=>e.ExpenseCategoryId==category.Id).Sum(e=>e.Amount);
        model.Categories=model.Categories.OrderByDescending(c=>c.Amount).ThenBy(c=>c.Name).ToList();
        for(int day=0;day<7;day++){
            var weekday=(DayOfWeek)((day+1)%7);var rows=selected.Where(e=>e.Date.DayOfWeek==weekday).ToList();
            model.Weekdays.Add(new(){Day=weekday.ToString(),Count=rows.Sum(e=>e.Count),Amount=rows.Sum(e=>e.Amount)});
        }
        model.Recommendations=SpendingInsightsRules.Build(model);
        return model;
    }
}
