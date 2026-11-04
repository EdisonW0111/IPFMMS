using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// A deleted month must not be regenerated when a repeat cursor is rewound.
public class BudgetRepeatSkip
{
    [StringLength(20)] public string BudgetScheduleId { get; set; } = "";
    public int SeriesVersion { get; set; }
    [Column(TypeName="date")] public DateTime BudgetMonth { get; set; }
    public BudgetSchedule? BudgetSchedule { get; set; }
}
