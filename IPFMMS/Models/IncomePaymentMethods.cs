namespace IPFMMS.Models
{
    public static class IncomePaymentMethods
    {
        // Methods by which income can be received.
        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[]
        {
            "Cash", "Bank Transfer", "E-Wallet", "Other"
        });

        public static bool IsValid(string? value)
            => value != null && Values.Contains(value, StringComparer.Ordinal);

        // Unknown historical values may stay unknown. A recorded method cannot
        // be silently cleared, and forged dropdown values are always rejected.
        public static bool CanEdit(string? submitted, string? existing)
            => string.IsNullOrWhiteSpace(submitted)
                ? string.IsNullOrWhiteSpace(existing)
                : IsValid(submitted);
    }
}
