using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IPFMMS.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string body)
        {
            var host = _configuration["SmtpSettings:Host"];
            var portString = _configuration["SmtpSettings:Port"];
            var senderEmail = _configuration["SmtpSettings:SenderEmail"];
            var senderName = _configuration["SmtpSettings:SenderName"];
            var username = _configuration["SmtpSettings:Username"];
            var password = _configuration["SmtpSettings:Password"];

            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portString, out int port) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "SMTP configuration is missing or invalid.");
            }

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                senderName ?? "IPFMMS",
                senderEmail));

            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = subject;

            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();

            await client.ConnectAsync(
                host,
                port,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(username, password);

            await client.SendAsync(message);

            await client.DisconnectAsync(true);
        }
    }
}