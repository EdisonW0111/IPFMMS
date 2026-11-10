namespace IPFMMS.Models;
public static class DebtOptions
{
    public static IReadOnlyList<string> Types { get; } = Array.AsReadOnly(new[]{"Education Loan","Personal Loan","Credit Card","Vehicle Loan","Mortgage","Family or Friends","Other"});
    public static DateTime Today => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    public static decimal Progress(Debt d) => d.originalAmount<=0?0:Math.Clamp(decimal.Round((d.originalAmount-d.outstandingAmount)/d.originalAmount*100,1),0,100);
    public static string DisplayStatus(Debt d)=>d.status=="Active"?(d.dueDate<Today?"Overdue":d.dueDate==Today?"Due today":"Active"):d.status;
}
