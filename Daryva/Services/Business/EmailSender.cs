using System.Net;
using System.Net.Mail;
using Daryva.Services;

namespace Daryva.Services.Business
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfigurationService? _configurationService;

        public EmailSender(IConfigurationService? configurationService = null)
        {
            _configurationService = configurationService;
        }

        private void GetSmtpSettings(out string? smtpServer, out int smtpPort, out string? smtpUsername, out string? smtpPassword, out bool enableSsl, out string? fromAddress)
        {
            smtpServer = _configurationService?.GetValue("SmtpServer");
            var portStr = _configurationService?.GetValue("SmtpPort");
            smtpPort = int.TryParse(portStr, out var port) ? port : 587;
            smtpUsername = _configurationService?.GetValue("SmtpUsername");
            smtpPassword = _configurationService?.GetValue("SmtpPassword");
            var sslStr = _configurationService?.GetValue("SmtpEnableSsl");
            enableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
            fromAddress = _configurationService?.GetValue("SmtpFromAddress");
        }

        public async Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null)
        {
            try
            {
                // Reload config to get latest settings
                _configurationService?.ReloadLocalConfig();
                
                // Get SMTP settings dynamically
                GetSmtpSettings(out var smtpServer, out var smtpPort, out var smtpUsername, out var smtpPassword, out var enableSsl, out var fromAddr);

                // If SMTP is not configured, show a helpful message
                if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
                {
                    throw new InvalidOperationException(
                        "SMTP is not configured. Please add the following settings to App.config.local:\n\n" +
                        "<add key=\"SmtpServer\" value=\"smtp.gmail.com\" />\n" +
                        "<add key=\"SmtpPort\" value=\"587\" />\n" +
                        "<add key=\"SmtpUsername\" value=\"your-email@gmail.com\" />\n" +
                        "<add key=\"SmtpPassword\" value=\"your-app-password\" />\n" +
                        "<add key=\"SmtpEnableSsl\" value=\"true\" />\n" +
                        "<add key=\"SmtpFromAddress\" value=\"your-email@gmail.com\" />\n\n" +
                        "For Gmail, you need to use an App Password (not your regular password).");
                }

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                var from = fromAddress ?? fromAddr ?? smtpUsername ?? "noreply@landlordbuddy.com";
                
                using var message = new MailMessage(from, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                // Re-throw with more context for user feedback
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }
    }
}
