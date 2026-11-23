using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using IPFMMS.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
if (Environment.GetEnvironmentVariable("IPFMMS_FEEDBACK_TESTS") != "1")
    throw new InvalidOperationException("Set IPFMMS_FEEDBACK_TESTS=1 to opt into the isolated localhost:33317 feedback_checks database. Never point these tests at real data.");
var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql("Server=127.0.0.1;Port=33317;Database=feedback_checks;User=root",new MySqlServerVersion(new Version(8,0,46))).Options;
using var db=new ApplicationDbContext(options);
await db.Database.MigrateAsync();
int checks=0;
void Check(bool pass,string name){if(!pass)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
async Task Reject(Func<Task> action,string name){try{await action();}catch(InvalidOperationException){Check(true,name);return;}throw new Exception("FAIL accepted: "+name);}
var today=DebtOptions.Today;var month=new DateTime(today.Year,today.Month,1);
var a="QA"+Guid.NewGuid().ToString("N")[..8];var b="QB"+Guid.NewGuid().ToString("N")[..8];var outsider="QC"+Guid.NewGuid().ToString("N")[..8];
foreach(var id in new[]{a,b,outsider})db.Users.Add(new User{UserId=id,FullName=id,Email=id+"@example.invalid",PasswordHash="test-only",AccountStatus="Active",Role="User"});
await db.SaveChangesAsync();
var ids=new IdGeneratorService(db);
if(!await db.IncomeCategories.AnyAsync())db.IncomeCategories.Add(new(){IncomeCategoryId="ICQA",CategoryName="QA income"});
if(!await db.ExpenseCategories.AnyAsync())db.ExpenseCategories.Add(new(){ExpenseCategoryId="ECQA",CategoryName="QA expense"});
if(!await db.ExpenseCategories.AnyAsync(c=>c.CategoryName=="Debt Repayment"))db.ExpenseCategories.Add(new(){ExpenseCategoryId="ECDQA",CategoryName="Debt Repayment"});
await db.SaveChangesAsync();
var ic=await db.IncomeCategories.Select(c=>c.IncomeCategoryId).FirstAsync();
var ec=await db.ExpenseCategories.Select(c=>c.ExpenseCategoryId).FirstAsync();
var gid="SG"+Guid.NewGuid().ToString("N")[..8];
db.Incomes.Add(new(){IncomeId=await ids.GenerateIdAsync("INC"),UserId=a,IncomeCategoryId=ic,Amount=1000,IncomeDate=today});
db.Incomes.Add(new(){IncomeId=await ids.GenerateIdAsync("INC"),UserId=a,IncomeCategoryId=ic,Amount=9000,IncomeDate=today.AddDays(1)});
db.Expenses.Add(new(){ExpenseId=await ids.GenerateIdAsync("EXP"),UserId=a,ExpenseCategoryId=ec,Amount=300,ExpenseDate=today,PaymentMethod="Cash"});
db.SavingsGoals.Add(new(){SavingsGoalId=gid,UserId=a,GoalName="Test long term",TargetAmount=20000,TargetDate=today.AddYears(2)});
await db.SaveChangesAsync();
db.SavingsContributions.Add(new(){ContributionId=await ids.GenerateIdAsync("SCT"),SavingsGoalId=gid,Amount=200,ContributionDate=today,ContributionMethod="Manual"});await db.SaveChangesAsync();
Check(await AvailableFundsService.BalanceAsync(db,a,today)==500,"available balance excludes future income and existing savings");
Check(await AvailableFundsService.BalanceAsync(db,b,today)==0,"balance isolated by account");
T Login<T>(T c,string owner) where T:Controller {c.ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,owner),new Claim(ClaimTypes.Role,"User")},"Test"))}};c.TempData=new TempDataDictionary(c.HttpContext,new MemoryTemp());return c;}
var sc=Login(new SavingsController(db,ids),a);
var fail=await sc.Contribute(new RecordSavingsContributionViewModel{SavingsGoalId=gid,Amount=500.01m,ContributionDate=today});
Check(fail is ViewResult&&!sc.ModelState.IsValid,"manual savings rejects excess by one cent");
Check(await db.SavingsContributions.Where(c=>c.SavingsGoalId==gid).SumAsync(c=>c.Amount)==200,"rejected savings writes nothing");
sc=Login(new SavingsController(db,ids),a);
Check(await sc.Contribute(new RecordSavingsContributionViewModel{SavingsGoalId=gid,Amount=50,ContributionDate=today}) is RedirectToActionResult,"manual savings within available balance succeeds");
Check(await AvailableFundsService.BalanceAsync(db,a,today)==450,"contribution reduces available balance");
var did="DT"+Guid.NewGuid().ToString("N")[..8];db.Debts.Add(new(){debtId=did,userId=a,debtName="Test debt",debtType="PersonalLoan",originalAmount=1000,outstandingAmount=1000,openingOutstandingAmount=1000,monthlyRepaymentAmount=50,dueDate=today});await db.SaveChangesAsync();
var debtService=new DebtService(db,ids);
await Reject(()=>debtService.RepayAsync(a,new(){DebtId=did,Revision=1,Amount=450.01m,PaymentDate=today,PaymentMethod="Cash"}),"debt rejects insufficient available funds");
db.ChangeTracker.Clear();Check(!await db.DebtRepayments.AnyAsync(p=>p.debtId==did),"rejected debt repayment creates no expense or repayment");
Check(await debtService.RepayAsync(a,new(){DebtId=did,Revision=1,Amount=50,PaymentDate=today,PaymentMethod="Cash"}),"affordable debt repayment succeeds");
Check(await AvailableFundsService.BalanceAsync(db,a,today)==400,"debt expense counted exactly once");
var autoId="AU"+Guid.NewGuid().ToString("N")[..8];var due=month;
db.SavingsGoals.Add(new(){SavingsGoalId=autoId,UserId=a,GoalName="Automatic test",TargetAmount=1200,TargetDate=today.AddYears(1),SavingsMethod="AutomaticMonthlyTarget",MonthlyTargetAmount=500,AutomaticSavingDay=1,NextAutomaticSavingDate=due,AutomaticSavingsBaseline=0});await db.SaveChangesAsync();
var auto=new AutomaticSavingsService(db,ids);await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();
Check((await db.SavingsAutomaticAttempts.SingleAsync(x=>x.SavingsGoalId==autoId)).Status=="InsufficientFunds","current-month missed due date checks funds instead of skipping");
Check((await db.SavingsGoals.FindAsync(autoId))!.NextAutomaticSavingDate==due,"insufficient funds remains retryable");
await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();Check(await db.SavingsAutomaticAttempts.CountAsync(x=>x.SavingsGoalId==autoId)==1,"failed retries reuse one monthly attempt");
db.Incomes.Add(new(){IncomeId=await ids.GenerateIdAsync("INC"),UserId=a,IncomeCategoryId=ic,Amount=200,IncomeDate=today});await db.SaveChangesAsync();await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();
Check((await db.SavingsAutomaticAttempts.SingleAsync(x=>x.SavingsGoalId==autoId)).Status=="Saved","automatic savings retries after income arrives");
Check(await db.SavingsContributions.Where(x=>x.SavingsGoalId==autoId).SumAsync(x=>x.Amount)==500,"catch-up deducts planned amount");
await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();Check(await db.SavingsContributions.CountAsync(x=>x.SavingsGoalId==autoId)==1,"automatic savings is idempotent");
Check(await AvailableFundsService.BalanceAsync(db,a,today)==100,"automatic saving reduces available balance");
var legacy="LG"+Guid.NewGuid().ToString("N")[..8];db.SavingsGoals.Add(new(){SavingsGoalId=legacy,UserId=a,GoalName="Legacy missed",TargetAmount=100,TargetDate=today.AddYears(1),SavingsMethod="AutomaticMonthlyTarget",MonthlyTargetAmount=10,AutomaticSavingDay=1,NextAutomaticSavingDate=due.AddMonths(1),AutomaticSavingsBaseline=0});db.SavingsAutomaticAttempts.Add(new(){SavingsGoalId=legacy,DueDate=due,PlannedAmount=10,Status="MissedOffline",ProcessedAtUtc=DateTime.UtcNow});await db.SaveChangesAsync();await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();
Check((await db.SavingsAutomaticAttempts.SingleAsync(x=>x.SavingsGoalId==legacy)).Status=="Saved","previously skipped current-month attempt catches up");
var scheduleId=await ids.GenerateIdAsync("BSC");db.BudgetSchedules.Add(new(){BudgetScheduleId=scheduleId,UserId=a,ExpenseCategoryId=ec,BudgetType="FixedAmount",BudgetAmount=50,NextBudgetMonth=month.AddMonths(3),EndMonth=month.AddMonths(6),IsActive=true});
var budgets=new List<Budget>();for(int i=0;i<3;i++){var row=new Budget{BudgetId=await ids.GenerateIdAsync("BDG"),UserId=a,ExpenseCategoryId=ec,BudgetMonth=month.AddMonths(i),BudgetType="FixedAmount",BudgetAmount=50,BudgetScheduleId=scheduleId,RepeatSeriesVersion=1};budgets.Add(row);db.Budgets.Add(row);}await db.SaveChangesAsync();
var bc=Login(new BudgetController(db,ids,new(db,ids)),a);
Check(await bc.Edit(new EditBudgetViewModel{BudgetId=budgets[0].BudgetId,BudgetType="FixedAmount",BudgetAmount=60,EditScope="month"}) is RedirectToActionResult,"month-only budget edit succeeds");db.ChangeTracker.Clear();
Check((await db.BudgetSchedules.FindAsync(scheduleId))!.BudgetAmount==50,"month-only edit preserves repeating template");Check((await db.Budgets.FindAsync(budgets[1].BudgetId))!.BudgetAmount==50,"month-only edit preserves future saved months");
bc=Login(new BudgetController(db,ids,new(db,ids)),a);
Check(await bc.Edit(new EditBudgetViewModel{BudgetId=budgets[0].BudgetId,BudgetType="FixedAmount",BudgetAmount=80,EditScope="series",RepeatMonthly=true,RepeatUntil=month.AddMonths(6).ToString("yyyy-MM")}) is RedirectToActionResult,"series edit succeeds");db.ChangeTracker.Clear();
Check(await db.Budgets.Where(x=>x.BudgetScheduleId==scheduleId).AllAsync(x=>x.BudgetAmount==80),"series edit updates all saved future linked budgets");
Check((await db.BudgetSchedules.FindAsync(scheduleId))!.BudgetAmount==80,"series edit updates future generation template");
var shared=new SharedFinanceService(db,ids,new FakeMail(),NullLogger<SharedFinanceService>.Instance);var sharedId=await shared.CreateAsync(a,new(){Name="Percent test",FinanceType="SavingsGoal",Amount=100.01m,InviteEmails=b+"@example.invalid"});
await shared.DecideAsync(b,sharedId,1,true);
int rev=(await db.SharedFinances.FindAsync(sharedId))!.revision;
SharedSharesViewModel Plan(decimal x,decimal y)=>new(){Id=sharedId,Revision=rev,Members=new(){new(){UserId=a,Percentage=x},new(){UserId=b,Percentage=y}}};
await Reject(()=>shared.SetSharesAsync(a,Plan(60,50)),"share percentages over 100 rejected");await Reject(()=>shared.SetSharesAsync(a,Plan(50,40)),"share percentages under 100 rejected");
try{await shared.SetSharesAsync(b,Plan(60,40));throw new Exception("member edited shares");}catch(SharedAccessException){Check(true,"only accepted Owners edit shares");}
await shared.SetSharesAsync(a,Plan(60,40));db.ChangeTracker.Clear();var members=await db.SharedFinanceMembers.Where(m=>m.sharedFinanceId==sharedId).ToListAsync();var amounts=SharedShareCalculator.Calculate(100.01m,members);
Check(amounts[a]==60.01m&&amounts[b]==40,"60/40 split rounds to exact target total");Check(!await db.SharedFinanceTransactions.AnyAsync(t=>t.sharedFinanceId==sharedId),"setting shares creates no payments or expenses");
await Reject(()=>shared.SetSharesAsync(a,Plan(50,50)),"stale share edit rejected");await shared.LeaveAsync(b,sharedId);db.ChangeTracker.Clear();Check(await db.SharedFinanceMembers.Where(m=>m.sharedFinanceId==sharedId).AllAsync(m=>m.sharePercentage==null),"membership change resets expected shares");
var notices=new NotificationService(db);var mail=new FakeMail();var emails=new NotificationEmailService(db,notices,mail,NullLogger<NotificationEmailService>.Instance);var personal=new PersonalNotificationService(db,ids,new(db));
await personal.RefreshAsync(a,today);db.ChangeTracker.Clear();var firstCount=await notices.Visible(a).CountAsync();Check(firstCount>=3,"own budget, due debt and automatic savings notifications generated");await personal.RefreshAsync(a,today);db.ChangeTracker.Clear();Check(await notices.Visible(a).CountAsync()==firstCount,"personal notifications deduplicate refreshes");Check(!await notices.Visible(b).AnyAsync(),"notifications do not leak to another account");
await emails.DeliverPendingAsync(a);Check(mail.Sent==0,"personal financial email defaults off");
var pref=new UserPreferenceService(db);await pref.SaveAsync(a,new(){Revision=0,FinancialInApp=false,FinancialEmail=false});db.ChangeTracker.Clear();
var newDebt="ND"+Guid.NewGuid().ToString("N")[..8];db.Debts.Add(new(){debtId=newDebt,userId=a,debtName="Preference test",originalAmount=10,outstandingAmount=10,dueDate=today});await db.SaveChangesAsync();await personal.RefreshAsync(a,today);db.ChangeTracker.Clear();Check(await notices.Visible(a).CountAsync()==firstCount,"in-app opt-out suppresses new financial alerts");
Check(await notices.UnreadAsync(a)==firstCount,"unread count includes personal alerts");await notices.MarkAllReadAsync(a);Check(await notices.UnreadAsync(a)==0,"mark all read covers personal alerts");
var billid="BL"+Guid.NewGuid().ToString("N")[..8];db.RoutineBills.Add(new(){billId=billid,userId=b,billName="Reminder test",billType="Bill",expenseCategoryId=ec,amount=5,dueDate=today,frequency="Monthly",reminderPeriod=3});await db.SaveChangesAsync();
var reminders=new RoutineBillReminderService(db,ids,emails,new ConfigurationBuilder().Build());await reminders.RefreshAsync(b,today,deliverEmail:false);db.ChangeTracker.Clear();Check(await notices.Visible(b).AnyAsync(n=>n.notificationType=="BillDueToday"),"bill in-app reminders work with no explicit enabled setting");Check(mail.Sent==0,"notification refresh does not wait for or send email");
await reminders.RefreshAsync(b,today,deliverEmail:false);Check(await notices.Visible(b).CountAsync(n=>n.notificationType=="BillDueToday")==1,"bill reminder refresh is idempotent");

