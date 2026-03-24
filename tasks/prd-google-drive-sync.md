# PRD: Google Drive Sync

## Introduction

TurdTracker stores all diary entries in browser LocalStorage, which means data is trapped on a single device and lost if storage is cleared. This feature adds automatic cloud sync via Google Drive so users can access their diary across multiple devices (phone, laptop, tablet) with a unified, merged dataset. Sync uses Google's hidden `appDataFolder` for privacy, an additive merge strategy to combine entries from all devices, and soft deletes to propagate removals correctly.

## Goals

- Enable multi-device access with automatic, seamless data synchronization
- Merge entries additively across devices so no data is lost
- Resolve edit conflicts deterministically using latest-timestamp-wins
- Propagate deletions across devices via soft deletes
- Maintain full offline functionality — sync is best-effort when online
- Prompt new users to connect Google Drive on first visit

## User Stories

### US-001: Add sync metadata to diary entries
**Description:** As a developer, I need to track when entries were last modified and whether they've been soft-deleted, so the sync engine can merge and resolve conflicts correctly.

**Acceptance Criteria:**
- [ ] `DiaryEntry` model has `DateTime LastModified` property (defaults to `DateTime.UtcNow`)
- [ ] `DiaryEntry` model has `bool IsDeleted` property (defaults to `false`)
- [ ] Existing entries in LocalStorage are migrated on first load — `LastModified` is backfilled from `Timestamp.ToUniversalTime()` where it is `default`
- [ ] `SyncEnvelope` model created with `Version`, `LastSyncedUtc`, and `List<DiaryEntry> Entries`
- [ ] `dotnet build` succeeds with no errors

### US-002: Update DiaryService for soft deletes and change tracking
**Description:** As a developer, I need the diary service to use soft deletes instead of hard deletes, track modification times, and notify listeners when data changes, so the sync engine can operate correctly.

**Acceptance Criteria:**
- [ ] `GetAllAsync()` filters out entries where `IsDeleted == true`
- [ ] `AddAsync()` sets `LastModified = DateTime.UtcNow` before saving and fires `OnDataChanged`
- [ ] `UpdateAsync()` sets `LastModified = DateTime.UtcNow` before saving and fires `OnDataChanged`
- [ ] `DeleteAsync()` sets `IsDeleted = true` and `LastModified = DateTime.UtcNow` instead of removing the entry, and fires `OnDataChanged`
- [ ] New method `GetAllIncludingDeletedAsync()` returns all entries without filtering
- [ ] New method `ReplaceAllAsync(List<DiaryEntry>)` overwrites the full LocalStorage key
- [ ] New event `Action? OnDataChanged` is declared on the interface and fired after every mutation
- [ ] Existing UI behavior is unchanged — soft-deleted entries are invisible to all pages
- [ ] `dotnet build` succeeds with no errors

### US-003: Google OAuth sign-in via JS interop
**Description:** As a user, I want to sign in with my Google account so TurdTracker can access my Google Drive for syncing.

**Acceptance Criteria:**
- [ ] JavaScript module `google-auth.js` dynamically loads Google Identity Services library
- [ ] JS module exposes `initializeGoogleAuth`, `signIn`, `signOut`, `getAccessToken`, `isSignedIn` functions
- [ ] Token client uses scope `https://www.googleapis.com/auth/drive.appdata` (minimal permission)
- [ ] Client ID `101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com` is hardcoded
- [ ] Access token is held in JS memory only, not persisted to storage
- [ ] C# `IGoogleAuthService` / `GoogleAuthService` wraps all JS calls via `IJSRuntime`
- [ ] Script tag added to `index.html`
- [ ] `dotnet build` succeeds with no errors

### US-004: Google Drive API service
**Description:** As a developer, I need a service that reads and writes the sync file to Google Drive's appDataFolder, so the sync engine can exchange data with the cloud.

