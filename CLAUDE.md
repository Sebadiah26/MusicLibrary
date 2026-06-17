# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Workspace Overview

This is a multi-project workspace containing independent projects, not a monorepo. There is no shared build system. Each subdirectory is a separate project with its own tooling.

## Primary Projects

### Sebadiah26/DFSSlateAnalyzerProject (main project, most active)
Multi-layered .NET + Angular solution for DFS slate analysis. Solution file: `DFSSlateAnalyzer.sln`

**Architecture** (layered, with shared core):
- `DFSSlateAnalyzerAPI` — ASP.NET Core Web API backend
- `DFSSlateAnalyzerAngular` — Angular frontend (in `ClientApp/` subdirectory)
- `DFSSlateAnalyzerCore` — Shared business logic library
- `DFSSlateAnalyzerData` — Data access layer
- `DFSSlateAnalyzerService` — Service layer
- `DFSSlateAnalyzerMobile` / `DFSSlateAnalyzerApp` — Mobile/desktop clients
- `StaffManagement` + `StaffManagement.Data` — Staff management module
- `CSVReader` — CSV parsing utility

```
dotnet build DFSSlateAnalyzer.sln
```

### Sebadiah26/SlateAnalyzer
.NET MAUI cross-platform app (Android, iOS, macOS, Windows).

### Sebadiah26/SchoolAppTracker
ASP.NET Core Razor Pages app (.NET 10.0) for school district third-party app tracking. Layered architecture: Web → Core → Data. SQL Server LocalDB, EF Core 10, Bootstrap 5, Google OAuth.
```
dotnet build SchoolAppTracker.slnx
dotnet run --project SchoolAppTracker
```

### Sebadiah26/MusicLibrary
ASP.NET Core Web API with vanilla JS frontend for managing a music library of artists, albums, and songs. EF Core data layer, CSV import service, and sample data.
```
dotnet build MusicLibrary.sln
dotnet run --project MusicLibrary
```

### template-python
Python 3.11+ project using Poetry. Has pytest, mypy, pylint, black, mkdocs.
```
poetry install
poetry run TemplateDemo
```

## Build Commands by Framework

**Angular projects** (angularproject1, DFSAngular, my-app, and ClientApp dirs):
```
npm install
ng serve          # dev server (some configured with --ssl)
ng build          # production build
ng test           # unit tests (Karma)
```

**ASP.NET Core APIs** (DFSAngularAPI, LineupAPI, and API projects within solutions):
```
dotnet restore
dotnet build
dotnet run
```

**Hybrid ASP.NET + Angular** (my-angular, GlobalMarket, DFSSlateAnalyzerAngular):
```
dotnet build      # builds both .NET backend and npm packages
```
The Angular frontend lives in a `ClientApp/` subdirectory and is built automatically via SPA proxy integration.

**.NET MAUI** (MauiApp1, MauiApp2, SlateAnalyzer):
```
dotnet build -f net7.0-windows10.0.19041.0
```

## Tech Stack Summary

| Stack | Versions |
|-------|----------|
| .NET | 6.0–10.0 |
| Angular | 13–14 |
| C# | Primary backend language |
| TypeScript | Frontend language |
| Python | 3.11+ (template-python only) |
| Swagger/OpenAPI | Configured on all .NET APIs |

## Notes

- Most .NET projects include `.sln` files for Visual Studio; use `dotnet build <solution>.sln` to build entire solutions.
- Angular projects use standard Angular CLI. Check individual `angular.json` for project-specific configuration (SSL, ports, etc.).
- The `Python/` directory contains Jupyter notebook experiments (NBA data, stock analysis) — no build system.
- The `React/` directory is an empty scaffold with only a `.sln` file.
