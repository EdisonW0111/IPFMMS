namespace IPFMMS.Services;
public interface IAlertEmailSender
{
    Task SendAsync(string recipientEmail, string subject, string message);
}
public sealed class AlertEmailSender(EmailService email) : IAlertEmailSender
{
    public Task SendAsync(string recipientEmail, string subject, string message) =>
        email.SendEmailAsync(recipientEmail, "IPFMMS - " + subject,
            message + "\n\nSign in to your IPFMMS account and open Notifications to view the latest financial summary. " +
            "This email records an alert at the time it was detected; the latest figures may have changed.\n\nIPFMMS Team");
}
