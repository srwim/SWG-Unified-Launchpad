using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwgLaunchpad.Core;

/// <summary>Launcher-level settings persisted in state\settings.json.</summary>
public sealed class LauncherSettings
{
    public const string DefaultRegistrySource =
        "https://raw.githubusercontent.com/r-swg/launchpad-registry/main/registry.json";

    [JsonPropertyName("registrySource")]
    public string RegistrySource { get; set; } = DefaultRegistrySource;

    /// <summary>
    /// Where the user's pristine SWG 14.1 client lives. Copied/imported into
    /// {InstallRoot}\base on first run if set to an external path.
    /// </summary>
    [JsonPropertyName("baseClientSource")]
    public string? BaseClientSource { get; set; }

    /// <summary>
    /// Manifest sources (URLs or local file paths) the user added themselves,
    /// beyond the moderated registry. Shown with a "custom" badge.
    /// </summary>
    [JsonPropertyName("customManifestSources")]
    public List<string> CustomManifestSources { get; set; } = new();

    [JsonPropertyName("windowWidth")] public double WindowWidth { get; set; } = 960;
    [JsonPropertyName("windowHeight")] public double WindowHeight { get; set; } = 700;

    public static LauncherSettings Load(string settingsFile)
    {
        try
        {
            if (File.Exists(settingsFile))
                return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(settingsFile)) ?? new();
        }
        catch { /* fall through to defaults */ }
        return new LauncherSettings();
    }

    public void Save(string settingsFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        File.WriteAllText(settingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
