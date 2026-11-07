namespace IPFMMS.Models;
public static class RoutineBillOptions
{
    public static IReadOnlyList<string> Types { get; } = Array.AsReadOnly(new[] { "Bill", "Subscription" });
    public static IReadOnlyList<string> Frequencies { get; } = Array.AsReadOnly(new[] { "OneTime", "Weekly", "Monthly", "Quarterly", "Yearly" });
    public static IReadOnlyList<string> PaymentMethods { get; } = Array.AsReadOnly(new[] { "Cash", "Debit Card", "Credit Card", "E-Wallet", "Bank Transfer", "Other" });
    public static string FrequencyLabel(string value) => value switch { "OneTime" => "One-time", "Quarterly" => "Every 3 months", _ => value };
}
