using System.Data;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services
{
    public class BudgetRolloverService
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;

        public BudgetRolloverService(ApplicationDbContext context, IdGeneratorService idGenerator)
        {
            _context = context;
            _idGenerator = idGenerator;
        }

        public async Task<List<string>> EnsureMonthAsync(string userId, DateTime month, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || month.Year < 1000 || month.Year > 9998 || month.Day != 1 || month.TimeOfDay != TimeSpan.Zero)
                throw new ArgumentException("An owner and a valid first day of the month are required.");
            ct.ThrowIfCancellationRequested();
            var latestMonth = BudgetRolloverPolicy.LatestMonth;
            if (month > latestMonth)
                return new List<string> { BudgetRolloverPolicy.LimitMessage(latestMonth) };

            // Summary/health calls can already be inside the consented owner-lock transaction.
            // Reuse it rather than opening a nested transaction or committing the caller's work.
            if (_context.Database.CurrentTransaction != null)
            {
                if (!await LockEligibleOwnerAsync(userId, ct)) return new List<string>();
                return await EnsureReadyThroughMonthInTransactionAsync(userId, month, ct);
            }
            // Most reads need no work. This preflight is outside a transaction so it cannot
            // invert the account-before-schedule lock order used by edits and generation.
            if (!await _context.BudgetSchedules.AsNoTracking().AnyAsync(s => s.UserId == userId && s.IsActive
                && s.NextBudgetMonth <= month && (!s.EndMonth.HasValue || s.NextBudgetMonth <= s.EndMonth.Value), ct))
                return new List<string>();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            if (!await LockEligibleOwnerAsync(userId, ct)) return new List<string>();
            var warnings = await EnsureReadyThroughMonthInTransactionAsync(userId, month, ct);
            await transaction.CommitAsync(ct);
            return warnings;
        }

        private async Task<bool> LockEligibleOwnerAsync(string userId, CancellationToken ct)
        {
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            return owners.Any(u => u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent"));
        }

        // Finish successful history chunks before a summary reads the selected month.
        // A conflict cannot be skipped: stop if no schedule cursor advances and retain its warning.
        // The planner still bounds each chunk and the transaction/cancellation protects partial work.
        public async Task<List<string>> EnsureReadyThroughMonthInTransactionAsync(string userId, DateTime month, CancellationToken ct = default)
        {
            if (_context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Budget history requires an owner-locked transaction.");
            if (month > BudgetRolloverPolicy.LatestMonth)
                return new List<string> { BudgetRolloverPolicy.LimitMessage(BudgetRolloverPolicy.LatestMonth) };
            var warnings = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var before = await _context.BudgetSchedules.AsNoTracking()
                    .Where(s => s.UserId == userId && s.IsActive && s.NextBudgetMonth <= month
                        && (!s.EndMonth.HasValue || s.NextBudgetMonth <= s.EndMonth.Value))
                    .OrderBy(s => s.BudgetScheduleId).Select(s => new { s.BudgetScheduleId, s.NextBudgetMonth }).ToListAsync(ct);
                if (before.Count == 0) break;
                var batch = await EnsureThroughMonthInTransactionAsync(userId, month, ct: ct);
                foreach (var warning in batch.Where(w => !w.StartsWith("Budget catch-up is limited to", StringComparison.Ordinal)))
                    warnings.Add(warning);
                var after = await _context.BudgetSchedules.AsNoTracking()
                    .Where(s => s.UserId == userId && s.IsActive && s.NextBudgetMonth <= month
                        && (!s.EndMonth.HasValue || s.NextBudgetMonth <= s.EndMonth.Value))
                    .OrderBy(s => s.BudgetScheduleId).Select(s => new { s.BudgetScheduleId, s.NextBudgetMonth }).ToListAsync(ct);
                if (before.SequenceEqual(after))
                {
                    foreach (var warning in batch) warnings.Add(warning);
                    break;
                }
            }
            return warnings.ToList();
        }

        // Caller must hold the owner lock. Editing/stopping a schedule uses this
        // same transaction to preserve earlier months with the OLD settings.
        public async Task<List<string>> EnsureThroughMonthInTransactionAsync(
            string userId, DateTime month, string? categoryId = null, CancellationToken ct = default)
        {
            // Also protect calls made by Edit/Delete to preserve older history.
            var latestMonth = BudgetRolloverPolicy.LatestMonth;
            if (month > latestMonth)
                return new List<string> { BudgetRolloverPolicy.LimitMessage(latestMonth) };

            if (_context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Budget history requires an owner-locked transaction.");

            var schedules = await _context.BudgetSchedules.Include(s => s.ExpenseCategory)
                .Where(s => s.UserId == userId && s.IsActive && s.NextBudgetMonth <= month &&
                    (!s.EndMonth.HasValue || s.NextBudgetMonth <= s.EndMonth.Value) &&
                    (categoryId == null || s.ExpenseCategoryId == categoryId))
                .ToListAsync(ct);
            if (schedules.Count == 0) return new List<string>();

            var firstMonth = schedules.Min(s => s.NextBudgetMonth);
            var existing = await _context.Budgets.AsNoTracking()
                .Where(b => b.UserId == userId && b.BudgetMonth >= firstMonth && b.BudgetMonth <= month)
                .ToListAsync(ct);
            var ids = schedules.Select(s => s.BudgetScheduleId).ToList();
            var skips = await _context.BudgetRepeatSkips.AsNoTracking()
                .Where(s => ids.Contains(s.BudgetScheduleId) && s.BudgetMonth >= firstMonth && s.BudgetMonth <= month).ToListAsync(ct);
            var history = BudgetRolloverHistoryPlanner.Build(userId, month, schedules, existing, skips);

            foreach (var budget in history.Budgets)
            {
                budget.BudgetId = await _idGenerator.GenerateIdAsync("BDG");
                _context.Budgets.Add(budget);
            }
            foreach (var advance in history.NextMonths)
                advance.Key.NextBudgetMonth = advance.Value;

            await _context.SaveChangesAsync(ct);
            return history.Warnings;
        }
    }
}
