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

        public async Task<List<string>> EnsureMonthAsync(string userId, DateTime month)
        {
            var latestMonth = BudgetRolloverPolicy.LatestMonth;
            if (month > latestMonth)
                return new List<string> { BudgetRolloverPolicy.LimitMessage(latestMonth) };

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync();
            if (owners.Count == 0)
                return new List<string> { "Your account could not be found. Please sign in again." };

            var warnings = await EnsureThroughMonthInTransactionAsync(userId, month);
            await transaction.CommitAsync();
            return warnings;
        }

        // Caller must hold the owner lock. Editing/stopping a schedule uses this
        // same transaction to preserve earlier months with the OLD settings.
        public async Task<List<string>> EnsureThroughMonthInTransactionAsync(
            string userId, DateTime month, string? categoryId = null)
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
                .ToListAsync();
            if (schedules.Count == 0) return new List<string>();

            var firstMonth = schedules.Min(s => s.NextBudgetMonth);
            var existing = await _context.Budgets.AsNoTracking()
                .Where(b => b.UserId == userId && b.BudgetMonth >= firstMonth && b.BudgetMonth <= month)
                .ToListAsync();
            var ids = schedules.Select(s => s.BudgetScheduleId).ToList();
            var skips = await _context.BudgetRepeatSkips.AsNoTracking()
                .Where(s => ids.Contains(s.BudgetScheduleId) && s.BudgetMonth >= firstMonth && s.BudgetMonth <= month).ToListAsync();
            var history = BudgetRolloverHistoryPlanner.Build(userId, month, schedules, existing, skips);

            foreach (var budget in history.Budgets)
            {
                budget.BudgetId = await _idGenerator.GenerateIdAsync("BDG");
                _context.Budgets.Add(budget);
            }
            foreach (var advance in history.NextMonths)
                advance.Key.NextBudgetMonth = advance.Value;

            await _context.SaveChangesAsync();
            return history.Warnings;
        }
    }
}
