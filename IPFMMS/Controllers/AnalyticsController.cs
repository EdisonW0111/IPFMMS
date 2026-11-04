using System.Globalization;
using System.Security.Claims;
using System.Text;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPFMMS.Controllers;

[Authorize(Roles = "User,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnalyticsController(AnalyticsService analytics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? from = null, string? to = null, CancellationToken ct = default)
    {
        var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(owner)) return Challenge();
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        if (!AnalyticsPeriod.TryParse(from, to, today, out var start, out var end, out var error))
        {
            ModelState.AddModelError(string.Empty, error!);
            AnalyticsPeriod.TryParse(null, null, today, out start, out end, out _);
        }
        var model = await analytics.BuildAsync(owner, start, end, ct);
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView("_Overview", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? from = null, string? to = null, CancellationToken ct = default)
    {
        var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(owner)) return Challenge();
        if (!AnalyticsPeriod.TryParse(from, to, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date,
            out var start, out var end, out var error)) return BadRequest(error);
        var model = await analytics.BuildAsync(owner, start, end, ct);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ReportCsv(model))).ToArray(),
            "text/csv; charset=utf-8", $"IPFMMS-financial-report-{model.FromValue}-to-{model.ToValue}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(string? from = null, string? to = null, CancellationToken ct = default)
    {
        var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(owner)) return Challenge();
        if (!AnalyticsPeriod.TryParse(from, to, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date,
            out var start, out var end, out var error)) return BadRequest(error);
        var model = await analytics.BuildAsync(owner, start, end, ct);
        ct.ThrowIfCancellationRequested();
        return File(AnalyticsPdfReport.Generate(model), "application/pdf",
            $"IPFMMS-financial-report-{model.FromValue}-to-{model.ToValue}.pdf");
    }

    [NonAction]
    public static string ReportCsv(AnalyticsViewModel model)
    {
        var csv = new StringBuilder();
        string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        // Category names can be user-managed: quote and neutralize spreadsheet formulas.
        string Cell(string text)
        {
            var first = text.TrimStart();
            bool numeric = decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
            if (!numeric && first.Length > 0 && "=+-@".Contains(first[0]) || text.StartsWith('\t') || text.StartsWith('\r') || text.StartsWith('\n')) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        void Row(params string[] cells) => csv.AppendLine(string.Join(",", cells.Select(Cell)));
        Row("IPFMMS Financial Report",model.PeriodLabel);
        Row("Currency","MYR");
        Row("Total income",Money(model.TotalIncome)); Row("Total expenses",Money(model.TotalExpenses));
        Row("Net cash flow before savings",Money(model.NetCashFlow)); Row("Allocated to savings",Money(model.TotalSavings));
        Row("Period remainder after savings (excludes opening balance)",Money(model.AfterSavings));
        Row(""); Row("Month","Income","Expenses","Savings allocations","Net before savings");
        foreach(var m in model.Months) Row(m.Month.ToString("yyyy-MM"),Money(m.Income),Money(m.Expenses),Money(m.Savings),Money(m.Net));
        Row(""); Row("Expense category","Amount","Transactions","Share (%)");
        foreach(var c in model.Categories) Row(c.Name,Money(c.Amount),c.Count.ToString(CultureInfo.InvariantCulture),Money(c.Share));
        Row(""); Row("Budget month","Category","Limit","Spent","Remaining","Usage (%)","Status");
        foreach(var b in model.Budgets) Row(b.Month.ToString("yyyy-MM"),b.Summary.CategoryName,Money(b.Summary.BudgetLimit),Money(b.Summary.AmountSpent),Money(b.Summary.Remaining),b.Summary.Utilization is decimal u ? Money(u) : "N/A",b.Summary.Status);
        Row("Spending in categories/months without a budget",Money(model.UnbudgetedSpending));
        Row("Notes","Saved records only. Savings allocations are separate from expenses. Unrecorded future repeating budgets are excluded.");
        return csv.ToString();
    }
}
