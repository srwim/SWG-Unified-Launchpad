using System.Security.Cryptography;
using System.Text.Json;

namespace SwgLaunchpad.Core.Services;

public static class HashService
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 20, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Per-server cache of file hashes so a verify pass doesn't re-hash gigabytes.
/// A cached hash is trusted only while the file's size and mtime are unchanged.
/// </summary>
public sealed class HashCache
{
    public sealed record Entry(long Size, long LastWriteUtcTicks, string Sha256);

    private readonly string _cacheFile;
    private readonly Dictionary<string, Entry> _entries;

    private HashCache(string cacheFile, Dictionary<string, Entry> entries)
    {
        _cacheFile = cacheFile;
        _entries = entries;
    }

    public static HashCache Load(string cacheFile)
    {
        try
        {
            if (File.Exists(cacheFile))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(cacheFile));
                if (loaded is not null) return new HashCache(cacheFile, loaded);
            }
        }
        catch
        {
            // A corrupt cache is not an error; we just re-hash.
        }
        return new HashCache(cacheFile, new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Returns the file's SHA-256, from cache when safe, hashing otherwise.</summary>
    public async Task<string> GetSha256Async(string serverDir, string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(serverDir, relativePath);
        var info = new FileInfo(fullPath);
        if (_entries.TryGetValue(relativePath, out var e) &&
            e.Size == info.Length &&
            e.LastWriteUtcTicks == info.LastWriteTimeUtc.Ticks)
        {
            return e.Sha256;
        }

        var sha = await HashService.ComputeSha256Async(fullPath, ct);
        Record(relativePath, info.Length, info.LastWriteTimeUtc, sha);
        return sha;
    }

    public void Record(string relativePath, long size, DateTime lastWriteUtc, string sha256)
    {
        lock (_entries)
        {
            _entries[relativePath] = new Entry(size, lastWriteUtc.Ticks, sha256);
        }
    }

    public void Invalidate(string relativePath)
    {
        lock (_entries) _entries.Remove(relativePath);
    }

    public void Save()
    {
        lock (_entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            File.WriteAllText(_cacheFile, JsonSerializer.Serialize(_entries));
        }
    }
}
