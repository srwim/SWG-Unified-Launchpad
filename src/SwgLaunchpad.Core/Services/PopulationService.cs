using System.Text.Json;
using System.Xml.Linq;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Reads a server's player count from its optional statusUrl. Understands the
/// two formats in the wild: SWGEmu-style status XML and simple JSON.
///
///   XML:  &lt;zoneServer&gt;...&lt;users connected="142"/&gt;...&lt;/zoneServer&gt;
///         (any element named "users" with a "connected" attribute, or an
///          element/attribute named "online"/"population")
///   JSON: { "online": 142 }  or  { "population": 142 }  or  { "users": 142 }
/// </summary>
public sealed class PopulationService
{
    private readonly HttpClient _http;

    public PopulationService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SwgLaunchpad/1.0");
    }

    public async Task<int?> GetPopulationAsync(string? statusUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(statusUrl)) return null;
        try
        {
            var body = (await _http.GetStringAsync(statusUrl, ct)).TrimStart();
            return body.StartsWith('{') || body.StartsWith('[')
                ? ParseJson(body)
                : ParseXml(body);
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var el in doc.Descendants())
            {
                var name = el.Name.LocalName.ToLowerInvariant();
                if (name is "users" or "online" or "population" or "connected")
                {
                    var attr = el.Attribute("connected")?.Value ?? el.Attribute("count")?.Value;
                    var text = attr ?? el.Value;
                    if (int.TryParse(text.Trim(), out var n) && n >= 0) return n;
                }
            }
        }
        catch { /* not parseable */ }
        return null;
    }

    private static int? ParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in new[] { "online", "population", "users", "connected" })
            {
                if (doc.RootElement.TryGetProperty(key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n) && n >= 0) return n;
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var s) && s >= 0) return s;
                }
            }
        }
        catch { /* not parseable */ }
        return null;
    }
}
