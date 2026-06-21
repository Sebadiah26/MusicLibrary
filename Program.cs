using Microsoft.EntityFrameworkCore;
using MusicLibrary.Data;
using MusicLibrary.Models;
using MusicLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MusicContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<CsvImportService>();
builder.Services.AddSingleton<ITunesXmlParserService>();

// Allow larger uploads (iTunes XML libraries can be 50+ MB).
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

var app = builder.Build();

// Create the database on first run (no manual migration needed for a quick start).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// ---- API ------------------------------------------------------------------

// Upload a CSV. ?type=artists|albums|songs
app.MapPost("/api/upload", async (HttpRequest request, CsvImportService importer) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected a multipart/form-data upload." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var typeRaw = form["type"].ToString();

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file was provided." });

    if (file.Length > 25 * 1024 * 1024)
        return Results.BadRequest(new { error = "File exceeds the 25 MB limit." });

    if (!Enum.TryParse<CsvType>(typeRaw, ignoreCase: true, out var csvType))
        return Results.BadRequest(new { error = "Query/form value 'type' must be artists, albums, or songs." });

    await using var stream = file.OpenReadStream();
    var result = await importer.ImportAsync(csvType, stream);

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// List all artists (with counts).
app.MapGet("/api/artists", async (MusicContext db, string? search, bool? favorites, string? genre, string? subGenre, string? sort, string? dir) =>
{
    var query = db.Artists.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(a => a.Name.Contains(search));

    if (favorites == true)
        query = query.Where(a => a.IsFavorite);

    if (!string.IsNullOrWhiteSpace(genre))
        query = query.Where(a => a.Genre != null && a.Genre == genre);

    if (!string.IsNullOrWhiteSpace(subGenre))
        query = query.Where(a => a.SubGenre != null && a.SubGenre == subGenre);

    var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
    var projected = query.Select(a => new
    {
        a.Id, a.Name, a.Genre, a.SubGenre, a.IsFavorite, a.Rating,
        AlbumCount = a.Albums.Count,
        SongCount = a.Songs.Count,
        FavoriteSongCount = a.Songs.Count(s => s.IsFavorite)
    });

    projected = sort?.ToLower() switch
    {
        "albums" => desc ? projected.OrderByDescending(a => a.AlbumCount) : projected.OrderBy(a => a.AlbumCount),
        "songs" => desc ? projected.OrderByDescending(a => a.SongCount) : projected.OrderBy(a => a.SongCount),
        "favorites" => desc ? projected.OrderByDescending(a => a.FavoriteSongCount) : projected.OrderBy(a => a.FavoriteSongCount),
        "rating" => desc ? projected.OrderByDescending(a => a.Rating) : projected.OrderBy(a => a.Rating),
        "genre" => desc ? projected.OrderByDescending(a => a.Genre) : projected.OrderBy(a => a.Genre),
        _ => desc ? projected.OrderByDescending(a => a.Name) : projected.OrderBy(a => a.Name),
    };

    var list = await projected.ToListAsync();

    return Results.Ok(list);
});

// List distinct genres and sub-genres.
app.MapGet("/api/genres", async (MusicContext db) =>
{
    var genres = await db.Artists.AsNoTracking()
        .Where(a => a.Genre != null && a.Genre != "")
        .Select(a => new { a.Genre, a.SubGenre })
        .ToListAsync();

    var genreList = genres
        .GroupBy(g => g.Genre, StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key)
        .Select(g => new
        {
            genre = g.Key,
            subGenres = g.Where(x => !string.IsNullOrWhiteSpace(x.SubGenre))
                         .Select(x => x.SubGenre!)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(s => s)
                         .ToList()
        })
        .ToList();

    return Results.Ok(genreList);
});

// Full detail for one artist, including albums and songs.
app.MapGet("/api/artists/{id:int}", async (int id, MusicContext db) =>
{
    var artist = await db.Artists
        .AsNoTracking()
        .Where(a => a.Id == id)
        .Select(a => new
        {
            a.Id, a.Name, a.Genre, a.SubGenre, a.IsFavorite, a.Rating,
            Albums = a.Albums
                .OrderBy(al => al.Year)
                .Select(al => new { al.Id, al.Title, al.Year, SongCount = al.Songs.Count }),
            Songs = a.Songs
                .OrderBy(s => s.Title)
                .Select(s => new { s.Id, s.Title, s.DurationSeconds, s.AlbumId, s.IsFavorite })
        })
        .FirstOrDefaultAsync();

    return artist is null ? Results.NotFound() : Results.Ok(artist);
});

// Update the curated fields on an artist.
app.MapPut("/api/artists/{id:int}", async (int id, ArtistUpdateDto dto, MusicContext db) =>
{
    var artist = await db.Artists.FindAsync(id);
    if (artist is null) return Results.NotFound();

    if (dto.Rating is < 1 or > 5)
        return Results.BadRequest(new { error = "Rating must be between 1 and 5, or null." });

    artist.Genre      = string.IsNullOrWhiteSpace(dto.Genre) ? null : dto.Genre.Trim();
    artist.SubGenre   = string.IsNullOrWhiteSpace(dto.SubGenre) ? null : dto.SubGenre.Trim();
    artist.IsFavorite = dto.IsFavorite;
    artist.Rating     = dto.Rating;

    await db.SaveChangesAsync();
    return Results.Ok(new { artist.Id, artist.Genre, artist.SubGenre, artist.IsFavorite, artist.Rating });
});

// Delete an artist (cascades albums + songs).
app.MapDelete("/api/artists/{id:int}", async (int id, MusicContext db) =>
{
    var artist = await db.Artists.FindAsync(id);
    if (artist is null) return Results.NotFound();
    db.Artists.Remove(artist);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Clear all data.
app.MapDelete("/api/reset", async (MusicContext db) =>
{
    db.Songs.RemoveRange(db.Songs);
    db.Albums.RemoveRange(db.Albums);
    db.Artists.RemoveRange(db.Artists);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "All data cleared." });
});

// ---- iTunes XML endpoints ------------------------------------------------

app.MapPost("/api/itunes/import", async (HttpRequest request, ITunesXmlParserService parser, CsvImportService importer) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected a multipart/form-data upload." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file was provided." });

    if (file.Length > 500 * 1024 * 1024)
        return Results.BadRequest(new { error = "File exceeds the 500 MB limit." });

    await using var stream = file.OpenReadStream();
    List<ITunesTrack> tracks;
    try
    {
        tracks = await parser.ParseXmlAsync(stream);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Failed to parse iTunes XML: " + ex.Message });
    }

    if (tracks.Count == 0)
        return Results.BadRequest(new { error = "No music tracks found in the XML file." });

    // Import artists, then albums, then songs through the existing CSV pipeline.
    var artistsCsv = parser.GenerateArtistsCsv(tracks);
    var albumsCsv = parser.GenerateAlbumsCsv(tracks);
    var songsCsv = parser.GenerateSongsCsv(tracks);

    var messages = new List<string>();
    int totalArtists = 0, totalAlbums = 0, totalSongs = 0, totalSkipped = 0;

    var artistResult = await importer.ImportAsync(CsvType.Artists, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(artistsCsv)));
    totalArtists += artistResult.ArtistsAdded;
    totalSkipped += artistResult.RowsSkipped;
    messages.AddRange(artistResult.Messages);

    var albumResult = await importer.ImportAsync(CsvType.Albums, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(albumsCsv)));
    totalArtists += albumResult.ArtistsAdded;
    totalAlbums += albumResult.AlbumsAdded;
    totalSkipped += albumResult.RowsSkipped;
    messages.AddRange(albumResult.Messages);

    var songResult = await importer.ImportAsync(CsvType.Songs, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(songsCsv)));
    totalArtists += songResult.ArtistsAdded;
    totalAlbums += songResult.AlbumsAdded;
    totalSongs += songResult.SongsAdded;
    totalSkipped += songResult.RowsSkipped;
    messages.AddRange(songResult.Messages);

    var totalFavorites = tracks.Count(t => t.IsFavorite);

    return Results.Ok(new
    {
        success = true,
        artistsAdded = totalArtists,
        albumsAdded = totalAlbums,
        songsAdded = totalSongs,
        favoriteSongs = totalFavorites,
        rowsSkipped = totalSkipped,
        messages = new[] { $"iTunes import complete: {totalArtists} artist(s), {totalAlbums} album(s), {totalSongs} song(s) added from {tracks.Count} track(s). {totalFavorites} favorite song(s)." }
    });
});

app.MapPost("/api/itunes/convert", async (HttpRequest request, ITunesXmlParserService parser) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected a multipart/form-data upload." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file was provided." });

    if (file.Length > 500 * 1024 * 1024)
        return Results.BadRequest(new { error = "File exceeds the 500 MB limit." });

    await using var stream = file.OpenReadStream();
    List<ITunesTrack> tracks;
    try
    {
        tracks = await parser.ParseXmlAsync(stream);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Failed to parse iTunes XML: " + ex.Message });
    }

    if (tracks.Count == 0)
        return Results.BadRequest(new { error = "No music tracks found in the XML file." });

    return Results.Ok(new
    {
        trackCount = tracks.Count,
        artistsCsv = parser.GenerateArtistsCsv(tracks),
        albumsCsv = parser.GenerateAlbumsCsv(tracks),
        songsCsv = parser.GenerateSongsCsv(tracks)
    });
});

app.Run();

// DTO used by the PUT endpoint.
record ArtistUpdateDto(string? Genre, string? SubGenre, bool IsFavorite, int? Rating);
