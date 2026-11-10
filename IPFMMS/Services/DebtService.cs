using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class DebtService(ApplicationDbContext db,IdGeneratorService ids)
{
    private async Task LockOwner(string owner,CancellationToken ct)
    {
        var users=await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if(!users.Any(u=>u.AccountStatus==AccountStatus.Active&&(u.Role=="User"||u.Role=="Parent")))throw new InvalidOperationException("An active user account is required.");
    }
    private static bool Money(decimal? n,bool zero=false)=>n.HasValue&&n>=(zero?0:.01m)&&n<=999999999.99m&&n==decimal.Round(n.Value,2);
    private static void Validate(DebtFormViewModel m)
    {
        if(string.IsNullOrWhiteSpace(m.DebtName)||m.DebtName.Trim().Length>100)throw new InvalidOperationException("Enter a debt name of 1–100 characters.");
        if(!DebtOptions.Types.Contains(m.DebtType))throw new InvalidOperationException("Choose a valid debt type.");
        if(!Money(m.OriginalAmount)||!Money(m.OutstandingAmount,true)||!Money(m.MonthlyRepaymentAmount))throw new InvalidOperationException("Enter valid amounts with at most two decimal places. Original and monthly repayment amounts must be positive.");
        if(m.OutstandingAmount>m.OriginalAmount)throw new InvalidOperationException("Outstanding amount cannot exceed the original amount.");
        if(!m.InterestRate.HasValue||m.InterestRate<0||m.InterestRate>100||m.InterestRate!=decimal.Round(m.InterestRate.Value,4))throw new InvalidOperationException("Enter an annual interest rate from 0 to 100 with at most four decimal places.");
        if(!m.DueDate.HasValue||m.DueDate.Value.Year<1000)throw new InvalidOperationException("Choose a valid due date.");
        if(m.Description?.Length>500)throw new InvalidOperationException("Description must not exceed 500 characters.");
    }
    public async Task<string?> SaveAsync(string owner,DebtFormViewModel m,bool edit,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);await LockOwner(owner,ct);
        if(m.CalculationMode!="Manual"&&m.CalculationMode!="Calculated")throw new InvalidOperationException("Choose a valid calculation mode.");
        Debt d;
        if(edit)
        {
            var existing=await db.Debts.SingleOrDefaultAsync(d=>d.debtId==m.DebtId&&d.userId==owner&&d.status!="Deleted",ct);
            if(existing==null)return null;d=existing;
            if(d.revision!=m.Revision)throw new InvalidOperationException("This debt changed after you opened the form. Return to its details and try again.");
            var hasPayments=await db.DebtRepayments.AnyAsync(p=>p.debtId==d.debtId,ct);
            if(hasPayments&&m.CalculationMode!=d.calculationMode)throw new InvalidOperationException("Calculation mode cannot change after repayments exist.");
            if(hasPayments&&d.calculationMode=="Calculated")
            {
                if(m.OriginalAmount!=d.originalAmount||m.RateType!=d.rateType||m.LoanTermMonths!=d.loanTermMonths||m.LoanStartDate!=d.loanStartDate||m.IsExistingLoan!=d.isExistingLoan||m.ReferenceRate!=d.referenceRate||m.RateSpread!=d.rateSpread||m.ReferenceRateName!=d.referenceRateName||m.DueDate!=d.dueDate||m.InterestRate!=d.interestRate)
                    throw new InvalidOperationException("Loan terms are locked after repayments. Use Update floating rate for a future rate change.");
                m.MonthlyRepaymentAmount=d.monthlyRepaymentAmount;
            }
            else {PrepareCalculation(m);d.loanDueDay=m.DueDate?.Day??1;}
            Validate(m);
            if(hasPayments&&m.OutstandingAmount!=d.outstandingAmount)throw new InvalidOperationException("Outstanding balance is managed by repayment history and cannot be edited after repayments are recorded.");
            if(hasPayments&&m.OriginalAmount<d.openingOutstandingAmount)throw new InvalidOperationException("Original amount cannot be lower than the opening tracked balance.");
            if(!hasPayments)d.openingOutstandingAmount=m.OutstandingAmount!.Value;
            if(m.Status!="Active"&&m.Status!="Inactive"&&!(m.Status=="Paid"&&d.status=="Paid"))throw new InvalidOperationException("Choose a valid debt status.");
            d.revision++;
        }
        else {PrepareCalculation(m);Validate(m);d=new Debt{loanDueDay=m.DueDate?.Day??1,debtId=await ids.GenerateIdAsync("DBT"),userId=owner,openingOutstandingAmount=m.OutstandingAmount!.Value};db.Debts.Add(d);}
        d.calculationMode=m.CalculationMode;
        if(m.CalculationMode=="Calculated")
        {
            d.rateType=m.RateType;d.loanTermMonths=m.LoanTermMonths;d.loanStartDate=m.LoanStartDate!.Value.Date;d.isExistingLoan=m.IsExistingLoan;
            d.referenceRate=m.RateType=="Floating"?m.ReferenceRate:null;d.rateSpread=m.RateType=="Floating"?m.RateSpread:null;d.referenceRateName=m.RateType=="Floating"?m.ReferenceRateName:"SBR";
        }
        d.debtName=m.DebtName.Trim();d.debtType=m.DebtType;d.originalAmount=m.OriginalAmount!.Value;d.outstandingAmount=m.OutstandingAmount!.Value;d.monthlyRepaymentAmount=m.MonthlyRepaymentAmount!.Value;d.interestRate=m.InterestRate!.Value;d.dueDate=m.DueDate!.Value.Date;d.description=string.IsNullOrWhiteSpace(m.Description)?null:m.Description.Trim();
        d.status=d.outstandingAmount==0?"Paid":edit&&m.Status=="Inactive"?"Inactive":"Active";
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return d.debtId;
    }
    public async Task<bool> DeleteAsync(string owner,string id,int revision,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);await LockOwner(owner,ct);
        var d=await db.Debts.SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==owner&&d.status!="Deleted",ct);if(d==null)return false;
        if(d.revision!=revision)throw new InvalidOperationException("This debt changed after you opened the confirmation. Review it again before deleting.");
        d.status="Deleted";d.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    public async Task<bool> RepayAsync(string owner,DebtRepayViewModel m,CancellationToken ct=default)
    {
        if(!Money(m.Amount))throw new InvalidOperationException("Repayment must be greater than RM 0 with at most two decimal places.");
        if(!m.PaymentDate.HasValue||m.PaymentDate.Value.Year<1000||m.PaymentDate.Value.Date>DebtOptions.Today)throw new InvalidOperationException("Choose today or a past payment date.");
        if(!RoutineBillOptions.PaymentMethods.Contains(m.PaymentMethod))throw new InvalidOperationException("Choose a valid payment method.");
        if(m.Remark?.Length>500)throw new InvalidOperationException("Remarks must not exceed 500 characters.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);await LockOwner(owner,ct);
        var d=await db.Debts.SingleOrDefaultAsync(d=>d.debtId==m.DebtId&&d.userId==owner&&d.status!="Deleted",ct);if(d==null)return false;
        if(d.revision!=m.Revision)throw new InvalidOperationException("This debt changed or this repayment was already recorded. Return to its details before continuing.");
        if(d.status!="Active"||d.outstandingAmount<=0)throw new InvalidOperationException("Only active debts with an outstanding balance can be repaid.");
        LoanPeriod? period=null;decimal principal=m.Amount!.Value,interest=0;
        if(d.calculationMode=="Calculated")
        {
            var latestDate=await db.DebtRepayments.Where(p=>p.debtId==d.debtId).MaxAsync(p=>(DateTime?)p.repaymentDate,ct);
            if(latestDate.HasValue&&m.PaymentDate!.Value.Date<latestDate.Value.Date)throw new InvalidOperationException("Calculated loan payments must be recorded in chronological order; this date precedes an existing repayment.");
            period=await CurrentPeriodAsync(d,ct);
            if(m.Amount>period.MaximumPayment)throw new InvalidOperationException("Repayment cannot exceed remaining principal plus this period's unpaid interest.");
            if(m.PaymentDate.Value.Date<d.loanStartDate)throw new InvalidOperationException("Payment date cannot be before the loan tracking start date.");
            interest=Math.Min(m.Amount.Value,period.InterestRemaining);principal=m.Amount.Value-interest;
        }
        else if(m.Amount>d.outstandingAmount)throw new InvalidOperationException("Repayment cannot exceed the outstanding balance.");
        if(d.calculationMode=="Manual"&&m.Amount<d.outstandingAmount&&m.NextDueDate.HasValue&&(m.NextDueDate.Value.Year<1000||m.NextDueDate.Value.Date<=d.dueDate))throw new InvalidOperationException("The next due date must be later than the current due date, or leave it blank to keep the current date.");
        var categories=await db.ExpenseCategories.FromSqlInterpolated($"SELECT * FROM `ExpenseCategories` WHERE `CategoryName` = {"Debt Repayment"} LOCK IN SHARE MODE").AsNoTracking().ToListAsync(ct);
        if(categories.Count!=1||!categories[0].IsActive)throw new InvalidOperationException("The Debt Repayment expense category must exist and be active. Contact the administrator.");
        var expense=await ids.GenerateIdAsync("EXP");
        db.Expenses.Add(new Expense{ExpenseId=expense,UserId=owner,ExpenseCategoryId=categories[0].ExpenseCategoryId,Amount=m.Amount!.Value,ExpenseDate=m.PaymentDate!.Value.Date,PaymentMethod=m.PaymentMethod,Description=$"Debt repayment: {d.debtName}"});
        db.DebtRepayments.Add(new DebtRepayment{repaymentId=await ids.GenerateIdAsync("DRP"),debtId=d.debtId,expenseId=expense,repaymentAmount=m.Amount!.Value,repaymentDate=m.PaymentDate.Value.Date,paymentMethod=m.PaymentMethod,remark=string.IsNullOrWhiteSpace(m.Remark)?null:m.Remark.Trim(),principalAmount=principal,interestAmount=interest,
            appliedInterestRate=period?.AnnualRate??0,scheduledDueDate=period==null?null:d.dueDate,periodInterest=period?.InterestTotal??0,scheduledPayment=period?.TargetPayment??0,allocationMethod=d.calculationMode});
        d.outstandingAmount-=principal;d.revision++;
        if(period!=null&&d.outstandingAmount>0&&period.CashPaid+m.Amount!.Value>=period.TargetPayment)
        {
            if(d.dueDate.Year==9999&&d.dueDate.Month==12)throw new InvalidOperationException("Next loan due date is outside the supported date range.");
            var next=new DateTime(d.dueDate.Year,d.dueDate.Month,1).AddMonths(1);d.dueDate=new DateTime(next.Year,next.Month,Math.Min(d.loanDueDay,DateTime.DaysInMonth(next.Year,next.Month)));d.completedInstallments++;
        }
        if(d.outstandingAmount==0){d.status="Paid";if(period!=null)d.completedInstallments++;}else if(d.calculationMode=="Manual"&&m.NextDueDate.HasValue)d.dueDate=m.NextDueDate.Value.Date;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }

    private static void PrepareCalculation(DebtFormViewModel m)
    {
        if(m.CalculationMode!="Calculated")return;
        if(m.RateType!="Fixed"&&m.RateType!="Floating")throw new InvalidOperationException("Choose fixed or variable (floating) rate.");
        if(!m.LoanTermMonths.HasValue||!m.LoanStartDate.HasValue||m.LoanStartDate.Value.Year<1000)throw new InvalidOperationException("Enter a valid term and tracking start date.");
        if(!m.DueDate.HasValue||m.DueDate.Value.Date<=m.LoanStartDate.Value.Date)throw new InvalidOperationException("First payment due date must be after the loan/tracking start date.");
        if(!Money(m.OriginalAmount))throw new InvalidOperationException("Enter a valid original principal amount.");
        if(!m.IsExistingLoan)m.OutstandingAmount=m.OriginalAmount;
        if(!Money(m.OutstandingAmount)||m.OutstandingAmount>m.OriginalAmount)throw new InvalidOperationException("Enter the lender's remaining principal, no higher than the original principal.");
        if(m.RateType=="Floating")
        {
            if(!new[]{"SBR","BR","BLR","Other"}.Contains(m.ReferenceRateName)||!m.ReferenceRate.HasValue||!m.RateSpread.HasValue)throw new InvalidOperationException("Enter the reference rate and spread from your lender.");
            m.InterestRate=LoanCalculator.Rate(m.ReferenceRate.Value,m.RateSpread.Value);
        }
        if(!m.InterestRate.HasValue)throw new InvalidOperationException("Enter the annual lending rate.");
        var quote=LoanCalculator.Quote(m.OutstandingAmount!.Value,m.InterestRate.Value,m.LoanTermMonths.Value);
        m.MonthlyRepaymentAmount=quote.MonthlyPayment;
    }
    public async Task<LoanPeriod> CurrentPeriodAsync(Debt d,CancellationToken ct=default)
    {
        var rows=await db.DebtRepayments.AsNoTracking().Where(p=>p.debtId==d.debtId&&p.allocationMethod=="Calculated"&&p.scheduledDueDate==d.dueDate).OrderBy(p=>p.createdDate).ThenBy(p=>p.repaymentId).ToListAsync(ct);
        var total=rows.Count==0?LoanCalculator.Interest(d.outstandingAmount,d.interestRate):rows[0].periodInterest;
        var remaining=Math.Max(0,total-rows.Sum(p=>p.interestAmount));
        var maximum=d.outstandingAmount+remaining;
        var target=rows.Count>0?rows[0].scheduledPayment:d.completedInstallments>=d.loanTermMonths-1?maximum:Math.Min(d.monthlyRepaymentAmount,maximum);
        return new LoanPeriod(total,remaining,maximum,target,rows.Sum(p=>p.repaymentAmount),rows.Count==0?d.interestRate:rows[0].appliedInterestRate);
    }
    public async Task<List<LoanScheduleRow>> ProjectAsync(Debt d,CancellationToken ct=default)
    {
        var rows=new List<LoanScheduleRow>();if(d.outstandingAmount<=0||d.calculationMode!="Calculated")return rows;
        var period=await CurrentPeriodAsync(d,ct);var balance=d.outstandingAmount;
        var remaining=Math.Max(1,d.loanTermMonths!.Value-d.completedInstallments);
        for(int i=0;i<remaining&&balance>0;i++)
        {
            var interest=i==0?period.InterestRemaining:LoanCalculator.Interest(balance,d.interestRate);
            var payment=i==remaining-1?balance+interest:Math.Min(balance+interest,i==0?Math.Max(0,period.TargetPayment-period.CashPaid):d.monthlyRepaymentAmount);
            var principal=payment-interest;if(principal<0)throw new InvalidOperationException("The scheduled payment does not cover interest.");
            rows.Add(new LoanScheduleRow(d.completedInstallments+i+1,balance,payment,principal,interest,balance-principal));balance-=principal;
        }
        return rows;
    }
    public async Task<bool> ChangeRateAsync(string owner,DebtRateViewModel m,CancellationToken ct=default)
    {
        var rate=LoanCalculator.Rate(m.ReferenceRate,m.Spread);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);await LockOwner(owner,ct);
        var d=await db.Debts.SingleOrDefaultAsync(d=>d.debtId==m.DebtId&&d.userId==owner&&d.status!="Deleted",ct);if(d==null)return false;
        if(d.revision!=m.Revision)throw new InvalidOperationException("This loan changed. Return to its details before continuing.");
        if(d.calculationMode!="Calculated"||d.rateType!="Floating"||d.status!="Active")throw new InvalidOperationException("Only an active calculated floating-rate loan can change rate.");
        if(d.dueDate<DebtOptions.Today||await db.DebtRepayments.AnyAsync(p=>p.debtId==d.debtId&&p.scheduledDueDate==d.dueDate,ct))throw new InvalidOperationException("Finish the overdue or partially paid instalment at its existing rate before updating the next instalment.");
        if(rate==d.interestRate&&m.ReferenceRate==d.referenceRate&&m.Spread==d.rateSpread)throw new InvalidOperationException("Enter a changed reference rate or spread.");
        var quote=LoanCalculator.Quote(d.outstandingAmount,rate,Math.Max(1,d.loanTermMonths!.Value-d.completedInstallments));
        db.DebtRateChanges.Add(new DebtRateChange{rateChangeId=await ids.GenerateIdAsync("DRC"),debtId=d.debtId,effectiveDueDate=d.dueDate,previousRate=d.interestRate,newRate=rate,referenceRate=m.ReferenceRate,spread=m.Spread,monthlyPayment=quote.MonthlyPayment});
        d.interestRate=rate;d.referenceRate=m.ReferenceRate;d.rateSpread=m.Spread;d.monthlyRepaymentAmount=quote.MonthlyPayment;d.revision++;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
public sealed record LoanPeriod(decimal InterestTotal,decimal InterestRemaining,decimal MaximumPayment,decimal TargetPayment,decimal CashPaid,decimal AnnualRate);
