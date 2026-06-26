using SwgLaunchpad.Core.Models;

namespace SwgLaunchpad.Core.Services;

public sealed record PatchProgress(
    int TotalFiles, int CompletedFiles, string CurrentFile,
    long TotalBytes, long DownloadedBytes);

/// <summary>
/// The generic patcher: diff local files against a server's file manifest,
/// download what differs. This is the capability every per-server launcher
/// implements privately, done once.
///
/// INVARIANT (hard-link safety): files in the server folder may be NTFS hard
/// links into the shared base client. We must never open an existing
/// destination file for writing — downloads go to a temp file and replace the
/// destination via File.Move(..., overwrite: true), which swaps the directory
/// entry and leaves the base client untouched.
/// </summary>
public sealed class PatchService
{
    private const int MaxParallelDownloads = 4;
    private const int RetriesPerFile = 2;
    private readonly HttpClient _http;

    public PatchService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SwgLaunchpad/1.0");
    }

    /// <summary>Returns the manifest entries that are missing or differ locally.</summary>
    public async Task<List<ManifestFile>> PlanAsync(
        string serverDir, FileManifest manifest, HashCache cache,
        bool ignoreCache = false, CancellationToken ct = default)
    {
        var needed = new List<ManifestFile>();
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = ResolveSafePath(serverDir, file.Path);
            var info = new FileInfo(fullPath);

            if (!info.Exists || info.Length != file.Size)
            {
                needed.Add(file);
                continue;
            }
            if (ignoreCache) cache.Invalidate(NormalizeRelative(file.Path));

            var localSha = await cache.GetSha256Async(serverDir, NormalizeRelative(file.Path), ct);
            if (!string.Equals(localSha, file.Sha256, StringComparison.OrdinalIgnoreCase))
                needed.Add(file);
        }
        return needed;
    }

    public async Task ExecuteAsync(
        string serverDir, string baseUrl, IReadOnlyList<ManifestFile> files, HashCache cache,
        IProgress<PatchProgress>? progress = null, CancellationToken ct = default)
    {
        long totalBytes = files.Sum(f => f.Size);
        long downloadedBytes = 0;
        int completed = 0;
        var errors = new List<Exception>();
        var gate = new SemaphoreSlim(MaxParallelDownloads);

        var tasks = files.Select(async file =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await DownloadOneAsync(serverDir, baseUrl, file, cache, ct);
                Interlocked.Add(ref downloadedBytes, file.Size);
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new PatchProgress(files.Count, done, file.Path, totalBytes,
                    Interlocked.Read(ref downloadedBytes)));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lock (errors) errors.Add(new IOException($"Failed to update '{file.Path}': {ex.Message}", ex));
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        cache.Save();

        if (errors.Count > 0)
            throw new AggregateException($"{errors.Count} file(s) failed to update.", errors);
    }

    private async Task DownloadOneAsync(
        string serverDir, string baseUrl, ManifestFile file, HashCache cache, CancellationToken ct)
    {
        var destPath = ResolveSafePath(serverDir, file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var tmpPath = destPath + ".lp-tmp";
        var url = baseUrl + Uri.EscapeDataString(file.Path).Replace("%2F", "/");

        Exception? last = null;
        for (int attempt = 0; attempt <= RetriesPerFile; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    await using var src = await response.Content.ReadAsStreamAsync(ct);
                    await using var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write,
                        FileShare.None, 1 << 20, useAsync: true);
                    await src.CopyToAsync(dst, ct);
                }

                var sha = await HashService.ComputeSha256Async(tmpPath, ct);
                if (!string.Equals(sha, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Downloaded file failed hash verification.");

                // Atomic swap: replaces a hard link without writing through it.
                File.Move(tmpPath, destPath, overwrite: true);

                var info = new FileInfo(destPath);
                cache.Record(NormalizeRelative(file.Path), info.Length, info.LastWriteTimeUtc, sha);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                TryDelete(tmpPath);
            }
        }
        throw last!;
    }

    /// <summary>
    /// Resolves a manifest-relative path inside serverDir, rejecting absolute
    /// paths and any traversal that escapes the server folder.
    /// </summary>
    public static string ResolveSafePath(string serverDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"Unsafe manifest path '{relativePath}'.");

        var root = Path.GetFullPath(serverDir);
        var full = Path.GetFullPath(Path.Combine(root, NormalizeRelative(relativePath)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Manifest path '{relativePath}' escapes the install folder.");
        return full;
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