**Acceptance Criteria:**
- [ ] `IGoogleDriveService` / `GoogleDriveService` created with `HttpClient` and Bearer token auth
- [ ] `FindSyncFileAsync()` queries Drive for `turdtracker-sync.json` in `appDataFolder` and returns file ID + version
- [ ] `DownloadSyncFileAsync(fileId)` downloads and deserializes the file into a `SyncEnvelope`
- [ ] `UploadSyncFileAsync(envelope, fileId?)` creates a new file (if no fileId) or updates existing file via multipart upload
- [ ] Upload uses `If-Match` header with file version for optimistic concurrency control
- [ ] Returns `412 Precondition Failed` correctly when the remote file was modified by another device
- [ ] `dotnet build` succeeds with no errors

### US-005: Merge engine and sync orchestrator
**Description:** As a user, I want my diary entries from all devices to be merged automatically so I see a complete, unified dataset everywhere.

**Acceptance Criteria:**
- [ ] Merge algorithm indexes local and remote entries by `Guid`
- [ ] Entries only in local are kept; entries only in remote are kept (additive)
- [ ] Entries in both sets: the one with later `LastModified` wins
- [ ] Soft-deleted entries are included in the merged result (UI filters them out)
- [ ] Tombstones (soft-deleted entries) older than 90 days are purged during merge
- [ ] Sync flow: check auth → download remote → merge → save local if changed → upload if changed
- [ ] On `412 Precondition Failed`, re-download, re-merge, and retry (up to 3 attempts)
- [ ] `SyncStatus` enum exposed: `NotSignedIn`, `Idle`, `Syncing`, `Synced`, `Error`
- [ ] `OnSyncStatusChanged` event fires on every status transition
- [ ] Auto-sync triggers on `DiaryService.OnDataChanged` with 2-second debounce
- [ ] Auto-sync triggers on app startup (if signed in)
- [ ] Auto-sync triggers when device comes back online (`navigator.onLine`)
- [ ] Sync skips silently when offline — no error shown
- [ ] If access token is expired (401 from Drive), attempt silent re-auth before failing
- [ ] `dotnet build` succeeds with no errors

### US-006: Sync status indicator in app bar
**Description:** As a user, I want to see the current sync status at a glance in the app bar so I know whether my data is up to date.

**Acceptance Criteria:**
- [ ] Sync icon appears in the app bar between the spacer and the theme toggle button
- [ ] `Synced` state: green CloudDone icon
- [ ] `Syncing` state: CloudSync icon with rotation animation
- [ ] `Error` state: red CloudOff icon; clicking shows a snackbar with error details
- [ ] `NotSignedIn` state: grey CloudOff icon
- [ ] Hovering/tapping the sync icon shows a tooltip with the last synced timestamp
- [ ] Icon updates reactively when `SyncService.OnSyncStatusChanged` fires
- [ ] On first render, if user is signed in, initial sync is triggered
- [ ] `dotnet build` succeeds with no errors
- [ ] Verify in browser using dev-browser skill

### US-007: Google Drive sync section on Settings page
**Description:** As a user, I want to manage my Google Drive connection from the Settings page so I can sign in, see sync status, manually sync, and sign out.

**Acceptance Criteria:**
- [ ] New "Google Drive Sync" section appears between Appearance and Data sections
- [ ] When not signed in: shows a "Sign in with Google" button
- [ ] Clicking "Sign in with Google" initiates OAuth flow and triggers sync on success
- [ ] When signed in: shows sync status text, last sync time, "Sync Now" button, and "Sign Out" button
- [ ] "Sync Now" button triggers `SyncService.SyncAsync()` immediately
- [ ] "Sign Out" button calls `GoogleAuthService.SignOutAsync()` and updates UI
- [ ] `dotnet build` succeeds with no errors
- [ ] Verify in browser using dev-browser skill

### US-008: First-visit sync onboarding prompt
**Description:** As a new user, I want to be prompted to connect Google Drive on my first visit so I know the sync feature exists and can set it up early.

**Acceptance Criteria:**
- [ ] On first app load (no prior sign-in), a MudAlert banner appears at the top of the Home page encouraging connecting Google Drive
- [ ] Banner includes a "Connect Google Drive" action button and a "Dismiss" option
- [ ] Dismissing the banner saves a flag to LocalStorage so it does not reappear
- [ ] Banner does not block any app functionality — user can ignore it and use the app normally
- [ ] `dotnet build` succeeds with no errors
- [ ] Verify in browser using dev-browser skill

