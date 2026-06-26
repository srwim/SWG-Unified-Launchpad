namespace SwgLaunchpad.Core;

/// <summary>
/// On-disk layout:
///   {InstallRoot}\base\                pristine SWG 14.1 client (shared, never written)
///   {InstallRoot}\servers\{serverId}\  complete runnable folder per server
///   {InstallRoot}\state\               hash caches + launcher settings
/// </summary>
public sealed class LaunchpadPaths
{
    public string InstallRoot { get; }

    public LaunchpadPaths(string installRoot) => InstallRoot = installRoot;

    public static LaunchpadPaths Default() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwgLaunchpad"));

    public string BaseClientDir => Path.Combine(InstallRoot, "base");
    public string ServersDir => Path.Combine(InstallRoot, "servers");
    public string StateDir => Path.Combine(InstallRoot, "state");

    public string ServerDir(string serverId) => Path.Combine(ServersDir, serverId);
    public string HashCacheFile(string serverId) => Path.Combine(StateDir, $"hashcache-{serverId}.json");
    public string SettingsFile => Path.Combine(StateDir, "settings.json");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(ServersDir);
        Directory.CreateDirectory(StateDir);
    }
}
