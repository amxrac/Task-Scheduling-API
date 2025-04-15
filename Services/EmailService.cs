using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TaskSchedulingApi.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string recipient, string subject, string body);
    }
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _logger = logger;
            _config = config;
        }

        public async Task SendEmailAsync(string recipient, string subject, string body)
        {
            try
            {
                var email = _config.GetValue<string>("EmailConfig:Email");
                var password = _config.GetValue<string>("EmailConfig:Password");
                var host = _config.GetValue<string>("EmailConfig:Host");
                var port = _config.GetValue<int>("EmailConfig:Port");

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(host) || port <= 0)
                {
                    throw new InvalidOperationException("Incomplete Email configuration.");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Task Scheduler", email));
                message.To.Add(new MailboxAddress("", recipient));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();

                client.Timeout = 60000;

                _logger.LogInformation("Connecting to SMTP server {Host}: {Port}", host, port);
                await client.ConnectAsync(host, port, SecureSocketOptions.Auto);

                _logger.LogInformation("Authenticating with SMTP server");
                await client.AuthenticateAsync(email, password);

                _logger.LogInformation("Sending email to recipient {Recipient}", recipient);
                await client.SendAsync(message);

                await client.DisconnectAsync(true);
                _logger.LogInformation("Email sent successfully to {Recipient}", recipient);
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError(ex, "SMTP error sending email to {Recipient}", recipient);
                throw new ApplicationException("Failed to send email", ex);
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError(ex, "SMTP error sending email to {Recipient}", recipient);
                throw new ApplicationException("An unexpected error occurred while sending email", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Recipient}", recipient);
                throw new ApplicationException("Error occured while sending email", ex);
            }
        }


    }
}
