using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels;

public sealed class AdminCategoryIndexViewModel
{
    public string Kind { get; set; } = "Income";
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public int Page { get; set; } = 1;
    public int Count { get; set; }
    public int Pages => Count == 0 ? 1 : (Count - 1) / 20 + 1;
    public List<AdminCategoryRowViewModel> Categories { get; set; } = new();
}

public class AdminCategoryRowViewModel
{
    public string CategoryId { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsProtectedName { get; set; }
    public int UsageCount { get; set; }
    public string EditStamp { get; set; } = "";
    public bool CanDelete => UsageCount == 0 && !IsProtectedName;
}

public sealed class AdminCategoryFormViewModel
{
    public string Kind { get; set; } = "Income";
    public string CategoryId { get; set; } = "";
    public string EditStamp { get; set; } = "";
    [Required, StringLength(100)]
    public string CategoryName { get; set; } = "";
    [StringLength(255)]
    public string? Description { get; set; }
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; } = 1000;
    public bool IsActive { get; set; } = true;
    public bool IsProtectedName { get; set; }
}

public sealed class AdminCategoryDeleteViewModel : AdminCategoryRowViewModel
{
    public string Kind { get; set; } = "Income";
}
