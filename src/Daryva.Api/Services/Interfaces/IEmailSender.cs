namespace Daryva.Api.Services.Interfaces;

public interface IEmailSender
{
    Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null);
}
