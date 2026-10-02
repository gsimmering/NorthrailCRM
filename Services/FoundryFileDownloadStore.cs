using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NorthrailCRM.Services;

public sealed class FoundryFileDownloadStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, FoundryDownloadRecord> files = new(StringComparer.Ordinal);

    public AgentChatAttachment CreateAttachment(string containerId, string fileId, string fileName)
    {
        PruneExpired();
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "Foundry-Datei";
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        files[token] = new FoundryDownloadRecord(containerId, fileId, safeFileName, DateTimeOffset.UtcNow.Add(TokenLifetime));

        return new AgentChatAttachment(
            safeFileName,
            $"/mnt/data/{safeFileName}",
            $"/api/agent-files/{token}");
    }

    public bool TryGet(string token, out FoundryDownloadRecord file)
    {
        if (files.TryGetValue(token, out file!) && file.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        files.TryRemove(token, out _);
        file = default!;
        return false;
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in files)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                files.TryRemove(entry.Key, out _);
            }
        }
    }
}

public sealed record FoundryDownloadRecord(
    string ContainerId,
    string FileId,
    string FileName,
    DateTimeOffset ExpiresAt);