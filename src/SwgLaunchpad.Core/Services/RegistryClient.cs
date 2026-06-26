using System.Text.Json;
using SwgLaunchpad.Core.Models;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Fetches the moderated registry, then each server's self-hosted manifest.
/// Sources may be HTTPS URLs or local file paths (handy for development and
/// for testing a manifest before submission).
/// </summary>
public sealed class RegistryClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public RegistryClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SwgLaunchpad/1.0");
    }

    public async Task<ServerRegistry> GetRegistryAsync(string source, CancellationToken ct = default)
    {
        var registry = JsonSerializer.Deserialize<ServerRegistry>(await ReadSourceAsync(source, ct), JsonOpts)
            ?? throw new InvalidDataException("Registry parsed to null.");
        if (registry.RegistryVersion != 1)
            throw new InvalidDataException($"Unsupported registryVersion {registry.RegistryVersion}.");
        return registry;
    }

    public async Task<ServerManifest> GetManifestAsync(string source, CancellationToken ct = default)
    {
        var manifest = JsonSerializer.Deserialize<ServerManifest>(await ReadSourceAsync(source, ct), JsonOpts)
            ?? throw new InvalidDataException("Manifest parsed to null.");
        manifest.Validate();
        return manifest;
    }

    public async Task<FileManifest> GetFileManifestAsync(string source, CancellationToken ct = default)
    {
        var fm = JsonSerializer.Deserialize<FileManifest>(await ReadSourceAsync(source, ct), JsonOpts)
            ?? throw new InvalidDataException("File manifest parsed to null.");
        foreach (var f in fm.Files)
        {
            if (string.IsNullOrWhiteSpace(f.Path) || f.Sha256.Length != 64 || f.Size < 0)
                throw new InvalidDataException($"File manifest entry '{f.Path}' is malformed.");
        }
        return fm;
    }

    private async Task<string> ReadSourceAsync(string source, CancellationToken ct)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await _http.GetStringAsync(uri, ct);
        }
        return await File.ReadAllTextAsync(source, ct);
    }
}
