using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    public class SavingsGoal
    {
        [Key]
        [StringLength(20)]
        public string SavingsGoalId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string GoalName { get; set; } = string.Empty;

        // ShortTerm or LongTerm.
        [Required]
        [StringLength(20)]
        public string GoalType { get; set; } = "ShortTerm";

        [Range(typeof(decimal), "0.01", "9999999999.99")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TargetAmount { get; set; }

        [DataType(DataType.Date)]
        public DateTime TargetDate { get; set; }

        // Manual or AutomaticMonthlyTarget. Automatic targets are planning
        // amounts; this setting does not imply an actual transfer of money.
        [Required]
        [StringLength(30)]
        public string SavingsMethod { get; set; } = "Manual";

        // Manual goals do not require a monthly target.
        [Range(typeof(decimal), "0.01", "9999999999.99")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MonthlyTargetAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        [Range(1, 28)]
        public int? AutomaticSavingDay { get; set; }
        [Column(TypeName = "date")]
        public DateTime? NextAutomaticSavingDate { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? AutomaticSavingsBaseline { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        public ICollection<SavingsContribution> SavingsContributions { get; set; }
            = new List<SavingsContribution>();
    }
}