// A later month's competing allocation must reject the whole series edit atomically.
var ec2="EC"+Guid.NewGuid().ToString("N")[..8];db.ExpenseCategories.Add(new(){ExpenseCategoryId=ec2,CategoryName=ec2});await db.SaveChangesAsync();
db.Budgets.Add(new(){BudgetId=await ids.GenerateIdAsync("BDG"),UserId=a,ExpenseCategoryId=ec2,BudgetMonth=month.AddMonths(2),BudgetType="Percentage",BudgetPercentage=70});await db.SaveChangesAsync();
bc=Login(new BudgetController(db,ids,new(db,ids)),a);
var rejectedSeries=await bc.Edit(new EditBudgetViewModel{BudgetId=budgets[0].BudgetId,BudgetType="Percentage",BudgetPercentage=40,EditScope="series",RepeatMonthly=true});
Check(rejectedSeries is ViewResult&&!bc.ModelState.IsValid,"future month over-allocation rejects series edit");db.ChangeTracker.Clear();
Check(await db.Budgets.Where(x=>x.BudgetScheduleId==scheduleId).AllAsync(x=>x.BudgetAmount==80&&x.BudgetType=="FixedAmount"),"failed series edit preserves every monthly budget");
Check((await db.BudgetSchedules.FindAsync(scheduleId))!.BudgetType=="FixedAmount","failed series edit preserves template");
// Two savings allocations competing for one balance must serialize on the owner.
db.Incomes.Add(new(){IncomeId=await ids.GenerateIdAsync("INC"),UserId=outsider,IncomeCategoryId=ic,Amount=100,IncomeDate=today});
var raceGoals=new[]{"RA"+Guid.NewGuid().ToString("N")[..8],"RB"+Guid.NewGuid().ToString("N")[..8]};
foreach(var id in raceGoals)db.SavingsGoals.Add(new(){SavingsGoalId=id,UserId=outsider,GoalName=id,TargetAmount=1000,TargetDate=today.AddYears(1)});await db.SaveChangesAsync();
async Task<bool> Race(string goal){using var other=new ApplicationDbContext(options);var ctrl=Login(new SavingsController(other,new(other)),outsider);return await ctrl.Contribute(new RecordSavingsContributionViewModel{SavingsGoalId=goal,Amount=80,ContributionDate=today}) is RedirectToActionResult;}
var racing=await Task.WhenAll(Race(raceGoals[0]),Race(raceGoals[1]));Check(racing.Count(x=>x)==1,"concurrent savings requests cannot overspend shared available balance");
Check(await AvailableFundsService.BalanceAsync(db,outsider,today)==20,"concurrent allocations leave nonnegative available funds");
// Pending requests become in-app alerts; a refresh cannot duplicate them.
var linkid="PL"+Guid.NewGuid().ToString("N")[..8];db.ParentStudentLinks.Add(new(){linkId=linkid,parentId=a,userId=b,linkStatus="Pending",requestVersion=1,requestedDate=DateTime.UtcNow});await db.SaveChangesAsync();
var pendingGroup=await shared.CreateAsync(a,new(){Name="Pending invitation",FinanceType="SavingsGoal",Amount=100,InviteEmails=outsider+"@example.invalid"});
await personal.RefreshAsync(b,today);await personal.RefreshAsync(outsider,today);db.ChangeTracker.Clear();
Check(await notices.Visible(b).AnyAsync(n=>n.notificationType=="MonitoringInvitation"),"monitoring invitations appear in notification centre");
Check(await notices.Visible(outsider).AnyAsync(n=>n.notificationType=="SharedInvitation"),"shared invitations appear in notification centre");
var invitationCount=await notices.Visible(b).CountAsync(n=>n.notificationType=="MonitoringInvitation");await personal.RefreshAsync(b,today);db.ChangeTracker.Clear();Check(await notices.Visible(b).CountAsync(n=>n.notificationType=="MonitoringInvitation")==invitationCount,"invitation refresh is idempotent");
var oldId="OL"+Guid.NewGuid().ToString("N")[..8];var oldDate=month.AddMonths(-1).AddDays(1);
db.SavingsGoals.Add(new(){SavingsGoalId=oldId,UserId=b,GoalName="Older missed month",TargetAmount=1200,TargetDate=today.AddYears(1),SavingsMethod="AutomaticMonthlyTarget",MonthlyTargetAmount=10,AutomaticSavingDay=1,NextAutomaticSavingDate=oldDate,AutomaticSavingsBaseline=0});await db.SaveChangesAsync();await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();
Check((await db.SavingsAutomaticAttempts.SingleAsync(x=>x.SavingsGoalId==oldId&&x.DueDate==oldDate)).Status=="MissedOffline","older missed months do not create retroactive deductions");
Check(!await db.SavingsContributions.AnyAsync(x=>x.SavingsGoalId==oldId),"unfunded catch-up makes no contribution");

