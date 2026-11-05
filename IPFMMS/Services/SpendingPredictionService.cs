using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class SpendingPredictionService(ApplicationDbContext db, IdGeneratorService ids)
{
    public async Task<SpendingPredictionViewModel> BuildAndSaveAsync(string owner, DateTime month, DateTime today, int page = 1, CancellationToken ct = default)
    {
        var current = new DateTime(today.Year, today.Month, 1);
        if (string.IsNullOrWhiteSpace(owner) || month < new DateTime(1000, 4, 1) || month > current || month.Day != 1 || month.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Choose a valid prediction month with three completed preceding months.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var users = await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (users.Count == 0) throw new InvalidOperationException("User no longer exists.");
        var start = month.AddMonths(-3);
        var totals = await db.Expenses.AsNoTracking().Where(e => e.UserId == owner && e.ExpenseDate >= start && e.ExpenseDate < month)
            .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(e => e.Amount), Count = g.Count() }).ToListAsync(ct);
        var model = new SpendingPredictionViewModel { Month = month };
        for (var i = 0; i < 3; i++)
        {
            var date = start.AddMonths(i);
            var total = totals.SingleOrDefault(t => t.Year == date.Year && t.Month == date.Month);
            model.Months.Add(new() { Month = date, Amount = total?.Amount ?? 0m, Count = total?.Count ?? 0, Weight = i == 0 ? .2m : i == 1 ? .3m : .5m });
        }
        SpendingPredictionCalculator.Calculate(model);
        if (month == current)
        {
            var cutoff = today.Date.AddDays(1);
            var partial = await db.Expenses.AsNoTracking()
                .Where(e => e.UserId == owner && e.ExpenseDate >= current && e.ExpenseDate < cutoff)
                .GroupBy(e => 1).Select(g => new { Amount = g.Sum(e => e.Amount), Count = g.Count() })
                .SingleOrDefaultAsync(ct);
            var preview = new SpendingPredictionViewModel { Month = current.AddMonths(1) };
            preview.Months = model.Months.Skip(1).Select(m => new PredictionBaselineMonth
                { Month = m.Month, Amount = m.Amount, Count = m.Count }).ToList();
            preview.Months.Add(new PredictionBaselineMonth { Month = current });
            SpendingPredictionCalculator.CalculateProvisional(preview, today, partial?.Amount ?? 0m, partial?.Count ?? 0);
            model.NextMonthPreview = preview;
            // This estimate is deliberately not persisted as a completed-data prediction.
        }
        var record = await db.SpendingPredictions.SingleOrDefaultAsync(p => p.userId == owner && p.predictionMonth == month, ct);
        if (!model.PredictedAmount.HasValue)
        {
            if (record != null) db.SpendingPredictions.Remove(record);
        }
        else
        {
            if (record == null)
            {
                record = new() { predictionId = await ids.GenerateIdAsync("SPD"), userId = owner, predictionMonth = month };
                db.SpendingPredictions.Add(record);
            }
            if (record.calculatedDate == default || record.predictedAmount != model.PredictedAmount.Value || record.trend != model.Trend)
            {
                record.predictedAmount = model.PredictedAmount.Value;
                record.trend = model.Trend;
                record.calculatedDate = DateTime.UtcNow;
            }
            model.SavedRecord = record;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        model.HistoryCount = await db.SpendingPredictions.CountAsync(p => p.userId == owner, ct);
        model.HistoryPage = Math.Clamp(page, 1, model.HistoryPages);
        model.History = await db.SpendingPredictions.AsNoTracking().Where(p => p.userId == owner).OrderByDescending(p => p.predictionMonth)
            .Skip((model.HistoryPage - 1) * 12).Take(12).ToListAsync(ct);
        return model;
    }
}
