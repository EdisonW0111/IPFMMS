using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class IdCounter
    {
        [Key]
        [StringLength(10)]
        public string Prefix { get; set; } = string.Empty;

        public long LastNumber { get; set; }
    }
}