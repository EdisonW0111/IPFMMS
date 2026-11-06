namespace IPFMMS.Services;
public interface IParentLinkEmailSender
{
    Task SendRequestAsync(string recipientEmail, string requesterName);
}
public sealed class ParentLinkEmailSender(EmailService email) : IParentLinkEmailSender
{
    public Task SendRequestAsync(string recipientEmail, string requesterName) => email.SendEmailAsync(recipientEmail,
        "IPFMMS - Financial monitoring request",
        $"{requesterName} has requested permission to monitor your financial summaries in IPFMMS.\n\n" +
        "Sign in to your existing IPFMMS account, open Parental Monitoring from the sidebar, then review Requests received.\n\n" +
        "You can accept or reject the request. No monitoring access is granted unless you accept while signed in. " +
        "You can disconnect later. If you do not recognize this person, reject the request.\n\nIPFMMS Team");
}
