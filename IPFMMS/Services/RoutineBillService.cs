using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class RoutineBillService(ApplicationDbContext db, IdGeneratorService ids)
{
    public async Task<string> CreateAsync(string owner, CreateRoutineBillViewModel model, CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(owner))throw new InvalidOperationException("An active user account is required.");
        var name=Validate(model);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var users=await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if(!users.Any(u=>u.AccountStatus==AccountStatus.Active&&(u.Role=="User"||u.Role=="Parent")))throw new InvalidOperationException("An active user account is required.");
        var categories=await db.ExpenseCategories.FromSqlInterpolated($"SELECT * FROM `ExpenseCategories` WHERE `ExpenseCategoryId` = {model.ExpenseCategoryId} LOCK IN SHARE MODE").AsNoTracking().ToListAsync(ct);
        if(!categories.Any(c=>c.IsActive))throw new InvalidOperationException("Choose an active expense category.");
        var bill=new RoutineBill
        {
            billId=await ids.GenerateIdAsync("RBL"),userId=owner,expenseCategoryId=model.ExpenseCategoryId,billName=name,
            billType=model.BillType,amount=model.Amount!.Value,dueDate=model.DueDate!.Value.Date,
            frequency=model.Frequency,paymentMethod=model.PaymentMethod,reminderPeriod=model.ReminderPeriod,
            description=string.IsNullOrWhiteSpace(model.Description)?null:model.Description.Trim(),status="Active",
            createdDate=DateTime.UtcNow,recurrenceAnchorDay=model.DueDate!.Value.Day
        };
        db.RoutineBills.Add(bill);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return bill.billId;
    }

    public async Task<RoutineBillIndexViewModel> ListAsync(string owner, DateTime today, int page=1, CancellationToken ct=default)
    {
        var query=db.RoutineBills.AsNoTracking().Where(b=>b.userId==owner&&b.status!="Deleted");
        var until=today.Date.AddDays(30);
        var model=new RoutineBillIndexViewModel
        {
            Today=today.Date,Count=await query.CountAsync(ct),ActiveCount=await query.CountAsync(b=>b.status=="Active",ct),
            UpcomingCount=await query.CountAsync(b=>b.status=="Active"&&b.dueDate>=today.Date&&b.dueDate<=until,ct),
            OverdueCount=await query.CountAsync(b=>b.status=="Active"&&b.dueDate<today.Date,ct)
        };
        model.Page=Math.Clamp(page,1,model.Pages);
        model.Bills=await query.Include(b=>b.ExpenseCategory).OrderBy(b=>b.dueDate).ThenBy(b=>b.billId).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        return model;
    }
    private static string Validate(CreateRoutineBillViewModel model)
    {
        var name=(model.BillName??"").Trim();
        if(name.Length==0||name.Length>100)throw new InvalidOperationException("Enter a bill name of 1–100 characters.");
        if(!RoutineBillOptions.Types.Contains(model.BillType)||!RoutineBillOptions.Frequencies.Contains(model.Frequency)||!RoutineBillOptions.PaymentMethods.Contains(model.PaymentMethod))
            throw new InvalidOperationException("Choose a valid bill type, frequency and payment method.");
        if(!model.Amount.HasValue||model.Amount<.01m||model.Amount>999999999.99m||model.Amount!=decimal.Round(model.Amount!.Value,2,MidpointRounding.AwayFromZero))
            throw new InvalidOperationException("Enter an amount from RM 0.01 to RM 999,999,999.99 with at most two decimal places.");
        if(!model.DueDate.HasValue||model.DueDate!.Value.Year<1000)throw new InvalidOperationException("Choose a valid due date.");
        if(model.ReminderPeriod<0||model.ReminderPeriod>365)throw new InvalidOperationException("Reminder days must be from 0 to 365.");
        if(model.Description?.Length>500)throw new InvalidOperationException("Description must not exceed 500 characters.");
        return name;
    }

    private async Task LockOwnerAsync(string owner,CancellationToken ct)
    {
        var users=await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if(!users.Any(u=>u.AccountStatus==AccountStatus.Active&&(u.Role=="User"||u.Role=="Parent")))throw new InvalidOperationException("An active user account is required.");
    }

    public async Task<bool> EditAsync(string owner,EditRoutineBillViewModel model,CancellationToken ct=default)
    {
        var name=Validate(model);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await LockOwnerAsync(owner,ct);
        var bill=await db.RoutineBills.SingleOrDefaultAsync(b=>b.billId==model.BillId&&b.userId==owner&&b.status!="Deleted",ct);
        if(bill==null)return false;
        if(!string.Equals(RoutineBillEditStamp.For(bill),model.EditStamp,StringComparison.Ordinal))throw new InvalidOperationException("This bill changed after you opened it. Reload the edit page before saving.");
        if(model.Status!="Active"&&model.Status!="Inactive"&&!(model.Status=="Completed"&&bill.status=="Completed"))throw new InvalidOperationException("Choose a valid bill status.");
        var categories=await db.ExpenseCategories.FromSqlInterpolated($"SELECT * FROM `ExpenseCategories` WHERE `ExpenseCategoryId` = {model.ExpenseCategoryId} LOCK IN SHARE MODE").AsNoTracking().ToListAsync(ct);
        if(!categories.Any(c=>c.IsActive))throw new InvalidOperationException("Choose an active expense category.");
        var due=model.DueDate!.Value.Date;
        if(model.Status=="Active"&&await db.RoutineBillPayments.AnyAsync(p=>p.billId==bill.billId&&p.dueDate==due,ct))throw new InvalidOperationException("This scheduled date has already been paid. Choose the next unpaid due date.");
        if(bill.dueDate!=due||bill.frequency!=model.Frequency)bill.recurrenceAnchorDay=due.Day;
        bill.billName=name;bill.billType=model.BillType;bill.expenseCategoryId=model.ExpenseCategoryId;bill.amount=model.Amount!.Value;
        bill.dueDate=due;bill.frequency=model.Frequency;bill.paymentMethod=model.PaymentMethod;bill.reminderPeriod=model.ReminderPeriod;
        bill.description=string.IsNullOrWhiteSpace(model.Description)?null:model.Description.Trim();bill.status=model.Status;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }

    public async Task<bool> DeleteAsync(string owner,string id,string stamp,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await LockOwnerAsync(owner,ct);
        var bill=await db.RoutineBills.SingleOrDefaultAsync(b=>b.billId==id&&b.userId==owner&&b.status!="Deleted",ct);
        if(bill==null)return false;
        if(!string.Equals(RoutineBillEditStamp.For(bill),stamp,StringComparison.Ordinal))throw new InvalidOperationException("This bill changed after you opened the confirmation. Review it again before deleting.");
        // Retain the schedule for payment/expense history; normal owner lists exclude it.
        bill.status="Deleted";
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }


    public static DateTime? NextDueDate(RoutineBill bill)
    {
        if(bill.frequency=="OneTime")return null;
        try
        {
            if(bill.frequency=="Weekly")return bill.dueDate.AddDays(7);
            var months=bill.frequency switch { "Monthly"=>1,"Quarterly"=>3,"Yearly"=>12,_=>throw new InvalidOperationException("Choose a valid frequency before recording payment.") };
            var next=new DateTime(bill.dueDate.Year,bill.dueDate.Month,1).AddMonths(months);
            return new DateTime(next.Year,next.Month,Math.Clamp(bill.recurrenceAnchorDay,1,DateTime.DaysInMonth(next.Year,next.Month)));
        }
        catch(ArgumentOutOfRangeException){throw new InvalidOperationException("The next due date exceeds the supported date range. Edit this schedule before paying.");}
    }

    public async Task<bool> PayAsync(string owner,PayRoutineBillViewModel model,CancellationToken ct=default)
    {
        var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        if(!model.Amount.HasValue||model.Amount<.01m||model.Amount>999999999.99m||model.Amount!=decimal.Round(model.Amount.Value,2))
            throw new InvalidOperationException("Enter a positive payment amount with at most two decimal places.");
        if(!model.PaidDate.HasValue||model.PaidDate.Value.Year<1000||model.PaidDate.Value.Date>today)
            throw new InvalidOperationException("Choose today or a past payment date.");
        if(!RoutineBillOptions.PaymentMethods.Contains(model.PaymentMethod))throw new InvalidOperationException("Choose a valid payment method.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await LockOwnerAsync(owner,ct);
        var bill=await db.RoutineBills.SingleOrDefaultAsync(b=>b.billId==model.BillId&&b.userId==owner&&b.status!="Deleted",ct);
        if(bill==null)return false;
        if(bill.status!="Active")throw new InvalidOperationException("Only active bills can be marked as paid.");
        if(bill.dueDate>=new DateTime(today.Year,today.Month,1).AddMonths(1))throw new InvalidOperationException("This bill is due in a future month. You can record it from the start of its due month.");
        if(RoutineBillEditStamp.For(bill)!=model.EditStamp)throw new InvalidOperationException("This bill changed or was already paid. Reload the payment page before continuing.");
        if(await db.RoutineBillPayments.AnyAsync(p=>p.billId==bill.billId&&p.dueDate==bill.dueDate,ct))throw new InvalidOperationException("This scheduled occurrence has already been paid.");
        var categories=await db.ExpenseCategories.FromSqlInterpolated($"SELECT * FROM `ExpenseCategories` WHERE `ExpenseCategoryId` = {bill.expenseCategoryId} LOCK IN SHARE MODE").AsNoTracking().ToListAsync(ct);
        if(!categories.Any(c=>c.IsActive))throw new InvalidOperationException("Edit this bill to select an active expense category before paying.");
        var next=NextDueDate(bill);
        if(next.HasValue&&await db.RoutineBillPayments.AnyAsync(p=>p.billId==bill.billId&&p.dueDate==next.Value,ct))throw new InvalidOperationException("The next scheduled date was already paid. Edit this schedule before continuing.");
        var expenseId=await ids.GenerateIdAsync("EXP");
        db.Expenses.Add(new Expense{ExpenseId=expenseId,UserId=owner,ExpenseCategoryId=bill.expenseCategoryId,Amount=model.Amount.Value,ExpenseDate=model.PaidDate.Value.Date,PaymentMethod=model.PaymentMethod,Description=$"Bill payment: {bill.billName} (due {bill.dueDate:yyyy-MM-dd})"});
        db.RoutineBillPayments.Add(new RoutineBillPayment{paymentId=await ids.GenerateIdAsync("RBP"),billId=bill.billId,expenseId=expenseId,dueDate=bill.dueDate,paidDate=model.PaidDate.Value.Date,amount=model.Amount.Value,paymentMethod=model.PaymentMethod,billNameSnapshot=bill.billName});
        if(next.HasValue)bill.dueDate=next.Value;else bill.status="Completed";
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
