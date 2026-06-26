using System.Diagnostics;
using SwgLaunchpad.Core.Models;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Launches a server's client from its own folder. Multi-instance
/// (dual-boxing) is just launching again, unless the server's manifest
/// disallows it.
/// </summary>
public sealed class LaunchService
{
    private readonly Dictionary<string, List<Process>> _running = new();

    public Process Launch(string serverId, string serverDir, ServerManifest manifest,
        ServerCredentials? credentials = null)
    {
        var exePath = Path.Combine(serverDir, manifest.Client.Executable);
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"Client executable not found. Run Install/Update first.", exePath);

        if (!manifest.Client.AllowMultiInstance && RunningCount(serverId) > 0)
            throw new InvalidOperationException($"{manifest.Name} does not allow multiple game instances.");

        if (manifest.Auth.RequiresLauncherLogin && credentials is null)
            throw new InvalidOperationException($"{manifest.Name} requires an account. Click Account to sign in first.");

        var arguments = manifest.Client.Arguments;
        if (credentials is not null)
        {
            arguments = arguments
                .Replace("{username}", credentials.Username, StringComparison.OrdinalIgnoreCase)
                .Replace("{password}", credentials.Password, StringComparison.OrdinalIgnoreCase);
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = serverDir,
            Arguments = arguments,
            UseShellExecute = false,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("The game process failed to start.");

        lock (_running)
        {
            if (!_running.TryGetValue(serverId, out var list))
                _running[serverId] = list = new List<Process>();
            list.Add(process);
        }
        return process;
    }

    public int RunningCount(string serverId)
    {
        lock (_running)
        {
            if (!_running.TryGetValue(serverId, out var list)) return 0;
            list.RemoveAll(p => p.HasExited);
            return list.Count;
        }
    }
}
