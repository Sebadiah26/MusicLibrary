# CLAUDE.md — MusicLibrary

## Project Overview

Single-project ASP.NET Core 8.0 Web API with a vanilla JavaScript frontend for managing a personal music library of artists, albums, and songs. Supports CSV and iTunes XML import.

**Repository:** https://github.com/Sebadiah26/MusicLibrary
**Solution file:** `MusicLibrary.sln`

## Build & Run

```bash
dotnet build MusicLibrary.sln
dotnet run --project MusicLibrary
```

- **Kestrel (dev):** https://localhost:7080 / http://localhost:5080
- **IIS (prod):** In-process hosting via AspNetCoreModuleV2
- **Cloudflare tunnel:** `music.craigkielinski.com` → http://192.168.1.168:80

## Architecture

Single project, no layered separation. Minimal APIs defined in `Program.cs`, vanilla JS frontend in `wwwroot/`.

```
MusicLibrary/
├── Program.cs              Startup config + all API endpoints (minimal APIs)
├── Data/
│   └── MusicContext.cs     EF Core DbContext + entity configuration
├── Models/
│   ├── Artist.cs           Artist entity (name, genre, subGenre, rating, favorite)
│   ├── Album.cs            Album entity (title, year, FK to artist)
│   └── Song.cs             Song entity (title, duration, favorite, FK to artist/album)
├── Services/
│   ├── CsvImportService.cs     CSV parsing & import (artists, albums, songs)
│   └── ITunesXmlParserService.cs  iTunes Music Library.xml parser
├── wwwroot/
│   ├── index.html          Single-page HTML layout
│   ├── app.js              Vanilla JS SPA logic (599 lines)
│   └── styles.css          Dark theme CSS
├── sample-data/            Sample CSV files (artists, albums, songs)
├── appsettings.json        SQL Server connection string
├── web.config              IIS hosting config (500 MB upload limit)
└── Properties/
    └── launchSettings.json
```

## API Endpoints

All defined as minimal APIs in `Program.cs`:

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/artists` | List artists (search, genre, subGenre, favorites, sort, paging) |
| GET | `/api/artists/{id}` | Artist detail with albums and songs |
| PUT | `/api/artists/{id}` | Update genre, subGenre, isFavorite, rating |
| DELETE | `/api/artists/{id}` | Delete artist (cascades albums + songs) |
| GET | `/api/genres` | List distinct genres with sub-genres |
| PUT | `/api/songs/{id}/favorite` | Toggle song favorite |
| POST | `/api/upload?type=` | CSV import (type: artists, albums, songs) |
| POST | `/api/itunes/import` | iTunes XML import to database |
| POST | `/api/itunes/convert` | iTunes XML to CSV conversion (no DB write) |
| DELETE | `/api/reset` | Clear all data |

**Paging:** Default page size 50, max 200. Genre view skips paging.

## Database

- **SQL Server** (named instance `CKIELINSKI`, Windows auth)
- **Database:** `MusicLibrary`
- **No migrations** — uses `EnsureCreated()` on startup
- **Entities:** Artist, Album, Song
- **Cascade deletes:** Artist deletion cascades to albums and songs

## Key Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Backend framework |
| EF Core | 8.0.8 | ORM (SQL Server provider) |
| CsvHelper | 33.0.1 | CSV parsing |
| Vanilla JS | ES6+ | Frontend SPA |
| Bootstrap | (none) | Custom dark theme CSS |

## Frontend (wwwroot/)

Vanilla JavaScript SPA with no build step.

**Features:**
- Artist list with search, genre/subGenre filters, favorites filter
- Sortable columns (name, albums, songs, favorites, rating, genre) with direction toggle
- Genre view (nested: genre → subGenre → artists in columns)
- Inline editing: genre, sub-genre, favorite toggle, star rating (1-5)
- Artist detail view with albums and songs
- Song favorite toggle
- CSV upload and iTunes XML import/convert
- Paging (50 per page)

**Key JS globals:** `currentPage`, `PAGE_SIZE`, `sortDir`, `viewMode`, `genreData`

## CSV Import Notes

- Case-insensitive, whitespace-normalized header matching
- Supports multiple column aliases (e.g., "artist"/"artistname"/"name")
- De-duplicates on import (artists by name, albums by artist+title, songs by artist+title+album)
- Duration accepts "mm:ss" or raw seconds
- Boolean fields accept: "true", "yes", "y", "1", "fav", "star"
- Re-import updates favorite status on existing songs

## Hosting Notes

- Upload limit: 500 MB (configured in FormOptions, Kestrel, and web.config)
- Server-side paging added to prevent Cloudflare timeouts on large artist lists
- `app.js` loaded with cache-busting query string in `index.html`
