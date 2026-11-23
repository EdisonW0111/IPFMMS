using IPFMMS.Models;
namespace IPFMMS.Services;
public static class SharedShareCalculator
{
    public static Dictionary<string, decimal> Calculate(decimal target, IEnumerable<SharedFinanceMember> members)
    {
        var rows = members.Where(m => m.invitationStatus == "Accepted").OrderBy(m => m.userId, StringComparer.Ordinal).ToArray();
        if (target < 0m || rows.Length == 0 || rows.Any(m => m.sharePercentage is null or < 0m or > 100m) || rows.Sum(m => m.sharePercentage!.Value) != 100m) return new();
        var cents = decimal.Round(target * 100m, 0, MidpointRounding.AwayFromZero);
        var raw = rows.ToDictionary(m => m.userId, m => cents * m.sharePercentage!.Value / 100m);
        var result = raw.ToDictionary(p => p.Key, p => decimal.Floor(p.Value));
        var leftover = (int)(cents - result.Values.Sum());
        foreach (var item in raw.OrderByDescending(p => p.Value - decimal.Floor(p.Value)).ThenBy(p => p.Key, StringComparer.Ordinal).Take(leftover)) result[item.Key]++;
        return result.ToDictionary(p => p.Key, p => p.Value / 100m);
    }
}
