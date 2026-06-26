using System.Xml.Linq;

namespace SwgLaunchpad.Core.Services;

public sealed record NewsHeadline(string Title, DateTimeOffset? Published);

/// <summary>
/// Pulls the latest headline from a server's RSS/Atom feed for the server
/// card. Tolerant: any failure just means no headline.
/// </summary>
public sealed class NewsService
{
    private readonly HttpClient _http;

    public NewsService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SwgLaunchpad/1.0");
    }

    public async Task<NewsHeadline?> GetLatestAsync(string? feedUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(feedUrl)) return null;
        try
        {
            var xml = await _http.GetStringAsync(feedUrl, ct);
            var doc = XDocument.Parse(xml);

            // RSS 2.0
            var item = doc.Descendants("item").FirstOrDefault();
            if (item is not null)
            {
                var title = item.Element("title")?.Value?.Trim();
                if (string.IsNullOrEmpty(title)) return null;
                DateTimeOffset? date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, out var d) ? d : null;
                return new NewsHeadline(title, date);
            }

            // Atom
            XNamespace atom = "http://www.w3.org/2005/Atom";
            var entry = doc.Descendants(atom + "entry").FirstOrDefault();
            var atomTitle = entry?.Element(atom + "title")?.Value?.Trim();
            if (string.IsNullOrEmpty(atomTitle)) return null;
            DateTimeOffset? atomDate = DateTimeOffset.TryParse(entry?.Element(atom + "updated")?.Value, out var ad) ? ad : null;
            return new NewsHeadline(atomTitle, atomDate);
        }
        catch
        {
            return null;
        }
    }
}
