using System.Net.Sockets;

namespace SwgLaunchpad.Core.Services;

public enum ServerOnlineState { Unknown, Checking, Online, Offline }

/// <summary>
/// Cheap liveness check: can we open a TCP connection to the server's login
/// host/port? The same indicator the per-server launchers show.
/// </summary>
public static class ServerStatusService
{
    public static async Task<ServerOnlineState> CheckAsync(string host, int port, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(host, port, timeout.Token);
            return ServerOnlineState.Online;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ServerOnlineState.Offline;
        }
    }
}
