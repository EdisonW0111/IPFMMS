using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels;

public sealed class SettingsViewModel
{
    [Required, RegularExpression("^(Light|Dark)$", ErrorMessage = "Choose Light or Dark mode.")]
    public string Theme { get; set; } = "Light";
    [Range(0, int.MaxValue)] public int Revision { get; set; }
    public bool FinancialInApp { get; set; } = true;
    public bool FinancialEmail { get; set; }
    public bool BillInApp { get; set; } = true;
    public bool BillEmail { get; set; } = true;
    public bool MonitoringInApp { get; set; } = true;
    public bool MonitoringEmail { get; set; } = true;
    public bool MonitoringDisconnectInApp { get; set; } = true;
    public bool MonitoringDisconnectEmail { get; set; } = true;
    public bool InvitationEmail { get; set; } = true;
}