// Failed goals at the front of a batch must not starve later funded goals.
var batchPrefix="BA"+Guid.NewGuid().ToString("N")[..6];
for(int i=0;i<105;i++)db.SavingsGoals.Add(new(){SavingsGoalId=batchPrefix+i.ToString("D3"),UserId=b,GoalName="Unfunded batch",TargetAmount=100,TargetDate=today.AddYears(1),SavingsMethod="AutomaticMonthlyTarget",MonthlyTargetAmount=1,AutomaticSavingDay=1,NextAutomaticSavingDate=month,AutomaticSavingsBaseline=0});
var lastGoal="ZZ"+Guid.NewGuid().ToString("N")[..8];db.SavingsGoals.Add(new(){SavingsGoalId=lastGoal,UserId=outsider,GoalName="Later funded goal",TargetAmount=100,TargetDate=today.AddYears(1),SavingsMethod="AutomaticMonthlyTarget",MonthlyTargetAmount=5,AutomaticSavingDay=1,NextAutomaticSavingDate=month,AutomaticSavingsBaseline=0});await db.SaveChangesAsync();await auto.ProcessDueAsync(today);db.ChangeTracker.Clear();
Check(await db.SavingsContributions.AnyAsync(c=>c.SavingsGoalId==lastGoal&&c.Amount==5),"more than 100 unfunded goals cannot starve later funded savings");
await pref.SaveAsync(a,new(){Revision=1,FinancialInApp=false,FinancialEmail=true});db.ChangeTracker.Clear();
var emailDebt="EM"+Guid.NewGuid().ToString("N")[..8];db.Debts.Add(new(){debtId=emailDebt,userId=a,debtName="Email preference",originalAmount=20,outstandingAmount=20,dueDate=today});await db.SaveChangesAsync();await personal.RefreshAsync(a,today);db.ChangeTracker.Clear();
Check(await db.Notifications.AnyAsync(n=>n.userId==a&&n.notificationType=="DebtReminder"&&n.emailStatus=="Pending"&&!n.showInApp),"email and in-app preferences operate independently");await emails.DeliverPendingAsync(a);Check(mail.Sent>0,"opted-in personal alert email is delivered through fake sender");
Check(!db.Database.HasPendingModelChanges(),"migration snapshot matches current model");
Console.WriteLine($"RESULT: {checks} checks passed");
class FakeMail:IAlertEmailSender{public int Sent;public Task SendAsync(string r,string s,string m){Sent++;return Task.CompletedTask;}}
class MemoryTemp:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){}}
