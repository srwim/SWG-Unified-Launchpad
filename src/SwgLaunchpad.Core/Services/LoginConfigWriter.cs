using SwgLaunchpad.Core.Models;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Retargets the stock client at a server's login host/port — the same trick
/// every per-server launcher uses: write login.cfg, make sure the client's
/// root .cfg includes it.
/// </summary>
public static class LoginConfigWriter
{
    private const string LoginCfgName = "login.cfg";
    private const string IncludeLine = ".include \"login.cfg\"";

    public static void Apply(string serverDir, ServerManifest manifest)
    {
        WriteLoginCfg(serverDir, manifest.Login.Host, manifest.Login.Port);
        EnsureIncluded(serverDir, manifest.Client.Executable);
    }

    public static void WriteLoginCfg(string serverDir, string host, int port)
    {
        var path = Path.Combine(serverDir, LoginCfgName);
        File.WriteAllText(path,
            "[ClientGame]\r\n" +
            $"loginServerAddress0={host}\r\n" +
            $"loginServerPort0={port}\r\n");
    }

    /// <summary>
    /// The client reads "{executableName}.cfg" (e.g. swgemu.cfg for
    /// SWGEmu.exe) at startup; guarantee it includes login.cfg.
    /// </summary>
    public static void EnsureIncluded(string serverDir, string executable)
    {
        var cfgName = Path.GetFileNameWithoutExtension(executable) + ".cfg";
        var cfgPath = Path.Combine(serverDir, cfgName);

        if (!File.Exists(cfgPath))
        {
            File.WriteAllText(cfgPath, IncludeLine + "\r\n");
            return;
        }

        var content = File.ReadAllText(cfgPath);
        if (content.Contains(IncludeLine, StringComparison.OrdinalIgnoreCase)) return;

        // Replace-via-temp keeps the hard-link safety invariant: never write
        // through a file that may be linked into the shared base client.
        var tmp = cfgPath + ".lp-tmp";
        File.WriteAllText(tmp, IncludeLine + "\r\n" + content);
        File.Move(tmp, cfgPath, overwrite: true);
    }
}
