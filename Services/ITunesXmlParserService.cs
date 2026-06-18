using System.Text;
using System.Xml.Linq;

namespace MusicLibrary.Services;

public record ITunesTrack(
    string? Name,
    string? Artist,
    string? Album,
    string? Genre,
    int? DurationSeconds,
    int? Year,
    bool IsFavorite);

public class ITunesXmlParserService
{
    /// <summary>
    /// Parses an iTunes Music Library XML (plist format) and extracts track data.
    /// </summary>
    public Task<List<ITunesTrack>> ParseXmlAsync(Stream stream)
    {
        var doc = XDocument.Load(stream);

        // Structure: plist > dict > key("Tracks") > dict > { key(trackId) > dict(track fields) }
        var rootDict = doc.Root?.Element("dict");
        if (rootDict is null)
            return Task.FromResult(new List<ITunesTrack>());

        var tracksDict = FindValueForKey(rootDict, "Tracks") as XElement;
        if (tracksDict is null)
            return Task.FromResult(new List<ITunesTrack>());

        var tracks = new List<ITunesTrack>();
        var elements = tracksDict.Elements().ToList();

        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name != "key")
                continue;

            var trackDict = elements[i + 1];
            if (trackDict.Name != "dict")
                continue;

            var fields = ParseDict(trackDict);

            // Skip non-music items (podcasts, movies, etc.)
            if (fields.TryGetValue("Podcast", out var isPodcast) && isPodcast == "true")
                continue;
            if (fields.TryGetValue("Kind", out var kind) &&
                kind is not null &&
                !kind.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
                !kind.Contains("music", StringComparison.OrdinalIgnoreCase))
                continue;

            int? duration = null;
            if (fields.TryGetValue("Total Time", out var totalTime) &&
                int.TryParse(totalTime, out var ms))
            {
                duration = ms / 1000;
            }

            int? year = null;
            if (fields.TryGetValue("Year", out var yearStr) &&
                int.TryParse(yearStr, out var y))
            {
                year = y;
            }

            bool isFavorite = fields.GetValueOrDefault("Loved") == "true";

            tracks.Add(new ITunesTrack(
                Name: fields.GetValueOrDefault("Name"),
                Artist: fields.GetValueOrDefault("Artist"),
                Album: fields.GetValueOrDefault("Album"),
                Genre: fields.GetValueOrDefault("Genre"),
                DurationSeconds: duration,
                Year: year,
                IsFavorite: isFavorite));
        }

        return Task.FromResult(tracks);
    }

    /// <summary>Generates a CSV string for unique artists.</summary>
    public string GenerateArtistsCsv(List<ITunesTrack> tracks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Genre");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tracks)
        {
            if (string.IsNullOrWhiteSpace(t.Artist)) continue;
            if (!seen.Add(t.Artist)) continue;
            sb.AppendLine($"{CsvEscape(t.Artist)},{CsvEscape(t.Genre)}");
        }

        return sb.ToString();
    }

    /// <summary>Generates a CSV string for unique albums.</summary>
    public string GenerateAlbumsCsv(List<ITunesTrack> tracks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Artist,Album,Year");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tracks)
        {
            if (string.IsNullOrWhiteSpace(t.Artist) || string.IsNullOrWhiteSpace(t.Album))
                continue;

            var key = $"{t.Artist}|||{t.Album}";
            if (!seen.Add(key)) continue;

            sb.AppendLine($"{CsvEscape(t.Artist)},{CsvEscape(t.Album)},{t.Year?.ToString() ?? ""}");
        }

        return sb.ToString();
    }

    /// <summary>Generates a CSV string for all songs.</summary>
    public string GenerateSongsCsv(List<ITunesTrack> tracks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Artist,Album,Song,Duration,IsFavorite");

        foreach (var t in tracks)
        {
            if (string.IsNullOrWhiteSpace(t.Artist) || string.IsNullOrWhiteSpace(t.Name))
                continue;

            sb.AppendLine($"{CsvEscape(t.Artist)},{CsvEscape(t.Album)},{CsvEscape(t.Name)},{t.DurationSeconds?.ToString() ?? ""},{(t.IsFavorite ? "true" : "")}");
        }

        return sb.ToString();
    }

    // ---- Helpers ---------------------------------------------------------------

    /// <summary>Finds the element following a key element with the given text in a dict.</summary>
    private static XElement? FindValueForKey(XElement dict, string keyName)
    {
        XElement? prev = null;
        foreach (var el in dict.Elements())
        {
            if (prev is not null && prev.Name == "key" && prev.Value == keyName)
                return el;
            prev = el;
        }
        return null;
    }

    /// <summary>Parses a plist dict element into key-value pairs (string values only).</summary>
    private static Dictionary<string, string> ParseDict(XElement dict)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var elements = dict.Elements().ToList();

        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name != "key") continue;

            var key = elements[i].Value;
            var valEl = elements[i + 1];

            var value = valEl.Name.LocalName switch
            {
                "string" => valEl.Value,
                "integer" => valEl.Value,
                "real" => valEl.Value,
                "true" => "true",
                "false" => "false",
                _ => null
            };

            if (value is not null)
                result[key] = value;
        }

        return result;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
