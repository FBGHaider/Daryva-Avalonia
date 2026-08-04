namespace Daryva.Api.Dtos;

public class SupportAccessCodeResponse
{
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ResolveSupportAccessCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class ResolvedSupportAccessCodeResponse
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
}
