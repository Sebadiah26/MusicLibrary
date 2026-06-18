using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using MusicLibrary.Data;
using MusicLibrary.Models;

namespace MusicLibrary.Services;

public record ImportResult(
    bool Success,
    int ArtistsAdded,
    int AlbumsAdded,
    int SongsAdded,
    int RowsSkipped,
    IReadOnlyList<string> Messages);

public enum CsvType { Artists, Albums, Songs }

public class CsvImportService
{
    private readonly MusicContext _db;

    public CsvImportService(MusicContext db) => _db = db;

    public async Task<ImportResult> ImportAsync(CsvType type, Stream csvStream)
    {
        return type switch
        {
            CsvType.Artists => await ImportArtistsAsync(csvStream),
            CsvType.Albums  => await ImportAlbumsAsync(csvStream),
            CsvType.Songs   => await ImportSongsAsync(csvStream),
            _ => new ImportResult(false, 0, 0, 0, 0, new[] { "Unknown CSV type." })
        };
    }

    // -------------------------------------------------------------------------

    private async Task<ImportResult> ImportArtistsAsync(Stream stream)
    {
        var messages = new List<string>();
        int added = 0, skipped = 0;

        using var csv = OpenCsv(stream);
        if (!ReadHeader(csv, messages, out var map))
            return new ImportResult(false, 0, 0, 0, 0, messages);

        // Cache existing names so a re-upload doesn't create duplicates.
        var existing = await _db.Artists
            .Select(a => a.Name)
            .ToListAsync();
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        while (csv.Read())
        {
            var name = Field(csv, map, "name", "artist", "artistname")?.Trim();
            if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }
            if (known.Contains(name)) { skipped++; continue; }

            var artist = new Artist
            {
                Name     = name,
                Genre    = Field(csv, map, "genre")?.Trim(),
                SubGenre = Field(csv, map, "subgenre", "sub-genre", "sub_genre")?.Trim(),
                IsFavorite = ParseBool(Field(csv, map, "isfavorite", "favorite", "fav")),
                Rating   = ParseRating(Field(csv, map, "rating", "stars"))
            };

            _db.Artists.Add(artist);
            known.Add(name);
            added++;
        }

