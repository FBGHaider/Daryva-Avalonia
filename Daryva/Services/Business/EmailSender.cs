using System.Net;
using System.Net.Mail;
using Daryva.Services;

namespace Daryva.Services.Business
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfigurationService? _configurationService;
        private readonly string? _smtpServer;
        private readonly int _smtpPort;
        private readonly string? _smtpUsername;
        private readonly string? _smtpPassword;
        private readonly bool _enableSsl;
        private readonly string? _fromAddress;

        public EmailSender(IConfigurationService? configurationService = null)
        {
            _configurationService = configurationService;
            
            // Load SMTP settings from configuration
            _smtpServer = _configurationService?.GetValue("SmtpServer");
            var portStr = _configurationService?.GetValue("SmtpPort");
            _smtpPort = int.TryParse(portStr, out var port) ? port : 587;
            _smtpUsername = _configurationService?.GetValue("SmtpUsername");
            _smtpPassword = _configurationService?.GetValue("SmtpPassword");
            var sslStr = _configurationService?.GetValue("SmtpEnableSsl");
            _enableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
            _fromAddress = _configurationService?.GetValue("SmtpFromAddress");
        }

        public async Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null)
        {
            try
            {
                // If SMTP is not configured, show a helpful message
                if (string.IsNullOrWhiteSpace(_smtpServer) || string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
                {
                    throw new InvalidOperationException(
                        "SMTP is not configured. Please add the following settings to App.config:\n\n" +
                        "<add key=\"SmtpServer\" value=\"smtp.gmail.com\" />\n" +
                        "<add key=\"SmtpPort\" value=\"587\" />\n" +
                        "<add key=\"SmtpUsername\" value=\"your-email@gmail.com\" />\n" +
                        "<add key=\"SmtpPassword\" value=\"your-app-password\" />\n" +
                        "<add key=\"SmtpEnableSsl\" value=\"true\" />\n" +
                        "<add key=\"SmtpFromAddress\" value=\"your-email@gmail.com\" />\n\n" +
                        "For Gmail, you need to use an App Password (not your regular password).");
                }

                using var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = _enableSsl,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                var from = fromAddress ?? _fromAddress ?? _smtpUsername ?? "noreply@landlordbuddy.com";
                
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
