# 🎵 Music Library

> Import musical artists, albums, and songs from CSV — then curate genre, sub-genre, favorites, and ratings.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)
![SQL Server](https://img.shields.io/badge/SQL%20Server-LocalDB-CC2927?logo=microsoftsqlserver&logoColor=white)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

An ASP.NET Core (.NET 8) web app that imports **Musical Artists** — and optionally **Albums** and **Songs** — from CSV files, then lets you curate each artist's **genre**, **sub-genre**, **favorite** status, and **rating** (1–5).

- **Backend:** ASP.NET Core Minimal API + Entity Framework Core
- **Database:** SQL Server LocalDB / Express
- **Frontend:** plain HTML + CSS + vanilla JS (no build step)
- **CSV parsing:** [CsvHelper](https://joshclose.github.io/CsvHelper/) with flexible, case-insensitive header matching
- **Upload type:** you pick *artists / albums / songs* at upload time

---

## Prerequisites

1. **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
2. **SQL Server LocalDB** (ships with Visual Studio, or install
   [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)).

   To use a different SQL Server instance, edit the `Default` connection string
   in `appsettings.json`.

---

## Run it

```bash
cd MusicLibrary
dotnet restore
dotnet run
```

Then open the URL shown in the console (e.g. `https://localhost:7080`).

The database (`MusicLibrary`) is created automatically on first launch via
`EnsureCreated()` — no migration step required.

> **Try it immediately:** sample files are in `sample-data/`.
> Upload `artists.csv` first, then `albums.csv`, then `songs.csv`.

---

## Publish to GitHub

A helper script is included. From the project folder:

```bash
chmod +x setup.sh
./setup.sh            # private repo
./setup.sh --public   # public repo
```

It initializes git, commits, and (if the [GitHub CLI](https://cli.github.com/)
is installed) creates the repo and pushes automatically. Without `gh`, it walks
you through creating an empty repo and pushing to it.

---

## How it works

### Upload
Choose a file type (Artists / Albums / Songs) and a `.csv` file, then click **Upload**.
Albums and Songs link to artists **by name** — if a referenced artist doesn't
exist yet, it is created automatically. Songs can optionally name an album.

### Curate
Each artist row lets you edit **Genre** and **Sub-genre**, toggle the **♥ favorite**
heart, and click the **★ stars** to set a 1–5 rating. Click **Save** to persist.
**Details** expands to list the artist's albums and songs.

### Expected CSV columns

Header names are matched **case-insensitively** and ignore spaces, so
`Sub Genre`, `subgenre`, and `SubGenre` all work. Unknown columns are ignored.

| Type    | Required            | Optional                                   |
|---------|---------------------|--------------------------------------------|
| Artists | `Name`              | `Genre`, `SubGenre`, `IsFavorite`, `Rating`|
| Albums  | `Artist`, `Title`*  | `Year`                                     |
| Songs   | `Artist`, `Title`*  | `Album`, `Duration` (seconds or `mm:ss`)   |

\* `Album` is also accepted as the title column header for the Albums file, and
`Song`/`Track` for the Songs file.

`IsFavorite` accepts `true/yes/y/1`. `Rating` accepts `1`–`5` (others ignored).

---

## API reference

| Method | Route                  | Purpose                                            |
|--------|------------------------|----------------------------------------------------|
| POST   | `/api/upload`          | Multipart upload. Form fields: `file`, `type`.     |
| GET    | `/api/artists`         | List artists. Query: `?search=`, `?favorites=true` |
| GET    | `/api/artists/{id}`    | Artist detail incl. albums + songs                 |
| PUT    | `/api/artists/{id}`    | Update genre, sub-genre, favorite, rating          |
| DELETE | `/api/artists/{id}`    | Delete artist (cascades albums + songs)            |

---

## Using EF Core migrations instead of EnsureCreated (optional)

For a production-style schema workflow, swap `EnsureCreated()` in `Program.cs`
for migrations:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Then replace the `db.Database.EnsureCreated();` call with
`db.Database.Migrate();`.

---

## Project layout

```
MusicLibrary/
├─ Program.cs                 # Minimal API endpoints + startup
├─ appsettings.json           # Connection string
├─ Models/                    # Artist, Album, Song
├─ Data/MusicContext.cs       # EF Core DbContext
├─ Services/CsvImportService.cs
├─ wwwroot/                   # index.html, app.js, styles.css
└─ sample-data/               # Example CSVs
```
