using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace SwgLaunchpad.Core.Services;

public static class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public record UpdateInfo(
        string CurrentVersion,
        string LatestVersion,
        string DownloadUrl,
        string ReleaseNotes,
        bool UpdateAvailable);

    /// <summary>
    /// Checks the GitHub releases API for a newer version.
    /// Returns null if the check fails (network error, rate limit, etc.).
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(
        string repoOwner = "srwim",
        string repoName  = "SWG-Unified-Launchpad")
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.Clear();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd($"SwgLaunchpad/{CurrentVersion()}");

            using var response = await Http.GetAsync(
                $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest");

            if (!response.IsSuccessStatusCode) return null;

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            var tag = doc.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
            var url = doc.GetProperty("html_url").GetString() ?? "";
            var body = doc.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

            if (!Version.TryParse(tag, out var latest)) return null;
            var current = CurrentVersionObject();

            return new UpdateInfo(
                CurrentVersion: current.ToString(3),
                LatestVersion: latest.ToString(3),
                DownloadUrl: url,
                ReleaseNotes: body.Length > 400 ? body[..400] + "…" : body,
                UpdateAvailable: latest > current);
        }
        catch
        {
            return null;
        }
    }

    public static string CurrentVersion()
        => CurrentVersionObject().ToString(3);

    private static Version CurrentVersionObject()
        => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
}
