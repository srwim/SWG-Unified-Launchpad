using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SwgLaunchpad.Core.Services;

public sealed record ServerCredentials(string Username, string Password);

/// <summary>
/// Per-server account storage for launcher-login servers (Legends-style).
/// Credentials are encrypted with Windows DPAPI (CurrentUser scope) — only
/// this Windows account on this machine can decrypt them. Never written to
/// settings.json, never synced into a server's game folder.
/// </summary>
public sealed class CredentialStore
{
    private readonly string _dir;

    public CredentialStore(LaunchpadPaths paths) =>
        _dir = Path.Combine(paths.StateDir, "credentials");

    public void Save(string serverId, ServerCredentials credentials)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Credential storage requires Windows DPAPI.");
        Directory.CreateDirectory(_dir);
        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials);
        var protectedBytes = ProtectedData.Protect(plain, OptionalEntropy(serverId), DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FileFor(serverId), protectedBytes);
        CryptographicOperations.ZeroMemory(plain);
    }

    public ServerCredentials? Load(string serverId)
    {
        var file = FileFor(serverId);
        if (!OperatingSystem.IsWindows() || !File.Exists(file)) return null;
        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(file), OptionalEntropy(serverId), DataProtectionScope.CurrentUser);
            var creds = JsonSerializer.Deserialize<ServerCredentials>(plain);
            CryptographicOperations.ZeroMemory(plain);
            return creds;
        }
        catch
        {
            return null; // wrong user/machine or corrupt — treat as not saved
        }
    }

    public void Clear(string serverId)
    {
        var file = FileFor(serverId);
        if (File.Exists(file)) File.Delete(file);
    }

    public bool Has(string serverId) => File.Exists(FileFor(serverId));

    private string FileFor(string serverId) => Path.Combine(_dir, serverId + ".cred");

    private static byte[] OptionalEntropy(string serverId) =>
        Encoding.UTF8.GetBytes("SwgLaunchpad:" + serverId);
}

/// <summary>Optional credential check against a server's loginUrl (2xx = valid).</summary>
public static class AccountValidator
{
    public static async Task<bool?> ValidateAsync(HttpClient http, string? loginUrl,
        ServerCredentials credentials, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(loginUrl)) return null; // server offers no check endpoint
        try
        {
            using var response = await http.PostAsync(loginUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = credentials.Username,
                ["password"] = credentials.Password,
            }), ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return null; // endpoint unreachable — don't block play on it
        }
    }
}
