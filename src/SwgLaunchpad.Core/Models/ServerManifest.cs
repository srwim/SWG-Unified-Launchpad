using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SwgLaunchpad.Core.Models;

/// <summary>
/// The manifest each server hosts at a stable HTTPS URL. This is the server's
/// "submission": everything the launcher needs to patch, configure, and launch
/// that server's client.
/// </summary>
public sealed class ServerManifest
{
    [JsonPropertyName("manifestVersion")] public int ManifestVersion { get; set; } = 1;
    [JsonPropertyName("serverId")] public string ServerId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("era")] public string Era { get; set; } = "";
    [JsonPropertyName("website")] public string? Website { get; set; }
    [JsonPropertyName("discord")] public string? Discord { get; set; }
    [JsonPropertyName("bannerUrl")] public string? BannerUrl { get; set; }
    [JsonPropertyName("newsUrl")] public string? NewsUrl { get; set; }
    /// <summary>Optional endpoint returning player count (SWGEmu status XML or JSON).</summary>
    [JsonPropertyName("statusUrl")] public string? StatusUrl { get; set; }
    [JsonPropertyName("login")] public LoginInfo Login { get; set; } = new();
    [JsonPropertyName("client")] public ClientInfo Client { get; set; } = new();
    [JsonPropertyName("files")] public FilesInfo Files { get; set; } = new();
    [JsonPropertyName("auth")] public AuthInfo Auth { get; set; } = new();

    private static readonly Regex ServerIdPattern = new("^[a-z0-9][a-z0-9-]{1,63}$", RegexOptions.Compiled);

    /// <summary>Throws if the manifest is structurally invalid or unsafe.</summary>
    public void Validate()
    {
        if (ManifestVersion != 1)
            throw new InvalidDataException($"Unsupported manifestVersion {ManifestVersion}.");
        if (!ServerIdPattern.IsMatch(ServerId))
            throw new InvalidDataException($"Invalid serverId '{ServerId}' (lowercase letters, digits, hyphens; 2-64 chars).");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("Manifest is missing a server name.");
        if (string.IsNullOrWhiteSpace(Login.Host) || Login.Port is < 1 or > 65535)
            throw new InvalidDataException("Manifest login host/port is missing or invalid.");
        if (string.IsNullOrWhiteSpace(Client.Executable) ||
            Client.Executable.IndexOfAny(new[] { '/', '\\' }) >= 0)
            throw new InvalidDataException("client.executable must be a bare file name.");
        RequireHttps(Files.BaseUrl, "files.baseUrl");
        RequireHttps(Files.FileManifestUrl, "files.fileManifestUrl");
        if (!Files.BaseUrl.EndsWith('/'))
            throw new InvalidDataException("files.baseUrl must end with '/'.");
    }

    private static void RequireHttps(string? url, string field)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"{field} must be an absolute https:// URL.");
    }
}

public sealed class LoginInfo
{
    [JsonPropertyName("host")] public string Host { get; set; } = "";
    [JsonPropertyName("port")] public int Port { get; set; }
}

public sealed class ClientInfo
{
    [JsonPropertyName("executable")] public string Executable { get; set; } = "SWGEmu.exe";
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = "";
    [JsonPropertyName("allowMultiInstance")] public bool AllowMultiInstance { get; set; } = true;
}

public sealed class FilesInfo
{
    [JsonPropertyName("baseUrl")] public string BaseUrl { get; set; } = "";
    [JsonPropertyName("fileManifestUrl")] public string FileManifestUrl { get; set; } = "";
}

/// <summary>
/// How the server handles accounts. mode "none": accounts are entered at the
/// in-game login screen (SWGEmu style) — the launcher stays out of it.
/// mode "launcher": the launcher collects credentials (SWG Legends style),
/// stores them DPAPI-encrypted, optionally validates them against loginUrl
/// (POST {username,password}, 2xx = valid), and substitutes {username} and
/// {password} placeholders in client.arguments at launch.
/// </summary>
public sealed class AuthInfo
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "none";
    [JsonPropertyName("registerUrl")] public string? RegisterUrl { get; set; }
    [JsonPropertyName("passwordResetUrl")] public string? PasswordResetUrl { get; set; }
    [JsonPropertyName("loginUrl")] public string? LoginUrl { get; set; }

    [JsonIgnore] public bool RequiresLauncherLogin =>
        string.Equals(Mode, "launcher", StringComparison.OrdinalIgnoreCase);
}
