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
            smtpServer = _configurationService?.GetValue("SmtpServer")?.Trim();
            var portStr = _configurationService?.GetValue("SmtpPort");
            smtpPort = int.TryParse(portStr, out var port) ? port : 587;
            smtpUsername = _configurationService?.GetValue("SmtpUsername")?.Trim();
            smtpPassword = _configurationService?.GetValue("SmtpPassword")?.Trim();
            var sslStr = _configurationService?.GetValue("SmtpEnableSsl");
            enableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
            fromAddress = _configurationService?.GetValue("SmtpFromAddress")?.Trim();
        }

        public async Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null)
        {
            string? smtpServer = null;
            try
            {
                // Reload config to get latest settings
                _configurationService?.ReloadLocalConfig();
                
                // Get SMTP settings dynamically
                GetSmtpSettings(out smtpServer, out var smtpPort, out var smtpUsername, out var smtpPassword, out var enableSsl, out var fromAddr);

                // If SMTP is not configured, show a helpful message
                if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
                {
                    var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.config.local");
                    throw new InvalidOperationException(
                        "SMTP is not configured. The app reads App.config.local from the output folder.\n\n" +
                        "Expected path: " + configPath + "\n\n" +
                        "Ensure that file exists and contains <appSettings> entries for SmtpServer, SmtpPort, SmtpUsername, SmtpPassword, SmtpEnableSsl, SmtpFromAddress. " +
                        "If you edit App.config.local in the project folder, rebuild the app so it is copied to output. " +
                        "You can also use Settings → Email / Integrations → Save to write config to the output folder.");
                }

                // Gmail and many providers require TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                var from = fromAddress ?? fromAddr ?? smtpUsername ?? "noreply@daryva.local";
                
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
                var msg = ex.Message;
                var isGmail = smtpServer?.Contains("gmail", StringComparison.OrdinalIgnoreCase) == true;
                var authRelated = msg.Contains("Authentication", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("5.7.0", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("secure connection", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("not authenticated", StringComparison.OrdinalIgnoreCase);
                if (isGmail && authRelated)
                    msg += "\n\nGmail requires an App Password, not your normal password. Turn on 2-Step Verification, then create an App Password at https://myaccount.google.com/apppasswords and use that in SmtpPassword.";
                throw new InvalidOperationException($"Failed to send email: {msg}", ex);
            }
        }
    }
}
