using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

// Personal cash reserved in shared savings, independent of current group/member visibility.
// Group archival or membership removal must not silently release a historical allocation.
public static class SharedSavingsAllocations
{
    public static IQueryable<SharedFinanceTransaction> Query(ApplicationDbContext db,string userId) =>
        db.SharedFinanceTransactions.AsNoTracking().Where(t=>t.userId==userId &&
            (t.transactionType=="Contribution"||t.transactionType=="Withdrawal"));

    public static async Task<decimal> NetAsync(ApplicationDbContext db,string userId,
        DateTime? start=null,DateTime? end=null,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(userId))throw new ArgumentException("An owner is required.");
        var query=Query(db,userId);
        if(start.HasValue)query=query.Where(t=>t.transactionDate>=start.Value);
        if(end.HasValue)query=query.Where(t=>t.transactionDate<end.Value);
        return await query.SumAsync(t=>(decimal?)(t.transactionType=="Withdrawal"?-t.amount:t.amount),ct)??0m;
    }
}
