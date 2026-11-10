using System.ComponentModel.DataAnnotations;
using IPFMMS.Models;
namespace IPFMMS.ViewModels;
public sealed class DebtFormViewModel
{
    public string? DebtId { get; set; }
    public int Revision { get; set; }
    [Required,StringLength(100),Display(Name="Debt name")] public string DebtName { get; set; } = "";
    [Required,StringLength(40),Display(Name="Debt type")] public string DebtType { get; set; } = "Other";
    [Required,Range(typeof(decimal),"0.01","999999999.99"),Display(Name="Original amount (RM)")] public decimal? OriginalAmount { get; set; }
    [Required,Range(typeof(decimal),"0","999999999.99"),Display(Name="Outstanding amount (RM)")] public decimal? OutstandingAmount { get; set; }
    [Required,Range(typeof(decimal),"0.01","999999999.99"),Display(Name="Monthly repayment amount (RM)")] public decimal? MonthlyRepaymentAmount { get; set; }
    [Required,Range(typeof(decimal),"0","100"),Display(Name="Annual interest rate (%)")] public decimal? InterestRate { get; set; } = 0;
    [Required,DataType(DataType.Date),Display(Name="Next payment due date")] public DateTime? DueDate { get; set; } = DebtOptions.Today;
    [StringLength(500)] public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public bool HasRepayments { get; set; }
    public string CalculationMode { get; set; } = "Manual";
    public string RateType { get; set; } = "Fixed";
    public bool IsExistingLoan { get; set; }
    [Range(1,480),Display(Name="Loan term / remaining term (months)")] public int? LoanTermMonths { get; set; }
    [DataType(DataType.Date),Display(Name="Loan / tracking start date")] public DateTime? LoanStartDate { get; set; } = DebtOptions.Today;
    [Display(Name="Reference rate (% p.a.)")] public decimal? ReferenceRate { get; set; }
    [Display(Name="Spread (% p.a.)")] public decimal? RateSpread { get; set; }
    public string ReferenceRateName { get; set; } = "SBR";

}
public sealed class DebtRepayViewModel
{
    [Required,StringLength(20)] public string DebtId { get; set; } = "";
    public int Revision { get; set; }
    [Required,Range(typeof(decimal),"0.01","999999999.99"),Display(Name="Repayment amount (RM)")] public decimal? Amount { get; set; }
    [Required,DataType(DataType.Date),Display(Name="Payment date")] public DateTime? PaymentDate { get; set; } = DebtOptions.Today;
    [Required,StringLength(30),Display(Name="Payment method")] public string PaymentMethod { get; set; } = "Cash";
    [StringLength(500)] public string? Remark { get; set; }
    [DataType(DataType.Date),Display(Name="Next payment due date (optional)")] public DateTime? NextDueDate { get; set; }
    public Debt? Debt { get; set; }
    public decimal EstimatedInterestDue { get; set; }
    public decimal MaximumPayment { get; set; }

}
public sealed class DebtIndexViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "all";
    public string? Type { get; set; }
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages=>Math.Max(1,(Count+19)/20);
    public decimal Outstanding { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal MonthlyRepayment { get; set; }
    public int ActiveCount { get; set; }
    public DateTime? UpcomingDate { get; set; }
    public List<Debt> Debts { get; set; } = new();
}
public sealed class DebtDetailsViewModel
{
    public string? ExpenseId { get; set; }
    public Debt Debt { get; set; } = new();
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages=>Math.Max(1,(Count+19)/20);
    public string? Month { get; set; }
    public decimal RecordedPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public List<IPFMMS.Services.LoanScheduleRow> Schedule { get; set; } = new();
    public List<DebtRateChange> RateChanges { get; set; } = new();

    public List<DebtRepayment> Repayments { get; set; } = new();
}

public sealed class LoanCalculatorViewModel
{
    public decimal Principal { get; set; } = 20000;
    public decimal AnnualRate { get; set; } = 2.98m;
    public int Months { get; set; } = 60;
    public string Method { get; set; } = "ReducingBalance";
    public string RateType { get; set; } = "Fixed";
    public decimal ReferenceRate { get; set; }
    public decimal Spread { get; set; }
    public IPFMMS.Services.LoanQuote? Quote { get; set; }
    public List<IPFMMS.Services.LoanScheduleRow> Schedule { get; set; } = new();
}
public sealed class DebtRateViewModel
{
    public string DebtId { get; set; } = "";
    public int Revision { get; set; }
    public decimal ReferenceRate { get; set; }
    public decimal Spread { get; set; }
    public Debt? Debt { get; set; }
}
