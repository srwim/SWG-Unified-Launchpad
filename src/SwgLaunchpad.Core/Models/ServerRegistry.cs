using System.Text.Json.Serialization;

namespace SwgLaunchpad.Core.Models;

/// <summary>
/// The moderated master list (one file, controlled by r/swg) pointing at each
/// approved server's self-hosted manifest.
/// </summary>
public sealed class ServerRegistry
{
    [JsonPropertyName("registryVersion")] public int RegistryVersion { get; set; } = 1;
    [JsonPropertyName("updated")] public DateTimeOffset Updated { get; set; }
    [JsonPropertyName("servers")] public List<RegistryEntry> Servers { get; set; } = new();
}

public sealed class RegistryEntry
{
    [JsonPropertyName("serverId")] public string ServerId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("manifestUrl")] public string ManifestUrl { get; set; } = "";
    /// <summary>"verified" entries are shown by default; anything else is hidden.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "verified";
}
