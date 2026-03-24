# PRD: Google Drive Sync — PR Review Bug Fixes

## Introduction

Copilot reviewed PR #1 (Google Drive sync feature) and identified 15 issues ranging from critical data corruption bugs to UX polish. This PRD covers the 11 actionable fixes (2 items are deferred). These fixes address broken conflict resolution, concurrency protection failures, resource leaks, race conditions, and missing UI feedback.

## Goals

- Fix critical bugs that would cause incorrect merge behavior and data loss
- Improve reliability of sync orchestration (error handling, resource cleanup)
- Eliminate race conditions in auth initialization
- Prevent token leakage to non-Drive HTTP requests
- Improve sync UX feedback (animation, error messages, sign-out status)

## User Stories

### US-001: Fix LastModified deserialization for legacy entries
**Description:** As a developer, I need legacy diary entries (created before sync was added) to deserialize with `default(DateTime)` for `LastModified`, so the backfill migration in DiaryService correctly sets `LastModified` from `Timestamp` instead of silently setting it to "now".

**Acceptance Criteria:**
- [ ] `DiaryEntry.LastModified` property has no initializer (no `= DateTime.UtcNow`)
- [ ] `DiaryService.AddAsync()` still explicitly sets `LastModified = DateTime.UtcNow` before saving
- [ ] Entries deserialized from LocalStorage that lack a `LastModified` field get `default(DateTime)` (0001-01-01)
- [ ] `BackfillLastModifiedIfNeeded` detects `default(DateTime)` and backfills from `Timestamp.ToUniversalTime()`
- [ ] Typecheck passes

### US-002: Use ETag instead of Version for Drive concurrency control
**Description:** As a developer, I need the Google Drive service to use `etag` (not `version`) for the `If-Match` header, because the Drive API's `If-Match` header expects an ETag value for optimistic concurrency.

**Acceptance Criteria:**
- [ ] `FindSyncFileAsync` requests `fields=files(id,etag)` instead of `fields=files(id,version)`
- [ ] Upload URLs use `fields=id,etag` instead of `fields=id,version`
- [ ] `FileResponse` model uses `[JsonPropertyName("etag")]` property named `ETag`
- [ ] `IGoogleDriveService` return tuples use `ETag` instead of `Version`
- [ ] `SyncService.PerformSyncAsync` passes `etag` (not `version`) to upload calls
- [ ] `If-Match` header in `UpdateFileAsync` uses the etag value
- [ ] Typecheck passes

### US-003: Fix tombstone purge not persisting
**Description:** As a developer, I need the merge engine to mark `LocalChanged` and `RemoteChanged` as true when tombstones are purged, so the purge actually gets saved to local storage and uploaded to Drive.

**Acceptance Criteria:**
- [ ] `MergeEngine.Merge` captures the count returned by `RemoveAll` for tombstone purge
- [ ] If any tombstones were purged (`count > 0`), both `localChanged` and `remoteChanged` are set to `true`
- [ ] Typecheck passes

### US-004: Guarantee re-subscription after ReplaceAllAsync
**Description:** As a developer, I need the sync service to re-subscribe to `OnDataChanged` even if `ReplaceAllAsync` throws, so auto-sync doesn't silently stop for the rest of the session.

**Acceptance Criteria:**
- [ ] `PerformSyncAsync` wraps the unsubscribe/ReplaceAllAsync/resubscribe block in try/finally
- [ ] The `finally` block always re-subscribes `_diaryService.OnDataChanged += OnDataChanged`
- [ ] Typecheck passes

### US-005: Fix CancellationTokenSource leak in debounce
**Description:** As a developer, I need the debounce handler to dispose the previous `CancellationTokenSource` before creating a new one, to prevent resource leaks from repeated edits.

**Acceptance Criteria:**
- [ ] `OnDataChanged` stores a reference to the previous CTS before canceling
- [ ] Previous CTS is disposed after canceling and before creating the replacement
- [ ] Typecheck passes

### US-006: Distinguish offline errors from real HTTP failures
**Description:** As a user, I want real sync errors (403, 429, 500) to show as errors instead of being silently swallowed as "offline", so I know when something is actually wrong.

**Acceptance Criteria:**
- [ ] `SyncAsync` catches `HttpRequestException` and checks `ex.StatusCode`
- [ ] When `StatusCode` is null (network/offline failure), sync skips silently and reverts to previous state
- [ ] When `StatusCode` has a value (real HTTP error like 403/429/500), `SyncStatus` is set to `Error`
- [ ] Typecheck passes

### US-007: Lazy-initialize Google Auth to prevent race conditions
**Description:** As a user, I want sign-in to work from the Home banner and Settings page even if the layout hasn't finished initializing yet, so I don't get a "not initialized" JS error.

**Acceptance Criteria:**
- [ ] `GoogleAuthService` has a private `_initialized` bool flag (default false)
- [ ] `InitializeAsync` sets `_initialized = true` after calling JS initialize (idempotent — skips if already initialized)
- [ ] `SignInAsync`, `IsSignedInAsync`, and `GetAccessTokenAsync` call `EnsureInitializedAsync()` which calls `InitializeAsync()` if `_initialized` is false
- [ ] Clicking "Sign in with Google" on Settings page works even before `MainLayout.OnAfterRenderAsync` completes
- [ ] Clicking "Connect Google Drive" on Home banner works even before layout init
- [ ] Typecheck passes

