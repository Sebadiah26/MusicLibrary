using Microsoft.EntityFrameworkCore;
using MusicLibrary.Data;
using MusicLibrary.Models;
using MusicLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MusicContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<CsvImportService>();

// Allow larger CSV uploads (default multipart limit is ~128 MB; we cap files at 25 MB below).
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 25 * 1024 * 1024;
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
app.MapGet("/api/artists", async (MusicContext db, string? search, bool? favorites) =>
{
    var query = db.Artists.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(a => a.Name.Contains(search));

    if (favorites == true)
        query = query.Where(a => a.IsFavorite);

    var list = await query
        .OrderBy(a => a.Name)
        .Select(a => new
        {
            a.Id, a.Name, a.Genre, a.SubGenre, a.IsFavorite, a.Rating,
            AlbumCount = a.Albums.Count,
            SongCount = a.Songs.Count
        })
        .ToListAsync();

    return Results.Ok(list);
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
                .Select(s => new { s.Id, s.Title, s.DurationSeconds, s.AlbumId })
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

app.Run();

// DTO used by the PUT endpoint.
record ArtistUpdateDto(string? Genre, string? SubGenre, bool IsFavorite, int? Rating);
