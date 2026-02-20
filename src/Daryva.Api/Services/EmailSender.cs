using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace Daryva.Api.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string> _localAppSettings;

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
        _localAppSettings = LoadLocalAppSettings();
    }

    private static Dictionary<string, string> LoadLocalAppSettings()
    {
        try
        {
            var localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Daryva",
                "app.config.local.json");

            if (!File.Exists(localPath))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(localPath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("AppSettings", out var appSettingsElement) ||
                appSettingsElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in appSettingsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var text = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        values[prop.Name] = text;
                }
            }

            return values;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private string? GetConfigValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (_localAppSettings.TryGetValue(key, out var exactLocalValue) && !string.IsNullOrWhiteSpace(exactLocalValue))
                return exactLocalValue;

            var compactKey = key.Replace(":", string.Empty, StringComparison.Ordinal);
            if (_localAppSettings.TryGetValue(compactKey, out var compactLocalValue) && !string.IsNullOrWhiteSpace(compactLocalValue))
                return compactLocalValue;

            var appSettingsKey = key.StartsWith("AppSettings:", StringComparison.OrdinalIgnoreCase)
                ? key["AppSettings:".Length..]
                : key;
            if (_localAppSettings.TryGetValue(appSettingsKey, out var appSettingsLocalValue) && !string.IsNullOrWhiteSpace(appSettingsLocalValue))
                return appSettingsLocalValue;

            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private void GetSmtpSettings(out string? smtpServer, out int smtpPort, out string? smtpUsername, out string? smtpPassword, out bool enableSsl, out string? fromAddress)
    {
        smtpServer = GetConfigValue("Smtp:Server", "SmtpServer", "AppSettings:SmtpServer");
        var portStr = GetConfigValue("Smtp:Port", "SmtpPort", "AppSettings:SmtpPort");
        smtpPort = int.TryParse(portStr, out var port) ? port : 587;
        smtpUsername = GetConfigValue("Smtp:Username", "SmtpUsername", "AppSettings:SmtpUsername");
        smtpPassword = GetConfigValue("Smtp:Password", "SmtpPassword", "AppSettings:SmtpPassword");
        var sslStr = GetConfigValue("Smtp:EnableSsl", "SmtpEnableSsl", "AppSettings:SmtpEnableSsl");
        enableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
        fromAddress = GetConfigValue("Smtp:FromAddress", "SmtpFromAddress", "AppSettings:SmtpFromAddress");

        if (!string.IsNullOrWhiteSpace(smtpServer) &&
            smtpServer.Contains("gmail", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(smtpPassword))
        {
            smtpPassword = smtpPassword.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        }
    }

    public async Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null)
    {
        string? smtpServer = null;
        try
        {
            GetSmtpSettings(out smtpServer, out var smtpPort, out var smtpUsername, out var smtpPassword, out var enableSsl, out var fromAddr);

            if (string.IsNullOrWhiteSpace(smtpServer))
            {
                throw new InvalidOperationException(
                    "SMTP is not configured. Set at least Smtp:Server (and usually Smtp:Port). For authenticated SMTP also set Smtp:Username and Smtp:Password.");
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(smtpUsername) && !string.IsNullOrWhiteSpace(smtpPassword))
            {
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
            }
            else
            {
                client.Credentials = CredentialCache.DefaultNetworkCredentials;
            }

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