        await _db.SaveChangesAsync();
        messages.Add($"Imported {added} artist(s); skipped {skipped} row(s).");
        return new ImportResult(true, added, 0, 0, skipped, messages);
    }

    // -------------------------------------------------------------------------

    private async Task<ImportResult> ImportAlbumsAsync(Stream stream)
    {
        var messages = new List<string>();
        int albumsAdded = 0, artistsAdded = 0, skipped = 0;

        using var csv = OpenCsv(stream);
        if (!ReadHeader(csv, messages, out var map))
            return new ImportResult(false, 0, 0, 0, 0, messages);

        var artistCache = await _db.Artists
            .ToDictionaryAsync(a => a.Name, StringComparer.OrdinalIgnoreCase);

        while (csv.Read())
        {
            var artistName = Field(csv, map, "artist", "artistname", "name")?.Trim();
            var title = Field(csv, map, "album", "title", "albumtitle")?.Trim();
            if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(title))
            {
                skipped++;
                continue;
            }

            if (!artistCache.TryGetValue(artistName, out var artist))
            {
                artist = new Artist { Name = artistName };
                _db.Artists.Add(artist);
                artistCache[artistName] = artist;
                artistsAdded++;
            }

            artist.Albums.Add(new Album
            {
                Title = title,
                Year  = ParseInt(Field(csv, map, "year", "releaseyear"))
            });
            albumsAdded++;
        }

        await _db.SaveChangesAsync();
        messages.Add($"Imported {albumsAdded} album(s); created {artistsAdded} new artist(s); skipped {skipped} row(s).");
        return new ImportResult(true, artistsAdded, albumsAdded, 0, skipped, messages);
    }

    // -------------------------------------------------------------------------

    private async Task<ImportResult> ImportSongsAsync(Stream stream)
    {
        var messages = new List<string>();
        int songsAdded = 0, albumsAdded = 0, artistsAdded = 0, skipped = 0;

        using var csv = OpenCsv(stream);
        if (!ReadHeader(csv, messages, out var map))
            return new ImportResult(false, 0, 0, 0, 0, messages);

        var artistCache = await _db.Artists
            .Include(a => a.Albums)
            .ToDictionaryAsync(a => a.Name, StringComparer.OrdinalIgnoreCase);

        while (csv.Read())
        {
            var artistName = Field(csv, map, "artist", "artistname")?.Trim();
            var songTitle = Field(csv, map, "song", "title", "songtitle", "track")?.Trim();
            if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(songTitle))
            {
                skipped++;
                continue;
            }

            if (!artistCache.TryGetValue(artistName, out var artist))
            {
                artist = new Artist { Name = artistName };
                _db.Artists.Add(artist);
                artistCache[artistName] = artist;
                artistsAdded++;
            }

            // Optionally attach to an album if one is named.
            Album? album = null;
            var albumTitle = Field(csv, map, "album", "albumtitle")?.Trim();
            if (!string.IsNullOrWhiteSpace(albumTitle))
            {
                album = artist.Albums
                    .FirstOrDefault(al => string.Equals(al.Title, albumTitle, StringComparison.OrdinalIgnoreCase));
                if (album is null)
                {
                    album = new Album { Title = albumTitle };
                    artist.Albums.Add(album);
                    albumsAdded++;
                }
            }

            var song = new Song
            {
                Title = songTitle,
                DurationSeconds = ParseDuration(Field(csv, map, "duration", "length", "time")),
                IsFavorite = ParseBool(Field(csv, map, "isfavorite", "favorite", "fav", "loved"))
            };
            artist.Songs.Add(song);
            if (album is not null) album.Songs.Add(song);
            songsAdded++;
        }

        await _db.SaveChangesAsync();
        messages.Add($"Imported {songsAdded} song(s); created {albumsAdded} album(s) and {artistsAdded} artist(s); skipped {skipped} row(s).");
        return new ImportResult(true, artistsAdded, albumsAdded, songsAdded, skipped, messages);
    }

    // ---- Helpers ------------------------------------------------------------

    private static CsvReader OpenCsv(Stream stream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = false
        };
        var reader = new StreamReader(stream);
        return new CsvReader(reader, config);
    }

    /// <summary>Reads the header row and builds a normalized header→index map.</summary>
    private static bool ReadHeader(CsvReader csv, List<string> messages, out Dictionary<string, int> map)
    {
        map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            messages.Add("The file appears to be empty or has no header row.");
            return false;
        }

        var headers = csv.HeaderRecord;
        for (int i = 0; i < headers.Length; i++)
        {
            var key = Normalize(headers[i]);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = i;
        }
        return true;
    }

    private static string Normalize(string s) =>
        new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    /// <summary>Returns the first matching column value for any of the supplied aliases.</summary>
    private static string? Field(CsvReader csv, Dictionary<string, int> map, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (map.TryGetValue(Normalize(alias), out var idx))
            {
                var value = csv.GetField(idx);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return null;
    }

    private static bool ParseBool(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().ToLowerInvariant();
        return s is "true" or "yes" or "y" or "1" or "fav" or "favorite" or "favourite" or "star";
    }

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int? ParseRating(string? s)
    {
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return null;
        return v is >= 1 and <= 5 ? v : null;
    }

    /// <summary>Accepts plain seconds ("215") or mm:ss ("3:35").</summary>
    private static int? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.Contains(':'))
        {
            var parts = s.Split(':');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var m)
                && int.TryParse(parts[1], out var sec))
                return m * 60 + sec;
            return null;
        }
        return ParseInt(s);
    }
}
