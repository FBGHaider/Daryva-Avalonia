using System.Net;
using System.Net.Mail;

namespace Daryva.Api.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private void GetSmtpSettings(out string? smtpServer, out int smtpPort, out string? smtpUsername, out string? smtpPassword, out bool enableSsl, out string? fromAddress)
    {
        smtpServer = _configuration["Smtp:Server"] ?? _configuration["SmtpServer"];
        var portStr = _configuration["Smtp:Port"] ?? _configuration["SmtpPort"];
        smtpPort = int.TryParse(portStr, out var port) ? port : 587;
        smtpUsername = _configuration["Smtp:Username"] ?? _configuration["SmtpUsername"];
        smtpPassword = _configuration["Smtp:Password"] ?? _configuration["SmtpPassword"];
        var sslStr = _configuration["Smtp:EnableSsl"] ?? _configuration["SmtpEnableSsl"];
        enableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
        fromAddress = _configuration["Smtp:FromAddress"] ?? _configuration["SmtpFromAddress"];
    }

    public async Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null)
    {
        string? smtpServer = null;
        try
        {
            GetSmtpSettings(out smtpServer, out var smtpPort, out var smtpUsername, out var smtpPassword, out var enableSsl, out var fromAddr);

            if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
            {
                throw new InvalidOperationException(
                    "SMTP is not configured. Set Smtp:Server, Smtp:Port, Smtp:Username, Smtp:Password, Smtp:EnableSsl, Smtp:FromAddress in appsettings.json.");
            }

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
                msg += "\n\nGmail requires an App Password, not your normal password. Turn on 2-Step Verification, then create an App Password at https://myaccount.google.com/apppasswords and use that in Smtp:Password.";
            throw new InvalidOperationException($"Failed to send email: {msg}", ex);
        }
    }
}
