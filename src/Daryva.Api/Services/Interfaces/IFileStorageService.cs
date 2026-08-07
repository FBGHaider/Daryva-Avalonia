namespace Daryva.Api.Services.Interfaces;

public interface IFileStorageService
{
    /// <summary>Writes content to disk and returns the relative StoragePath to persist on the Document row.</summary>
    Task<string> SaveAsync(Guid organizationId, Guid documentId, string? originalFileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Returns null if the path is missing/invalid or the file isn't present on disk.</summary>
    Task<byte[]?> ReadAsync(string? relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default);
}