### US-009: DI registration and wiring
**Description:** As a developer, I need all new services registered in the DI container so they are available throughout the app.

**Acceptance Criteria:**
- [ ] `Program.cs` registers `IGoogleAuthService` / `GoogleAuthService` as scoped
- [ ] `Program.cs` registers `IGoogleDriveService` / `GoogleDriveService` as scoped
- [ ] `Program.cs` registers `ISyncService` / `SyncService` as scoped
- [ ] App starts successfully with all services resolved
- [ ] `dotnet build` succeeds with no errors

## Functional Requirements

- FR-1: The system must add `LastModified` (UTC DateTime) and `IsDeleted` (bool) fields to the `DiaryEntry` model
- FR-2: The system must migrate existing entries on first load by backfilling `LastModified` from `Timestamp`
- FR-3: The system must use soft deletes — setting `IsDeleted = true` instead of removing entries
- FR-4: The system must filter soft-deleted entries from all user-facing queries
- FR-5: The system must fire an `OnDataChanged` event after every add, update, or delete operation
- FR-6: The system must authenticate users via Google OAuth using Google Identity Services with the `drive.appdata` scope
- FR-7: The system must store sync data as a single `turdtracker-sync.json` file in Google Drive's hidden `appDataFolder`
- FR-8: The system must merge local and remote entries additively — entries unique to either side are kept, conflicts resolved by latest `LastModified`
- FR-9: The system must purge soft-deleted tombstone entries older than 90 days during merge
- FR-10: The system must auto-sync on data changes (debounced 2 seconds), on app startup, and when connectivity returns
- FR-11: The system must handle concurrent sync from multiple devices using optimistic concurrency (If-Match/412 retry)
- FR-12: The system must display sync status (Synced/Syncing/Error/NotSignedIn) as an icon in the app bar
- FR-13: The system must provide sign-in, sync-now, and sign-out controls on the Settings page
- FR-14: The system must prompt first-time users to connect Google Drive via a dismissable banner
- FR-15: The system must work fully offline — all features function without connectivity, sync occurs when online

## Non-Goals

- No real-time collaboration or live conflict resolution UI
- No support for non-Google cloud providers (iCloud, OneDrive, Dropbox)
- No encryption of the sync file beyond Google Drive's built-in encryption
- No sharing of diary data between different Google accounts
- No server-side component or backend — everything runs client-side
- No manual conflict resolution dialog — latest timestamp wins automatically
- No sync of theme/settings preferences — only diary entries

## Design Considerations

- Sync icon in app bar should use MudBlazor `MudIconButton` with Material icons (`CloudDone`, `CloudSync`, `CloudOff`)
- Settings page sync section should use `MudPaper` consistent with existing Appearance and Data sections
- Onboarding prompt should be a `MudAlert` or `MudBanner` at the top of the main content area — non-intrusive
- Reuse existing MudBlazor components (MudButton, MudIcon, MudText, MudSnackbar) throughout

## Technical Considerations

- Google Identity Services uses implicit grant flow (no backend needed) — access tokens last 1 hour
- All Google Drive API calls go through browser `fetch` (Blazor WASM HttpClient) — CORS is supported by Google for authorized requests
- JS interop required for Google Identity Services library (no .NET SDK available for browser OAuth)
- Access tokens stored in JS memory only (not LocalStorage) for security — user re-authenticates on page reload (GIS often grants silently)
- `appDataFolder` is a special Drive space that only the app can access — user cannot see the file in their Drive UI
- Blazor WASM scoped services live for the browser tab lifetime — `SyncService` can hold debounce state in memory
- Client ID: `101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com`

## Success Metrics

- User can log an entry on device A and see it on device B within 10 seconds of opening the app
- Deleting an entry on one device removes it from all devices after sync
- Editing the same entry on two devices offline results in the latest edit winning after both sync
- App remains fully functional with no errors when offline
- Sync completes without data loss under concurrent usage from 2+ devices

## Open Questions

None — all resolved.
