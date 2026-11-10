using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Figure 4.24 names are preserved exactly.
public class Debt
{
    [Key,StringLength(20)] public string debtId { get; set; } = "";
    [Required,StringLength(20)] public string userId { get; set; } = "";
    [Required,StringLength(100)] public string debtName { get; set; } = "";
    [Required,StringLength(40)] public string debtType { get; set; } = "";
    [Column(TypeName="decimal(12,2)")] public decimal originalAmount { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal outstandingAmount { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal monthlyRepaymentAmount { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal interestRate { get; set; }
    [Column(TypeName="date")] public DateTime dueDate { get; set; }
    [StringLength(500)] public string? description { get; set; }
    [Required,StringLength(20)] public string status { get; set; } = "Active";
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    // Opening balance distinguishes repayments made before tracking began.
    [Column(TypeName="decimal(12,2)")] public decimal openingOutstandingAmount { get; set; }
    public int revision { get; set; } = 1;
    [Required,StringLength(20)] public string calculationMode { get; set; } = "Manual";
    [Required,StringLength(20)] public string rateType { get; set; } = "Fixed";
    public int? loanTermMonths { get; set; }
    public int loanDueDay { get; set; } = 1;
    public int completedInstallments { get; set; }
    public bool isExistingLoan { get; set; }
    [Column(TypeName="date")] public DateTime? loanStartDate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal? referenceRate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal? rateSpread { get; set; }
    [StringLength(10)] public string? referenceRateName { get; set; }
    [ForeignKey(nameof(userId))] public User? User { get; set; }
}
