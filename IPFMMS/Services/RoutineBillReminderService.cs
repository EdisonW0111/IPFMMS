using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class RoutineBillReminderService(ApplicationDbContext db,IdGeneratorService ids,NotificationEmailService email,IConfiguration config)
{
    public async Task RefreshAsync(string owner,DateTime today,CancellationToken ct=default)
    {
        if(!config.GetValue<bool>("RoutineBills:RemindersEnabled"))return;
        today=today.Date;
        await using(var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct))
        {
            var accounts=await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            if(!accounts.Any(u=>u.AccountStatus==AccountStatus.Active&&(u.Role=="User"||u.Role=="Parent")))return;
            var bills=await db.RoutineBills.AsNoTracking().Where(b=>b.userId==owner&&b.status=="Active"&&b.dueDate<=today.AddDays(b.reminderPeriod)
                && !db.RoutineBillPayments.Any(p=>p.billId==b.billId&&p.dueDate==b.dueDate)).ToListAsync(ct);
            var existing=(await db.Notifications.Where(n=>n.userId==owner&&n.routineBillId!=null).Select(n=>n.eventKey).ToListAsync(ct)).ToHashSet();
            foreach(var bill in bills)
            {
                var type=bill.dueDate<today?"BillOverdue":bill.dueDate==today?"BillDueToday":"BillUpcoming";
                var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"bill|{owner}|{bill.billId}|{bill.dueDate:yyyy-MM-dd}|{type}")));
                if(!existing.Add(key))continue;
                var label=type=="BillOverdue"?"overdue":type=="BillDueToday"?"due today":"upcoming";
                db.Notifications.Add(new Notification{notificationId=await ids.GenerateIdAsync("NTF"),userId=owner,notificationType=type,title=$"Bill {label}",
                    message=$"{bill.billName}: RM {bill.amount.ToString("N2",CultureInfo.InvariantCulture)}, scheduled for {bill.dueDate:dd MMM yyyy}. This payment is {label}, checked on {today:dd MMM yyyy} (Malaysia time). Open Routine Bills to view the schedule or record a payment you have made. No bank payment has been made by this reminder.",
                    eventKey=key,routineBillId=bill.billId,billDueDate=bill.dueDate,createdDateTime=DateTime.UtcNow,emailStatus="Pending"});
            }
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        await email.DeliverPendingAsync(owner,ct);
    }
}
