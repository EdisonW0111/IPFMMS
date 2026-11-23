using System.Data;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace IPFMMS.Services;
public sealed class SharedAccessException:Exception { }
public sealed class SharedOwnershipChangedException : InvalidOperationException
{
    public SharedOwnershipChangedException() : base("The group's ownership changed. Reload the page and try again.") { }
}
public sealed class SharedFinanceService(ApplicationDbContext db,IdGeneratorService ids,IAlertEmailSender email,ILogger<SharedFinanceService> logger)
{
    public static DateTime Today=>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    public static DateTime Month(string? value)
    {
        if(!DateTime.TryParseExact(value,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date)||date.Year<1000||date.Year>9998)throw new InvalidOperationException("Choose a valid month.");return date;
    }
    public static void Money(decimal? value){if(!value.HasValue||value<.01m||value>999999999.99m||value!=decimal.Round(value.Value,2))throw new InvalidOperationException("Enter an amount from RM 0.01 to RM 999,999,999.99 with at most two decimal places.");}
    private static bool Eligible(User u)=>u.AccountStatus==AccountStatus.Active&&(u.Role=="User"||u.Role=="Parent");
    private async Task<(IDbContextTransaction Tx,SharedFinance Finance)> Locked(string actor,string id,bool release,CancellationToken ct,string? deliveryTarget=null)
    {
        // Lock account IDs before the group, in the same order used by personal finances
        // and Admin access changes. A newly promoted owner can change this lock set;
        // retry rather than taking another account lock while holding the group.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var ownerIds = await db.SharedFinanceMembers.AsNoTracking()
                .Where(m => m.sharedFinanceId == id && m.memberRole == "Owner" && m.invitationStatus == "Accepted")
                .Select(m => m.userId).ToArrayAsync(ct);
            var lockIds = ownerIds.Append(actor).Append(deliveryTarget ?? actor)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var parameters = string.Join(",", Enumerable.Range(0, lockIds.Length).Select(i => "{" + i + "}"));
            var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                var users = await db.Users.FromSqlRaw(
                    "SELECT * FROM `Users` WHERE `UserId` IN (" + parameters + ") ORDER BY `UserId` FOR UPDATE",
                    lockIds.Cast<object>().ToArray()).AsNoTracking().ToListAsync(ct);
                if (!users.Any(u => u.UserId == actor && Eligible(u))) throw new SharedAccessException();
                var rows = await db.SharedFinances.FromSqlInterpolated(
                    $"SELECT * FROM `SharedFinances` WHERE `sharedFinanceId` = {id} FOR UPDATE").ToListAsync(ct);
                var group = rows.SingleOrDefault();
                if (group == null) throw new SharedAccessException();
                await db.Entry(group).ReloadAsync(ct);
                if (!release && group.status == "Deleted") throw new SharedAccessException();
                var currentOwners = await db.SharedFinanceMembers.AsNoTracking()
                    .Where(m => m.sharedFinanceId == id && m.memberRole == "Owner" && m.invitationStatus == "Accepted")
                    .Select(m => m.userId).ToArrayAsync(ct);
                if (currentOwners.Except(lockIds, StringComparer.OrdinalIgnoreCase).Any())
                {
                    await tx.DisposeAsync();
                    continue;
                }
                if (!release && !users.Any(u => currentOwners.Contains(u.UserId, StringComparer.OrdinalIgnoreCase) && Eligible(u)))
                    throw new SharedAccessException();
                return (tx, group);
            }
            catch { await tx.DisposeAsync(); throw; }
        }
        throw new SharedOwnershipChangedException();
    }
    private async Task<SharedFinanceMember> Membership(string actor,SharedFinance group,CancellationToken ct)
    {
        var member = await db.SharedFinanceMembers.SingleOrDefaultAsync(
            m => m.sharedFinanceId == group.sharedFinanceId && m.userId == actor, ct);
        if (member == null) throw new SharedAccessException();
        await db.Entry(member).ReloadAsync(ct);
        if (member.invitationStatus != "Accepted") throw new SharedAccessException();
        return member;
    }
    private async Task<SharedFinanceMember> OwnerMembership(string actor,SharedFinance group,CancellationToken ct)
    {
        var member = await Membership(actor, group, ct);
        if (member.memberRole != "Owner") throw new SharedAccessException();
        return member;
    }
    public IQueryable<SharedFinance> AvailableGroups() => db.SharedFinances.AsNoTracking().Where(g => g.status != "Deleted"
        && db.SharedFinanceMembers.Any(m => m.sharedFinanceId == g.sharedFinanceId && m.memberRole == "Owner"
            && m.invitationStatus == "Accepted" && m.User!.AccountStatus == AccountStatus.Active
            && (m.User.Role == "User" || m.User.Role == "Parent")));
    public IQueryable<SharedFinance> Accessible(string actor) => AvailableGroups().Where(g =>
        db.Users.Any(u => u.UserId == actor && u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent"))
        && db.SharedFinanceMembers.Any(m => m.sharedFinanceId == g.sharedFinanceId && m.userId == actor && m.invitationStatus == "Accepted"));
    public IQueryable<SharedFinance> Managed(string actor) => Accessible(actor).Where(g =>
        db.SharedFinanceMembers.Any(m => m.sharedFinanceId == g.sharedFinanceId && m.userId == actor
            && m.invitationStatus == "Accepted" && m.memberRole == "Owner"));
    public Task<int> ActiveOwnerCountAsync(string id,CancellationToken ct=default) => db.SharedFinanceMembers.AsNoTracking()
        .CountAsync(m => m.sharedFinanceId == id && m.memberRole == "Owner" && m.invitationStatus == "Accepted"
            && m.User!.AccountStatus == AccountStatus.Active && (m.User.Role == "User" || m.User.Role == "Parent"), ct);
    private void Validate(SharedFinanceFormViewModel m)
    {
        if(string.IsNullOrWhiteSpace(m.Name)||m.Name.Trim().Length>100)throw new InvalidOperationException("Enter a name of 1–100 characters.");
        if(m.FinanceType!="MonthlyBudget"&&m.FinanceType!="SavingsGoal")throw new InvalidOperationException("Choose a shared monthly budget or savings goal.");
        Money(m.Amount);if(m.Description?.Length>500)throw new InvalidOperationException("Description must not exceed 500 characters.");if(m.FinanceType=="MonthlyBudget")Month(m.Month);
    }
    public async Task<string> CreateAsync(string actor,SharedFinanceFormViewModel m,CancellationToken ct=default)
    {
        Validate(m);var emails=(m.InviteEmails??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(emails.Length<1||emails.Length>10||emails.Any(e=>!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(e)))throw new InvalidOperationException("Enter between 1 and 10 registered users' email addresses, separated by commas or new lines.");
        var recipients=await db.Users.AsNoTracking().Where(u=>emails.Contains(u.Email)).ToListAsync(ct);
        if(recipients.Count!=emails.Length||recipients.Any(u=>!Eligible(u)||u.UserId==actor))throw new InvalidOperationException("Invite only other registered active users. Check the supplied email addresses.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var me=await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {actor} FOR UPDATE").AsNoTracking().ToListAsync(ct);if(!me.Any(Eligible))throw new SharedAccessException();
        var group=new SharedFinance{sharedFinanceId=await ids.GenerateIdAsync("SHF"),sharedFinanceName=m.Name.Trim(),financeType=m.FinanceType,description=string.IsNullOrWhiteSpace(m.Description)?null:m.Description.Trim(),ownerId=actor};db.SharedFinances.Add(group);
        db.SharedFinanceMembers.Add(new(){sharedFinanceId=group.sharedFinanceId,userId=actor,memberRole="Owner",invitationStatus="Accepted",joinedDate=DateTime.UtcNow,emailStatus="NotRequested"});
        foreach(var u in recipients)db.SharedFinanceMembers.Add(new(){sharedFinanceId=group.sharedFinanceId,userId=u.UserId});
        if(group.financeType=="MonthlyBudget")db.SharedBudgets.Add(new(){sharedBudgetId=await ids.GenerateIdAsync("SHB"),sharedFinanceId=group.sharedFinanceId,budgetAmount=m.Amount!.Value,budgetMonth=Month(m.Month)});
        else db.SharedSavingsGoals.Add(new(){sharedSavingsGoalId=await ids.GenerateIdAsync("SHG"),sharedFinanceId=group.sharedFinanceId,targetAmount=m.Amount!.Value});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        foreach(var u in recipients)await DeliverInviteAsync(actor,group.sharedFinanceId,u.UserId,1,CancellationToken.None);return group.sharedFinanceId;
    }
    public async Task EditAsync(string actor,SharedFinanceFormViewModel m,CancellationToken ct=default)
    {
        Validate(m);var state=await Locked(actor,m.Id!,false,ct);await using var tx=state.Tx;var g=state.Finance;await OwnerMembership(actor,g,ct);
        if(g.revision!=m.Revision)throw new InvalidOperationException("This arrangement changed. Reopen the edit page before saving.");
        if(m.FinanceType!=g.financeType)throw new InvalidOperationException("Finance type cannot change after creation.");
        if(m.Status!="Active"&&m.Status!="Inactive")throw new InvalidOperationException("Choose Active or Inactive.");
        if(g.financeType=="SavingsGoal")
        {
            var saved=await SavedAsync(g.sharedFinanceId,null,ct);if(m.Amount<saved)throw new InvalidOperationException("The target cannot be below the amount already saved.");var goal=await db.SharedSavingsGoals.SingleAsync(x=>x.sharedFinanceId==g.sharedFinanceId,ct);goal.targetAmount=m.Amount!.Value;goal.status=saved>=goal.targetAmount?"Achieved":"Active";
        }
        else await PutBudget(g.sharedFinanceId,Month(m.Month),m.Amount!.Value,ct);
        g.sharedFinanceName=m.Name.Trim();g.description=string.IsNullOrWhiteSpace(m.Description)?null:m.Description.Trim();g.status=m.Status;g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    private async Task PutBudget(string id,DateTime month,decimal amount,CancellationToken ct)
    {
        var b=await db.SharedBudgets.SingleOrDefaultAsync(b=>b.sharedFinanceId==id&&b.budgetMonth==month,ct);if(b==null)db.SharedBudgets.Add(new(){sharedBudgetId=await ids.GenerateIdAsync("SHB"),sharedFinanceId=id,budgetMonth=month,budgetAmount=amount});else b.budgetAmount=amount;
    }
    public async Task BudgetAsync(string actor,string id,int revision,string month,decimal amount,CancellationToken ct=default)
    {
        Money(amount);var period=Month(month);var state=await Locked(actor,id,false,ct);await using var tx=state.Tx;var g=state.Finance;await OwnerMembership(actor,g,ct);if(g.financeType!="MonthlyBudget")throw new SharedAccessException();
        if(g.revision!=revision)throw new InvalidOperationException("The arrangement changed. Reload before updating its budget.");await PutBudget(id,period,amount,ct);g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task DeleteAsync(string actor,string id,int revision,CancellationToken ct=default)
    {
        var state=await Locked(actor,id,false,ct);await using var tx=state.Tx;var g=state.Finance;await OwnerMembership(actor,g,ct);if(g.revision!=revision)throw new InvalidOperationException("This arrangement changed. Review the confirmation again.");g.status="Deleted";g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task InviteAsync(string actor,string id,string address,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(address)||address.Length>255||!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(address))throw new InvalidOperationException("Enter a valid registered email address.");
        var target=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.Email==address.Trim(),ct);if(target==null||!Eligible(target)||target.UserId==actor)throw new InvalidOperationException("Choose another registered active user.");
        var state=await Locked(actor,id,false,ct,target.UserId);await using(var tx=state.Tx)
        {
            var g=state.Finance;await OwnerMembership(actor,g,ct);var freshTarget=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==target.UserId,ct);if(freshTarget==null||!Eligible(freshTarget))throw new InvalidOperationException("Choose another registered active user.");if(g.status!="Active")throw new InvalidOperationException("Activate the arrangement before inviting members.");
            var member=await db.SharedFinanceMembers.SingleOrDefaultAsync(m=>m.sharedFinanceId==id&&m.userId==target.UserId,ct);
            if(member==null)db.SharedFinanceMembers.Add(member=new(){sharedFinanceId=id,userId=target.UserId});
            else {if(member.invitationStatus=="Pending"||member.invitationStatus=="Accepted")throw new InvalidOperationException("This person already has a pending invitation or membership.");if(member.invitedDate>DateTime.UtcNow.AddMinutes(-1))throw new InvalidOperationException("Wait one minute before inviting this person again.");member.invitationVersion++;member.invitationStatus="Pending";member.joinedDate=null;member.invitedDate=DateTime.UtcNow;member.emailStatus="Pending";member.emailAttemptedDate=null;member.emailSentDate=null;}
            member.memberRole="Member";g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        var version=await db.SharedFinanceMembers.Where(m=>m.sharedFinanceId==id&&m.userId==target.UserId).Select(m=>m.invitationVersion).SingleAsync(ct);await DeliverInviteAsync(actor,id,target.UserId,version,CancellationToken.None);
    }
    public async Task DecideAsync(string actor,string id,int version,bool accept,CancellationToken ct=default)
    {
        var state=await Locked(actor,id,false,ct);await using var tx=state.Tx;var g=state.Finance;var m=await db.SharedFinanceMembers.SingleOrDefaultAsync(m=>m.sharedFinanceId==id&&m.userId==actor,ct);
        if(m==null||m.memberRole=="Owner"||m.invitationStatus!="Pending"||m.invitationVersion!=version)throw new SharedAccessException();if(g.status!="Active")throw new InvalidOperationException("This arrangement is inactive.");m.invitationStatus=accept?"Accepted":"Rejected";m.joinedDate=accept?DateTime.UtcNow:null;if(accept)await ResetSharesAsync(id,ct);g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task SetSharesAsync(string actor, SharedSharesViewModel model, CancellationToken ct = default)
    {
        var state = await Locked(actor, model.Id, false, ct);
        await using var tx = state.Tx;
        var group = state.Finance;
        await OwnerMembership(actor, group, ct);
        if (group.status != "Active") throw new InvalidOperationException("Activate this group before setting shares.");
        if (group.revision != model.Revision) throw new InvalidOperationException("Group membership or settings changed. Reopen the shares page.");
        var members = await db.SharedFinanceMembers.Where(m => m.sharedFinanceId == model.Id && m.invitationStatus == "Accepted").ToListAsync(ct);
        foreach (var member in members) await db.Entry(member).ReloadAsync(ct);
        var rows = model.Members;
        if (rows == null || rows.Count != members.Count || rows.Select(r => r.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Count ||
            rows.Any(r => !members.Any(m => string.Equals(m.userId, r.UserId, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("The accepted member list changed. Reopen this page.");
        if (rows.Any(r => !r.Percentage.HasValue || r.Percentage < 0m || r.Percentage > 100m || decimal.Round(r.Percentage.Value, 2) != r.Percentage))
            throw new InvalidOperationException("Enter each member's share from 0% to 100%, with at most two decimal places.");
        if (rows.Sum(r => r.Percentage!.Value) != 100m) throw new InvalidOperationException("Member shares must total exactly 100%.");
        foreach (var member in members) member.sharePercentage = rows.Single(r => string.Equals(r.UserId, member.userId, StringComparison.OrdinalIgnoreCase)).Percentage;
        group.revision++;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task ResetSharesAsync(string id, CancellationToken ct)
    {
        var members = await db.SharedFinanceMembers.Where(m => m.sharedFinanceId == id).ToListAsync(ct);
        foreach (var member in members) member.sharePercentage = null;
    }
    public async Task PromoteOwnerAsync(string actor,string id,string target,int revision,int version,CancellationToken ct=default)
    {
        var state = await Locked(actor, id, false, ct, target);
        await using var tx = state.Tx;
        var group = state.Finance;
        await OwnerMembership(actor, group, ct);
        if (group.status != "Active") throw new InvalidOperationException("Activate the arrangement before promoting a member.");
        if (group.revision != revision) throw new InvalidOperationException("This arrangement changed. Review the promotion again.");
        var member = await db.SharedFinanceMembers.SingleOrDefaultAsync(m => m.sharedFinanceId == id && m.userId == target, ct);
        if (member == null) throw new SharedAccessException();
        await db.Entry(member).ReloadAsync(ct);
        if (member.invitationVersion != version || member.invitationStatus != "Accepted" || member.memberRole != "Member")
            throw new InvalidOperationException("Only a current accepted Member can be promoted. Reload the members list.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == member.userId, ct);
        if (user == null || !Eligible(user)) throw new InvalidOperationException("Only an active member account can become an Owner.");
        member.memberRole = "Owner";
        group.revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task MemberActionAsync(string actor,string id,string target,int version,string action,CancellationToken ct=default)
    {
        var state=await Locked(actor,id,false,ct,target);await using var tx=state.Tx;var g=state.Finance;await OwnerMembership(actor,g,ct);
        var m=await db.SharedFinanceMembers.SingleOrDefaultAsync(m=>m.sharedFinanceId==id&&m.userId==target,ct);
        if(m==null)throw new SharedAccessException();await db.Entry(m).ReloadAsync(ct);
        if(m.invitationVersion!=version||m.memberRole=="Owner")throw new SharedAccessException();
        if(action=="Cancel"&&m.invitationStatus=="Pending")m.invitationStatus="Cancelled";
        else if(action=="Remove"&&m.invitationStatus=="Accepted"){if(await SavedAsync(id,m.userId,ct)>0)throw new InvalidOperationException("This member must release their savings allocation before removal.");m.invitationStatus="Removed";await ResetSharesAsync(id,ct);}
        else throw new InvalidOperationException("This member state changed; reload the page.");g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task LeaveAsync(string actor,string id,CancellationToken ct=default)
    {
        var state=await Locked(actor,id,false,ct);await using var tx=state.Tx;var g=state.Finance;var m=await Membership(actor,g,ct);
        if(m.memberRole=="Owner"&&!await db.SharedFinanceMembers.AsNoTracking().AnyAsync(other=>other.sharedFinanceId==id
            &&other.userId!=m.userId&&other.memberRole=="Owner"&&other.invitationStatus=="Accepted"
            &&other.User!.AccountStatus==AccountStatus.Active&&(other.User.Role=="User"||other.User.Role=="Parent"),ct))
            throw new InvalidOperationException("The last active Owner cannot leave. Promote another active Member to Owner first.");
        if(await SavedAsync(id,actor,ct)>0)throw new InvalidOperationException("Release your savings allocation before leaving.");m.invitationStatus="Left";await ResetSharesAsync(id,ct);g.revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task DeliverInviteAsync(string actor,string id,string target,int version,CancellationToken ct=default)
    {
        (IDbContextTransaction Tx,SharedFinance Finance) state;
        // The arrangement may have been archived between saving the invitation and delivery.
        // Keep the committed invitation instead of surfacing a post-commit access error.
        try{state=await Locked(actor,id,false,ct,target);}catch(SharedAccessException){return;}catch(SharedOwnershipChangedException){return;}
        await using var tx=state.Tx;var g=state.Finance;try{await OwnerMembership(actor,g,ct);}catch(SharedAccessException){return;}
        if(g.status!="Active")return;
        var m=await db.SharedFinanceMembers.SingleOrDefaultAsync(m=>m.sharedFinanceId==id&&m.userId==target&&m.invitationVersion==version&&m.invitationStatus=="Pending",ct);if(m==null)return;
        await db.Entry(m).ReloadAsync(ct);
        if(m.invitationVersion!=version||m.invitationStatus!="Pending")return;
        if(m.emailStatus=="Sent"||m.emailStatus=="Sending")return;
        var targetUser=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==target,ct);if(targetUser==null||!Eligible(targetUser))return;
        var preferences=await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p=>p.UserId==target,ct)??new UserPreference();
        if(!preferences.InvitationEmail){m.emailStatus="Disabled";await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return;}
        if(m.emailAttemptedDate>DateTime.UtcNow.AddMinutes(-1))return;
        m.emailStatus="Sending";m.emailAttemptedDate=DateTime.UtcNow;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        // Persist the claim before SMTP, then recheck group, invitation and recipient under
        // ordered account/group locks. No browser cancellation abandons delivery bookkeeping.
        (IDbContextTransaction Tx,SharedFinance Finance) send;
        try{send=await Locked(actor,id,false,CancellationToken.None,target);}
        catch(SharedOwnershipChangedException)
        {
            // SMTP has not started. Release only this still-current delivery claim,
            // so a later Owner retry is possible without duplicating a sent email.
            await db.SharedFinanceMembers.Where(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.emailStatus=="Sending")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.emailStatus,"Pending"),CancellationToken.None);
            return;
        }
        catch(SharedAccessException)
        {
            await db.SharedFinanceMembers.Where(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.emailStatus=="Sending")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.emailStatus,"Suppressed"),CancellationToken.None);
            return;
        }
        await using var sendTx=send.Tx;
        var fresh=await db.SharedFinanceMembers.AsNoTracking().SingleOrDefaultAsync(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.invitationStatus=="Pending"&&x.emailStatus=="Sending",CancellationToken.None);
        var recipient=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==target,CancellationToken.None);
        var senderIsOwner=await db.SharedFinanceMembers.AsNoTracking().AnyAsync(x=>x.sharedFinanceId==id&&x.userId==actor&&x.memberRole=="Owner"&&x.invitationStatus=="Accepted",CancellationToken.None);
        if(send.Finance.status!="Active"||!senderIsOwner||fresh==null||recipient==null||!Eligible(recipient))
        {
            await db.SharedFinanceMembers.Where(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.emailStatus=="Sending")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.emailStatus,"Suppressed"),CancellationToken.None);
            await sendTx.CommitAsync();return;
        }
        var currentPreferences=await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p=>p.UserId==target,CancellationToken.None)??new UserPreference();
        if(!currentPreferences.InvitationEmail)
        {
            await db.SharedFinanceMembers.Where(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.emailStatus=="Sending")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.emailStatus,"Disabled"),CancellationToken.None);
            await sendTx.CommitAsync();return;
        }
        var status="Sent";try{await email.SendAsync(recipient.Email,"IPFMMS - Shared finance invitation",$"You have been invited as a Member to {send.Finance.sharedFinanceName} in IPFMMS. Sign in to your existing account, open Shared Finance, and accept or reject the pending invitation. No group financial access is granted until you accept. No bank transfer is performed by accepting.");}
        catch(Exception){status="Failed";logger.LogWarning("Shared finance invitation email failed; pending invitation is retained.");}
        await db.SharedFinanceMembers.Where(x=>x.sharedFinanceId==id&&x.userId==target&&x.invitationVersion==version&&x.emailStatus=="Sending").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.emailStatus,status).SetProperty(x=>x.emailSentDate,status=="Sent"?DateTime.UtcNow:(DateTime?)null));await sendTx.CommitAsync();
    }
    public async Task<decimal> SavedAsync(string id,string? user=null,CancellationToken ct=default)=>await db.SharedFinanceTransactions.AsNoTracking().Where(t=>t.sharedFinanceId==id&&(user==null||t.userId==user)&&(t.transactionType=="Contribution"||t.transactionType=="Withdrawal")).SumAsync(t=>(decimal?)(t.transactionType=="Contribution"?t.amount:-t.amount),ct)??0;
    public async Task<string> RecordAsync(string actor,SharedEntryViewModel m,CancellationToken ct=default)
    {
        Money(m.Amount);if(!m.Date.HasValue||m.Date.Value.Year<1000||m.Date.Value.Date>Today)throw new InvalidOperationException("Choose today or a past transaction date.");if(m.Description?.Length>500)throw new InvalidOperationException("Description must not exceed 500 characters.");if(!RoutineBillOptions.PaymentMethods.Contains(m.PaymentMethod))throw new InvalidOperationException("Choose a valid payment method.");if(string.IsNullOrWhiteSpace(m.SubmissionKey)||m.SubmissionKey.Length!=32||!Guid.TryParseExact(m.SubmissionKey,"N",out _))throw new InvalidOperationException("Reopen the entry form before recording.");
        var releasing=m.Type=="Withdrawal";var state=await Locked(actor,m.Id,releasing,ct);await using var tx=state.Tx;var g=state.Finance;
        if(!releasing){await Membership(actor,g,ct);if(g.status!="Active")throw new InvalidOperationException("The arrangement is inactive.");}
        if(await db.SharedFinanceTransactions.AnyAsync(t=>t.userId==actor&&t.submissionKey==m.SubmissionKey,ct))throw new InvalidOperationException("This entry has already been recorded. Open a new form for a different transaction.");
        string category;string? expense=null;
        if(g.financeType=="MonthlyBudget")
        {
            if(m.Type!="Expense")throw new InvalidOperationException("Monthly budgets record shared spending.");
            var list=await db.ExpenseCategories.AsNoTracking().Where(c=>c.ExpenseCategoryId==m.CategoryId&&c.IsActive).ToListAsync(ct);if(list.Count!=1)throw new InvalidOperationException("Choose an active expense category.");category=list[0].ExpenseCategoryId;
            var date=new DateTime(m.Date.Value.Year,m.Date.Value.Month,1);if(!await db.SharedBudgets.AnyAsync(b=>b.sharedFinanceId==g.sharedFinanceId&&b.budgetMonth==date,ct))throw new InvalidOperationException("Ask the owner to set a shared budget for this transaction month first.");
            expense=await ids.GenerateIdAsync("EXP");db.Expenses.Add(new(){ExpenseId=expense,UserId=actor,ExpenseCategoryId=category,Amount=m.Amount!.Value,ExpenseDate=m.Date.Value.Date,PaymentMethod=m.PaymentMethod,Description=$"Shared spending: {g.sharedFinanceName}"});
        }
        else
        {
            if(m.Type!="Contribution"&&m.Type!="Withdrawal")throw new InvalidOperationException("Savings goals record contributions and withdrawals.");
            if(releasing)category=await db.SharedFinanceTransactions.Where(t=>t.sharedFinanceId==g.sharedFinanceId&&t.userId==actor&&t.transactionType=="Contribution").OrderBy(t=>t.createdDate).Select(t=>t.expenseCategoryId).FirstOrDefaultAsync(ct)??throw new InvalidOperationException("No savings allocation is available to release.");
            else{var categories=await db.ExpenseCategories.AsNoTracking().Where(c=>c.CategoryName=="Shared Savings"&&c.IsActive).ToListAsync(ct);if(categories.Count!=1)throw new InvalidOperationException("The Shared Savings category must exist and be active for contributions.");category=categories[0].ExpenseCategoryId;}
            var latest=await db.SharedFinanceTransactions.Where(t=>t.sharedFinanceId==g.sharedFinanceId&&t.userId==actor&&(t.transactionType=="Contribution"||t.transactionType=="Withdrawal")).MaxAsync(t=>(DateTime?)t.transactionDate,ct);if(latest.HasValue&&m.Date.Value.Date<latest.Value.Date)throw new InvalidOperationException("Record savings allocations in date order; this date precedes an existing entry.");
            if(releasing){if(m.Amount>await SavedAsync(g.sharedFinanceId,actor,ct))throw new InvalidOperationException("You can only withdraw your own currently allocated savings.");}
            else
            {
                var goal=await db.SharedSavingsGoals.SingleAsync(s=>s.sharedFinanceId==g.sharedFinanceId,ct);if(await SavedAsync(g.sharedFinanceId,null,ct)+m.Amount>goal.targetAmount)throw new InvalidOperationException("Contribution cannot exceed the amount remaining to reach the shared target.");
                var income=await db.Incomes.Where(i=>i.UserId==actor&&i.IncomeDate<=Today).SumAsync(i=>(decimal?)i.Amount,ct)??0;var spending=await db.Expenses.Where(e=>e.UserId==actor&&e.ExpenseDate<=Today).SumAsync(e=>(decimal?)e.Amount,ct)??0;var savings=await db.SavingsContributions.Where(c=>c.SavingsGoal!.UserId==actor).SumAsync(c=>(decimal?)c.Amount,ct)??0;var shared=await SharedSavingsAllocations.NetAsync(db,actor,ct:ct);
                if(m.Amount>income-spending-savings-shared)throw new InvalidOperationException("You do not have enough available funds for this shared savings contribution.");
            }
        }
        var row=new SharedFinanceTransaction{sharedTransactionId=await ids.GenerateIdAsync("SHT"),sharedFinanceId=g.sharedFinanceId,userId=actor,expenseCategoryId=category,transactionType=m.Type,amount=m.Amount!.Value,transactionDate=m.Date!.Value.Date,description=string.IsNullOrWhiteSpace(m.Description)?null:m.Description.Trim(),expenseId=expense,paymentMethod=m.PaymentMethod,submissionKey=m.SubmissionKey};db.SharedFinanceTransactions.Add(row);g.revision++;
        await db.SaveChangesAsync(ct);if(g.financeType=="SavingsGoal"){var goal=await db.SharedSavingsGoals.SingleAsync(s=>s.sharedFinanceId==g.sharedFinanceId,ct);goal.status=await SavedAsync(g.sharedFinanceId,null,ct)>=goal.targetAmount?"Achieved":"Active";await db.SaveChangesAsync(ct);}await tx.CommitAsync(ct);return row.sharedTransactionId;
    }
}
