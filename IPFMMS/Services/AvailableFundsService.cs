using IPFMMS.Data;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public static class AvailableFundsService
{
    // Call inside the owner's locked transaction before allocating funds.
    // Debt repayments already appear in Expenses and must not be counted twice.
    public static async Task<decimal> BalanceAsync(ApplicationDbContext db, string owner,
        DateTime asOf, string? excludeContributionId = null, CancellationToken ct = default)
    {
        var income = await db.Incomes.Where(i => i.UserId == owner && i.IncomeDate <= asOf.Date)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var expenses = await db.Expenses.Where(e => e.UserId == owner && e.ExpenseDate <= asOf.Date)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var savings = await db.SavingsContributions.Where(c => c.SavingsGoal!.UserId == owner &&
            (excludeContributionId == null || c.ContributionId != excludeContributionId))
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        return income - expenses - savings - await SharedSavingsAllocations.NetAsync(db, owner, ct: ct);
    }
}