### US-008: Set auth header per-request instead of on shared HttpClient
**Description:** As a developer, I need the Google Drive service to set the Authorization header on individual requests rather than on the shared HttpClient's DefaultRequestHeaders, to prevent the Bearer token from leaking to non-Drive requests.

**Acceptance Criteria:**
- [ ] `SetAuthHeaderAsync` is replaced with `GetAuthTokenAsync()` that returns the token string
- [ ] Each Drive API method creates an `HttpRequestMessage` and sets `Authorization = new AuthenticationHeaderValue("Bearer", token)` on the request
- [ ] If the token is null or empty, an `InvalidOperationException` is thrown with a clear message
- [ ] `_httpClient.DefaultRequestHeaders.Authorization` is never set
- [ ] `FindSyncFileAsync` and `DownloadSyncFileAsync` use `HttpRequestMessage` + `SendAsync` instead of `GetAsync`
- [ ] Typecheck passes

### US-009: Sign-out updates sync status in app bar
**Description:** As a user, I want the app bar sync icon to immediately show "Not signed in" (grey) after I sign out, instead of staying green/synced.

**Acceptance Criteria:**
- [ ] After `GoogleAuthService.SignOutAsync()` in Settings, `SyncService.SyncAsync()` is called
- [ ] `SyncAsync` detects not-signed-in and sets `SyncStatus.NotSignedIn`
- [ ] App bar sync icon immediately changes to grey CloudOff after sign-out
- [ ] Typecheck passes
- [ ] Verify in browser using dev-browser skill

### US-010: Apply spin animation to syncing icon
**Description:** As a user, I want the sync icon in the app bar to visually spin while syncing is in progress, so I can see that something is happening.

**Acceptance Criteria:**
- [ ] The sync `MudIconButton` in MainLayout gets a conditional CSS class (e.g., `sync-spinning`) when `SyncStatus == Syncing`
- [ ] The existing `spin` keyframes in MainLayout.razor.css are applied via that class
- [ ] The icon stops spinning when sync completes or errors
- [ ] Typecheck passes
- [ ] Verify in browser using dev-browser skill

### US-011: Show actual error details in sync error snackbar
**Description:** As a user, I want to see what went wrong when sync fails, instead of a generic "check your connection" message, so I can take appropriate action.

**Acceptance Criteria:**
- [ ] `ISyncService` has a `string? LastError` property
- [ ] `SyncService` captures `ex.Message` in catch blocks and stores it in `LastError`
- [ ] `LastError` is cleared at the start of each `SyncAsync` call
- [ ] MainLayout error snackbar displays `SyncService.LastError` when available, falling back to generic message if null
- [ ] Typecheck passes
- [ ] Verify in browser using dev-browser skill

## Functional Requirements

- FR-1: `DiaryEntry.LastModified` must deserialize as `default(DateTime)` for entries that don't have the field, enabling the backfill migration to detect and correct them
- FR-2: Google Drive optimistic concurrency must use the file's `etag` (not `version`) in the `If-Match` header
- FR-3: When the merge engine purges tombstones, both local and remote changed flags must be set to ensure the purge is persisted
- FR-4: The `OnDataChanged` event subscription must be guaranteed to be restored after `ReplaceAllAsync`, even on exceptions
- FR-5: Previous `CancellationTokenSource` instances must be disposed during debounce to prevent resource leaks
- FR-6: `HttpRequestException` with a non-null `StatusCode` must surface as `SyncStatus.Error`, not be silently skipped
- FR-7: Google Auth must be lazily initialized so sign-in works regardless of call site timing
- FR-8: OAuth Bearer tokens must be set per-request, not on shared `HttpClient.DefaultRequestHeaders`
- FR-9: Signing out must immediately update `SyncStatus` to `NotSignedIn`
- FR-10: The sync icon must animate (spin) during the `Syncing` state
- FR-11: Sync error messages must be captured and displayed in the error snackbar

## Non-Goals

- No online/offline event listener (`navigator.onLine`) — deferred, auto-sync on changes covers main cases
- No silent re-auth for token refresh — GIS limitation, interactive prompt is acceptable
- No refactoring beyond what the review comments identified
- No new features — strictly bug fixes and reliability improvements

## Technical Considerations

- `DiaryEntry.LastModified` initializer removal: System.Text.Json runs property initializers during deserialization, so `= DateTime.UtcNow` defeats the backfill check for `default(DateTime)`. Removing the initializer fixes this.
- Google Drive `If-Match` header: Drive documentation specifies ETags for conditional requests. The `version` field is a monotonic counter, not suitable for `If-Match`.
- Per-request auth headers: Blazor WASM's `HttpClient` is shared via DI. Setting `DefaultRequestHeaders.Authorization` leaks credentials to any other code using the same `HttpClient` instance.
- Lazy auth init: Multiple call sites (`MainLayout`, `Settings.razor`, `Home.razor`) can trigger `SignInAsync` — the first to call it must ensure the GIS token client is initialized.

## Success Metrics

- Zero data corruption from legacy entry migration (LastModified correctly backfilled)
- Concurrent sync from two devices correctly detects conflicts via ETag
- Auto-sync never silently stops due to unhandled exceptions
- Real HTTP errors (403/429/500) are surfaced to the user, not hidden
- Sign-in works from any UI entry point without initialization errors

## Open Questions

None — all resolved. Fixes are directly derived from specific code review comments.
