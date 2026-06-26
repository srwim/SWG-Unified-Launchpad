namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Minimal round-tripping editor for the client's INI-style options.cfg.
/// Only the lines we touch change; comments, ordering, and unknown settings
/// are preserved byte-for-byte.
/// </summary>
public sealed class OptionsCfg
{
    public const string FileName = "options.cfg";

    private readonly string _path;
    private readonly List<string> _lines;

    private OptionsCfg(string path, List<string> lines)
    {
        _path = path;
        _lines = lines;
    }

    public static OptionsCfg Load(string serverDir)
    {
        var path = Path.Combine(serverDir, FileName);
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        return new OptionsCfg(path, lines);
    }

    public string? Get(string section, string key)
    {
        int i = FindSection(section);
        if (i < 0) return null;
        for (i++; i < _lines.Count && !IsSectionHeader(_lines[i]); i++)
        {
            if (TryParseKey(_lines[i], key, out var value)) return value;
        }
        return null;
    }

    public void Set(string section, string key, string value)
    {
        int i = FindSection(section);
        if (i < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        int insertAt = i + 1;
        for (int j = i + 1; j < _lines.Count && !IsSectionHeader(_lines[j]); j++)
        {
            if (TryParseKey(_lines[j], key, out _))
            {
                _lines[j] = $"{key}={value}";
                return;
            }
            if (_lines[j].Trim().Length > 0) insertAt = j + 1;
        }
        _lines.Insert(insertAt, $"{key}={value}");
    }

    /// <summary>Saves via temp-file swap (hard-link safety invariant).</summary>
    public void Save()
    {
        var tmp = _path + ".lp-tmp";
        File.WriteAllLines(tmp, _lines);
        File.Move(tmp, _path, overwrite: true);
    }

    // Convenience accessors for the common knobs surfaced in the UI.
    public (int Width, int Height) GetResolution() =>
        (int.TryParse(Get("ClientGraphics", "screenWidth"), out var w) ? w : 1024,
         int.TryParse(Get("ClientGraphics", "screenHeight"), out var h) ? h : 768);

    public void SetResolution(int width, int height)
    {
        Set("ClientGraphics", "screenWidth", width.ToString());
        Set("ClientGraphics", "screenHeight", height.ToString());
    }

    public bool GetWindowed() =>
        string.Equals(Get("ClientGraphics", "windowed"), "1", StringComparison.Ordinal);

    public void SetWindowed(bool windowed) =>
        Set("ClientGraphics", "windowed", windowed ? "1" : "0");

    public bool GetSkipIntro() =>
        string.Equals(Get("ClientGame", "skipIntro"), "1", StringComparison.Ordinal);

    public void SetSkipIntro(bool skip) =>
        Set("ClientGame", "skipIntro", skip ? "1" : "0");

    private int FindSection(string section)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]) &&
                string.Equals(_lines[i].Trim(), $"[{section}]", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static bool IsSectionHeader(string line)
    {
        var t = line.Trim();
        return t.StartsWith('[') && t.EndsWith(']');
    }

    private static bool TryParseKey(string line, string key, out string value)
    {
        value = "";
        int eq = line.IndexOf('=');
        if (eq <= 0) return false;
        if (!string.Equals(line[..eq].Trim(), key, StringComparison.OrdinalIgnoreCase)) return false;
        value = line[(eq + 1)..].Trim();
        return true;
    }
}
