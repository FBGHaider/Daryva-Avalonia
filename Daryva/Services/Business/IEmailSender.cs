namespace Daryva.Services.Business
{
    public interface IEmailSender
    {
        Task<bool> SendEmailAsync(string toAddress, string subject, string body, string? fromAddress = null);
    }
}
