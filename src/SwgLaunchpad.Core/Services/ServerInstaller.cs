using SwgLaunchpad.Core.Models;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Orchestrates the full install/update flow for one server:
/// base hard-link → fetch file manifest → diff → download → login config.
/// </summary>
public sealed class ServerInstaller
{
    private readonly LaunchpadPaths _paths;
    private readonly RegistryClient _registryClient;
    private readonly PatchService _patchService;
    private readonly GameFolderService _folderService = new();

    public ServerInstaller(LaunchpadPaths paths, RegistryClient registryClient, PatchService patchService)
    {
        _paths = paths;
        _registryClient = registryClient;
        _patchService = patchService;
    }

    public async Task InstallOrUpdateAsync(
        ServerManifest manifest,
        bool verifyAll = false,
        IProgress<PatchProgress>? progress = null,
        IProgress<string>? status = null,
        CancellationToken ct = default)
    {
        _paths.EnsureCreated();
        var serverDir = _paths.ServerDir(manifest.ServerId);

        status?.Report("Preparing game folder…");
        _folderService.PrepareServerFolder(_paths.BaseClientDir, serverDir);

        status?.Report("Fetching file manifest…");
        var fileManifest = await _registryClient.GetFileManifestAsync(manifest.Files.FileManifestUrl, ct);

        status?.Report("Checking files…");
        var cache = HashCache.Load(_paths.HashCacheFile(manifest.ServerId));
        var needed = await _patchService.PlanAsync(serverDir, fileManifest, cache, ignoreCache: verifyAll, ct);
        cache.Save();

        if (needed.Count > 0)
        {
            status?.Report($"Downloading {needed.Count} file(s)…");
            await _patchService.ExecuteAsync(serverDir, manifest.Files.BaseUrl, needed, cache, progress, ct);
        }

        status?.Report("Writing login configuration…");
        LoginConfigWriter.Apply(serverDir, manifest);

        status?.Report("Up to date");
    }
}
