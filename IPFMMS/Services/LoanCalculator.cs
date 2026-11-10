namespace IPFMMS.Services;

public sealed record LoanQuote(decimal MonthlyPayment, decimal TotalInterest, decimal TotalRepayment);
public sealed record LoanScheduleRow(int Number, decimal OpeningPrincipal, decimal Payment,
    decimal Principal, decimal Interest, decimal ClosingPrincipal);

/// <summary>
/// Planning estimates for equal monthly intervals, with no fees or daily-interest adjustments.
/// Annual reducing-balance lending rates are divided by twelve; this is not an EIR conversion.
/// </summary>
public static class LoanCalculator
{
    public const decimal MaximumAmount = 999999999.99m;

    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static LoanQuote Quote(decimal principal, decimal annualRatePercent, int months,
        string method = "ReducingBalance")
    {
        Validate(principal, annualRatePercent, months);
        if (method == "FlatRate")
        {
            var interest = Round(principal * annualRatePercent / 100m * months / 12m);
            var total = principal + interest;
            var monthly = Math.Max(.01m, Round(total / months));
            EnsurePayment(monthly);
            return new LoanQuote(monthly, interest, total);
        }
        if (method != "ReducingBalance")
            throw new InvalidOperationException("Choose a valid interest calculation method.");

        var payment = MonthlyPayment(principal, annualRatePercent, months);
        var schedule = BuildSchedule(principal, annualRatePercent, months, payment);
        return new LoanQuote(payment, schedule.Sum(row => row.Interest), schedule.Sum(row => row.Payment));
    }

    public static IReadOnlyList<LoanScheduleRow> Schedule(decimal principal, decimal annualRatePercent, int months)
    {
        Validate(principal, annualRatePercent, months);
        return BuildSchedule(principal, annualRatePercent, months,
            MonthlyPayment(principal, annualRatePercent, months));
    }

    public static decimal Interest(decimal principal, decimal annualRatePercent)
    {
        if (principal < 0m || principal > MaximumAmount || principal != Round(principal))
            throw new InvalidOperationException("Principal must be a non-negative amount with at most two decimal places.");
        ValidateRate(annualRatePercent);
        return Round(principal * annualRatePercent / 1200m);
    }

    public static decimal Rate(decimal reference, decimal spread)
    {
        ValidateRate(reference);
        if (spread < -100m || spread > 100m || spread != decimal.Round(spread, 4))
            throw new InvalidOperationException("Enter a spread from -100 to 100 percentage points with at most four decimal places.");
        var rate = reference + spread;
        ValidateRate(rate);
        return rate;
    }

    private static decimal MonthlyPayment(decimal principal, decimal annualRatePercent, int months)
    {
        decimal payment;
        if (annualRatePercent == 0m)
            payment = principal / months;
        else
        {
            // Stable annuity form: P*r / (1 - (1+r)^-n).
            var monthlyRate = annualRatePercent / 1200m;
            var discount = 1m / Power(1m + monthlyRate, months);
            payment = principal * monthlyRate / (1m - discount);
        }
        payment = Math.Max(.01m, Round(payment));
        EnsurePayment(payment);
        return payment;
    }

    private static IReadOnlyList<LoanScheduleRow> BuildSchedule(decimal principal,
        decimal annualRatePercent, int months, decimal monthlyPayment)
    {
        var rows = new List<LoanScheduleRow>(months);
        var balance = principal;
        for (var month = 1; month <= months && balance > 0m; month++)
        {
            var interest = Interest(balance, annualRatePercent);
            var payment = month == months ? balance + interest : Math.Min(monthlyPayment, balance + interest);
            // The declared term must amortize the principal; cents rounding is settled in the last payment.
            if (payment < interest)
                throw new InvalidOperationException("The calculated payment does not cover monthly interest.");
            EnsurePayment(payment);
            var principalPaid = payment - interest;
            var closing = balance - principalPaid;
            rows.Add(new LoanScheduleRow(month, balance, payment, principalPaid, interest, closing));
            balance = closing;
        }
        return rows.AsReadOnly();
    }

    private static decimal Power(decimal value, int exponent)
    {
        var result = 1m;
        while (exponent > 0)
        {
            if ((exponent & 1) != 0) result *= value;
            exponent >>= 1;
            if (exponent > 0) value *= value;
        }
        return result;
    }

    private static void Validate(decimal principal, decimal annualRatePercent, int months)
    {
        if (principal < .01m || principal > MaximumAmount || principal != Round(principal))
            throw new InvalidOperationException("Loan principal must be from RM 0.01 to RM 999,999,999.99 with at most two decimal places.");
        ValidateRate(annualRatePercent);
        if (months < 1 || months > 480)
            throw new InvalidOperationException("Choose a loan term from 1 to 480 months.");
    }

    private static void ValidateRate(decimal rate)
    {
        if (rate < 0m || rate > 100m || rate != decimal.Round(rate, 4))
            throw new InvalidOperationException("Annual interest rate must be from 0 to 100 percent with at most four decimal places.");
    }

    private static void EnsurePayment(decimal payment)
    {
        if (payment < .01m || payment > MaximumAmount)
            throw new InvalidOperationException("The calculated instalment is outside the supported amount range. Adjust the loan amount or term.");
    }
}
