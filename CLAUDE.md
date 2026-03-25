# TurdTracker

Blazor WebAssembly (.NET 10) poop diary app with Google Drive sync. Hosted on GitHub Pages.

## Build

```
DOTNET_CLI_USE_MSBUILD_SERVER=0 DOTNET_HOST_PATH=$(which dotnet) dotnet build src/TurdTracker/TurdTracker.csproj
```

- Requires `wasm-tools` workload: `dotnet workload install wasm-tools`
- NuGet restore needs network — run build with sandbox disabled

## Project layout

All source under `src/TurdTracker/`:
- `Services/` — DI services (scoped): DiaryService, GoogleAuthService, GoogleDriveService, SyncService, ThemeService, MergeEngine (static)
- `Pages/` — Razor pages (Home, Calendar, Stats, Export, Settings, EntryDetail)
- `Components/` — Shared Blazor components
- `Models/` — DiaryEntry, SyncEnvelope
- `Layout/` — MainLayout with sync status indicator
- `wwwroot/js/` — JS interop modules (google-auth.js)

## Key patterns

- DI: `builder.Services.AddScoped<IService, Service>()` in Program.cs
- JS interop: `IJSRuntime.InvokeAsync("googleAuth.method", args)` — no `window.` prefix
- `_Imports.razor` has global `@using TurdTracker.Services`
- UI: MudBlazor v9.2.0 — no `Icons.Material.Filled.Google` available, use `Cloud` instead
- Event-driven UI updates: `InvokeAsync(StateHasChanged)` from service event handlers
- Soft-delete model: `DiaryEntry.IsDeleted` flag, `GetAllAsync()` filters them out
- Sync: SyncService subscribes to `DiaryService.OnDataChanged` with 2s debounce; handles 412 (retry) and 401 (re-auth)
- Google OAuth: GIS token client, access token in JS closure (never localStorage), `google-auth-connected` boolean flag in localStorage for session detection
