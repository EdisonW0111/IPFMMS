using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    public class SavingsContribution
    {
        [Key]
        [StringLength(20)]
        public string ContributionId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string SavingsGoalId { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "9999999999.99")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime ContributionDate { get; set; }

        [Required]
        [StringLength(30)]
        public string ContributionMethod { get; set; } = "Manual";

        [StringLength(255)]
        public string? Description { get; set; }

        // Ownership is checked through SavingsGoal.UserId; no extra owner
        // field is needed on the contribution in the Chapter 4 design.
        [ForeignKey(nameof(SavingsGoalId))]
        public SavingsGoal? SavingsGoal { get; set; }

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string contributionId { get => ContributionId; set => ContributionId = value; }
        [NotMapped, JsonIgnore]
        public string savingsGoalId { get => SavingsGoalId; set => SavingsGoalId = value; }
        [NotMapped, JsonIgnore]
        public decimal amount { get => Amount; set => Amount = value; }
        [NotMapped, JsonIgnore]
        public DateTime contributionDate { get => ContributionDate; set => ContributionDate = value; }
        [NotMapped, JsonIgnore]
        public string contributionMethod { get => ContributionMethod; set => ContributionMethod = value; }
        [NotMapped, JsonIgnore]
        public string? description { get => Description; set => Description = value; }
    }
}
