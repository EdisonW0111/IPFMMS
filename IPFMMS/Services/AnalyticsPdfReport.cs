using System.Globalization;
using System.Security;
using System.Text;
using IPFMMS.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IPFMMS.Services;

public static class AnalyticsPdfReport
{
    private const string Teal = "#16899A";
    private const string Ink = "#193E4D";
    private const string Muted = "#526D7B";
    private const string Border = "#DDE6EB";

    // This final-year academic project qualifies for QuestPDF's Community licence.
    static AnalyticsPdfReport() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(AnalyticsViewModel model)
    {
        var generated = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                Configure(page, model, generated, "Overview");
                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Element(c => Metric(c, "Income", model.TotalIncome));
                        row.RelativeItem().Element(c => Metric(c, "Expenses", model.TotalExpenses));
                    });
                    column.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Element(c => Metric(c, "Net cash flow before savings", model.NetCashFlow));
                        row.RelativeItem().Element(c => Metric(c, "Allocated to savings", model.TotalSavings));
                    });
                    column.Item().Background("#F1F7F9").Padding(10).Text(text =>
                    {
                        text.Span("Period remainder after savings: ").FontColor(Muted);
                        text.Span("RM " + Money(model.AfterSavings)).Bold();
                        text.Span("\nExcludes opening balance. Savings allocations are separate from expenses.").FontSize(9).FontColor(Muted);
                    });
                    column.Item().Text("Income versus expenses").FontSize(13).Bold();
                    column.Item().Text("Teal: income    Coral: expenses    All amounts in MYR").FontSize(9).FontColor(Muted);
                    column.Item().Height(170).Svg(Chart(model.Months, false)).FitArea();
                    column.Item().Text("Monthly spending trend").FontSize(13).Bold();
                    column.Item().Height(135).Svg(Chart(model.Months, true)).FitArea();
                    column.Item().Text("Based on saved financial records for the selected period. Automatic and manual savings contributions are included only when recorded. Unrecorded future repeating budgets are excluded.")
                        .FontSize(9).FontColor(Muted);
                });
            });
            document.Page(page =>
            {
                Configure(page, model, generated, "Monthly and category breakdowns");
                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Monthly figures (MYR)").FontSize(13).Bold();
                    column.Item().Element(c => Table(c,
                        new[] { "Month", "Income", "Expenses", "Savings", "Net before savings" },
                        model.Months.Select(m => new[] { m.Label, Money(m.Income), Money(m.Expenses), Money(m.Savings), Money(m.Net) }),
                        new[] { 1.2f, 1f, 1f, 1f, 1.2f }));
                    column.Item().EnsureSpace(80).Text("Spending by category").FontSize(13).Bold();
                    if (model.Categories.Count == 0)
                        column.Item().Text("No expenses recorded in this period.").FontColor(Muted);
                    else
                        column.Item().Element(c => Table(c,
                            new[] { "Category", "Amount (MYR)", "Transactions", "Share" },
                            model.Categories.Select(c => new[] { c.Name, Money(c.Amount), c.Count.ToString(CultureInfo.InvariantCulture), c.Share.ToString("0.##", CultureInfo.InvariantCulture) + "%" }),
                            new[] { 2.4f, 1.2f, 1f, .8f }));
                });
            });
            document.Page(page =>
            {
                Configure(page, model, generated, "Budget utilization", landscape: true);
                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text($"Total limits: RM {Money(model.TotalBudgetLimit)}    |    Spent in budgeted categories: RM {Money(model.SpendingInBudgetedCategories)}").Bold();
                    column.Item().Text($"Spending without a matching category/month budget: RM {Money(model.UnbudgetedSpending)}.").FontColor(Muted);
                    column.Item().Text("Percentage limits use the income recorded in each budget's month. Usage is N/A when the budget limit is zero. Negative remaining amounts indicate overspending.").FontSize(9).FontColor(Muted);
                    if (model.Budgets.Count == 0)
                        column.Item().Text("No saved budgets in this period.").FontColor(Muted);
                    else
                        column.Item().Element(c => Table(c,
                            new[] { "Month", "Category", "Limit (MYR)", "Spent (MYR)", "Remaining (MYR)", "Usage", "Status" },
                            model.Budgets.Select(b => new[] { b.Month.ToString("MMM yyyy", CultureInfo.InvariantCulture), b.Summary.CategoryName,
                                Money(b.Summary.BudgetLimit), Money(b.Summary.AmountSpent), Money(b.Summary.Remaining),
                                b.Summary.Utilization is decimal usage ? usage.ToString("0.##", CultureInfo.InvariantCulture) + "%" : "N/A", b.Summary.Status }),
                            new[] { 1f, 2f, 1.2f, 1.2f, 1.3f, .8f, 1.2f }));
                });
            });
        }).GeneratePdf();
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static void Configure(PageDescriptor page, AnalyticsViewModel model, DateTimeOffset generated, string section, bool landscape = false)
    {
        page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(10).FontColor(Ink));
        page.Header().BorderBottom(2).BorderColor(Teal).PaddingBottom(10).Column(header =>
        {
            header.Item().Text("IPFMMS  /  Financial report").FontSize(18).Bold().FontColor(Teal);
            header.Item().Text(model.PeriodLabel).FontSize(12).Bold();
            header.Item().PaddingTop(3).Text(section).FontSize(10).FontColor(Muted);
        });
        page.Footer().BorderTop(1).BorderColor(Border).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text($"Generated {generated:dd MMM yyyy, HH:mm} MYT").FontSize(8).FontColor(Muted);
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(t => t.FontSize(8).FontColor(Muted));
                text.Span("Page ");text.CurrentPageNumber();text.Span(" of ");text.TotalPages();
            });
        });
    }

    private static void Metric(IContainer container, string label, decimal amount) => container
        .Background("#F1F7F9").Padding(12).Column(column =>
        {
            column.Item().Text(label).FontSize(9).FontColor(Muted);
            column.Item().PaddingTop(4).Text("RM " + Money(amount)).FontSize(16).Bold();
        });

    private static void Table(IContainer container, string[] headers, IEnumerable<string[]> rows, float[] widths)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => { foreach (var width in widths) columns.RelativeColumn(width); });
            table.Header(header =>
            {
                for (int i = 0; i < headers.Length; i++)
                    header.Cell().Background(Ink).Padding(7).Text(headers[i]).FontColor(Colors.White).FontSize(9).Bold();
            });
            int rowIndex = 0;
            foreach (var row in rows)
            {
                for (int i = 0; i < headers.Length; i++)
                    table.Cell().ShowEntire().Background(rowIndex % 2 == 0 ? "#F1F7F9" : Colors.White).BorderBottom(.5f)
                        .BorderColor(Border).Padding(7).Text(row[i]).FontSize(9);
                rowIndex++;
            }
        });
    }

    private static string Chart(IReadOnlyList<AnalyticsMonth> months, bool trend)
    {
        const double width = 750, height = 220, left = 70, right = 15, top = 15, bottom = 38;
        var max = months.Count == 0 ? 1m : Math.Max(1m, months.Max(m => trend ? m.Expenses : Math.Max(m.Income, m.Expenses)));
        var ceiling = (double)max * 1.1;
        var step = (width-left-right) / Math.Max(1,months.Count);
        double X(int i) => left+step*(i+.5);
        double Y(decimal value) => top+(height-top-bottom)*(1-(double)value/ceiling);
        string N(double n) => n.ToString("0.##",CultureInfo.InvariantCulture);
        var svg=new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        for (int i=0;i<=4;i++)
        {
            var value=ceiling*i/4;var y=top+(height-top-bottom)*(1-value/ceiling);
            svg.Append($"<line x1='{left}' y1='{N(y)}' x2='{width-right}' y2='{N(y)}' stroke='{Border}'/><text x='{left-8}' y='{N(y+4)}' text-anchor='end' font-family='Lato' font-size='10' fill='{Muted}'>{value.ToString("N0",CultureInfo.InvariantCulture)}</text>");
        }
        if(trend&&months.Count>1)
            svg.Append($"<polyline fill='none' stroke='{Teal}' stroke-width='3' points='{string.Join(" ",months.Select((m,i)=>$"{N(X(i))},{N(Y(m.Expenses))}"))}'/>");
        for(int i=0;i<months.Count;i++)
        {
            var m=months[i];
            svg.Append($"<text x='{N(X(i))}' y='{height-12}' text-anchor='middle' font-family='Lato' font-size='10' fill='{Muted}'>{SecurityElement.Escape(m.Label)}</text>");
            if(trend)svg.Append($"<circle cx='{N(X(i))}' cy='{N(Y(m.Expenses))}' r='4' fill='{Teal}'/>");
            else
            {
                double bar=Math.Min(20,step*.3);
                svg.Append($"<rect x='{N(X(i)-bar)}' y='{N(Y(m.Income))}' width='{N(bar-1)}' height='{N(height-bottom-Y(m.Income))}' fill='{Teal}'/>");
                svg.Append($"<rect x='{N(X(i)+1)}' y='{N(Y(m.Expenses))}' width='{N(bar-1)}' height='{N(height-bottom-Y(m.Expenses))}' fill='#DC5666'/>");
            }
        }
        return svg.Append("</svg>").ToString();
    }
}
