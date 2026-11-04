using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace IPFMMS.ViewModels
{
    public class EditSavingsContributionViewModel : RecordSavingsContributionViewModel
    {
        [Required, StringLength(20)]
        public string ContributionId { get; set; } = string.Empty;
        [BindNever]
        public decimal OtherContributions { get; set; }
        public decimal MaximumAmount => Math.Max(0m, TargetAmount - OtherContributions);
    }
}
