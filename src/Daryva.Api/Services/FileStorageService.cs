using System.Text.RegularExpressions;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

/// <summary>
/// Local-disk document storage. StoragePath on Document rows is always the relative
/// "{orgId}/{documentId}{ext}" path returned by SaveAsync, never an absolute path -
/// keeps the DB portable across environments/volumes and never leaks the host
/// filesystem layout through the API's DocumentResponse.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private static readonly Regex ExtensionPattern = new("^[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);

    private readonly string _root;
    private readonly string _rootWithSeparator;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _logger = logger;

        var configuredRoot = configuration["Storage:DocumentsRootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot) ? "./storage/documents" : configuredRoot);
        _rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Guid organizationId, Guid documentId, string? originalFileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var extension = SanitizeExtension(originalFileName);
        var relativePath = $"{organizationId:D}/{documentId:D}{extension}";
        var fullPath = ResolveWithinRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return relativePath;
    }

    public async Task<byte[]?> ReadAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        string fullPath;
        try
        {
            fullPath = ResolveWithinRoot(relativePath);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Rejected document read outside storage root: {RelativePath}", relativePath);
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Document file missing on disk: {RelativePath}", relativePath);
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.CompletedTask;

        try
        {
            var fullPath = ResolveWithinRoot(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete document file on disk: {RelativePath}", relativePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveWithinRoot(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Resolved path '{fullPath}' escapes the storage root.");
        }

        return fullPath;
    }

    private static string SanitizeExtension(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            return ".bin";

        var extension = Path.GetExtension(originalFileName).TrimStart('.');
        return ExtensionPattern.IsMatch(extension) ? $".{extension.ToLowerInvariant()}" : ".bin";
    }
}
