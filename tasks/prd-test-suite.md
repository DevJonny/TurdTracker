# PRD: Comprehensive Test Suite

## Introduction

TurdTracker has zero test coverage. This feature adds a complete test suite covering unit tests (services, models, merge engine), component tests (Blazor pages and shared components), and integration tests (sync flow with mocked dependencies). A GitHub Actions CI workflow ensures tests run on every PR.

## Goals

- Achieve comprehensive test coverage across all services, components, and pages
- Catch regressions before they ship via automated CI
- Provide confidence for future refactoring and feature work
- Validate all sync state machine transitions and error handling paths

## User Stories

### US-001: Create test project and CI workflow
**Description:** As a developer, I need a test project with the right dependencies and a CI workflow so tests run automatically on PRs.

**Acceptance Criteria:**
- [ ] New xUnit test project at `tests/TurdTracker.Tests/TurdTracker.Tests.csproj`
- [ ] References: xUnit, bUnit, FluentAssertions, Blazored.LocalStorage, MudBlazor (no mocking framework — use hand-rolled mocks)
- [ ] Project references `src/TurdTracker/TurdTracker.csproj`
- [ ] `dotnet test` runs successfully with zero tests (project compiles)
- [ ] GitHub Actions workflow at `.github/workflows/test.yml` triggers on PRs to `main`
- [ ] Workflow runs `dotnet test` and fails the PR check if any test fails
- [ ] Typecheck passes

### US-002: MergeEngine unit tests
**Description:** As a developer, I want comprehensive tests for the merge engine so that conflict resolution logic is verified.

**Acceptance Criteria:**
- [ ] Test: empty local + empty remote → empty result, no changes flagged
- [ ] Test: local-only entries → merged list contains them, remoteChanged=true
- [ ] Test: remote-only entries → merged list contains them, localChanged=true
- [ ] Test: both have same entry, local newer → local wins, remoteChanged=true
- [ ] Test: both have same entry, remote newer → remote wins, localChanged=true
- [ ] Test: both have same entry, equal LastModified → local wins (tie-break), remoteChanged=true
- [ ] Test: soft-deleted entry older than 90 days → purged from result, both changed flags true
- [ ] Test: soft-deleted entry younger than 90 days → kept in result
- [ ] Test: mix of local-only, remote-only, conflicts → all resolved correctly
- [ ] All tests pass

### US-003: DiaryService unit tests
**Description:** As a developer, I want tests for DiaryService CRUD operations, soft-delete, and event firing.

**Acceptance Criteria:**
- [ ] Test: AddAsync stores entry, sets LastModified to UtcNow, fires OnDataChanged
- [ ] Test: GetAllAsync filters out IsDeleted entries
- [ ] Test: GetAllIncludingDeletedAsync returns all entries including deleted
- [ ] Test: GetByIdAsync returns correct entry or null
- [ ] Test: GetByDateAsync returns entries matching the date
- [ ] Test: UpdateAsync updates entry, sets LastModified, fires OnDataChanged
- [ ] Test: UpdateAsync with non-existent ID does not fire OnDataChanged
- [ ] Test: DeleteAsync sets IsDeleted=true and LastModified, fires OnDataChanged
- [ ] Test: ReplaceAllAsync replaces all entries, does NOT fire OnDataChanged
- [ ] Test: BackfillLastModifiedIfNeeded migrates entries with default(DateTime) LastModified
- [ ] Hand-rolled FakeLocalStorageService implementing ILocalStorageService
- [ ] All tests pass

### US-004: ThemeService unit tests
**Description:** As a developer, I want tests for ThemeService to verify localStorage read/write and default behavior.

**Acceptance Criteria:**
- [ ] Test: GetIsDarkModeAsync returns true when localStorage has no value (default)
- [ ] Test: GetIsDarkModeAsync returns stored value when present
- [ ] Test: SetIsDarkModeAsync writes to localStorage
- [ ] Reuse FakeLocalStorageService from US-003
- [ ] All tests pass

### US-005: GoogleAuthService unit tests
**Description:** As a developer, I want tests for GoogleAuthService to verify initialization, sign-in/out, and token management.

**Acceptance Criteria:**
- [ ] Test: InitializeAsync calls JS googleAuth.initialize once (idempotent)
- [ ] Test: SignInAsync calls EnsureInitializedAsync then JS googleAuth.signIn
- [ ] Test: SignOutAsync calls JS googleAuth.signOut (no initialization needed)
- [ ] Test: IsSignedInAsync calls EnsureInitializedAsync then JS googleAuth.isSignedIn
- [ ] Test: GetAccessTokenAsync returns token from JS
- [ ] Test: TrySilentSignInAsync returns true when token obtained, false otherwise
- [ ] Hand-rolled FakeJSRuntime implementing IJSRuntime (or use bUnit's built-in JSInterop)
- [ ] All tests pass

### US-006: GoogleDriveService unit tests
**Description:** As a developer, I want tests for GoogleDriveService to verify Drive API interactions.

**Acceptance Criteria:**
- [ ] Test: FindSyncFileAsync returns (fileId, etag) when file exists
- [ ] Test: FindSyncFileAsync returns (null, null) when no file found
- [ ] Test: DownloadSyncFileAsync deserializes SyncEnvelope correctly
- [ ] Test: UploadSyncFileAsync creates new file when fileId is null
- [ ] Test: UploadSyncFileAsync updates existing file with If-Match etag header
- [ ] Test: UploadSyncFileAsync throws HttpRequestException on 412 conflict
- [ ] Hand-rolled FakeHttpMessageHandler and FakeGoogleAuthService
- [ ] All tests pass

### US-007: SyncService unit tests — happy path and state machine
**Description:** As a developer, I want tests for the SyncService sync flow and status transitions.

**Acceptance Criteria:**
- [ ] Test: InitializeAsync with signed-in user → triggers SyncAsync, status transitions to Synced
- [ ] Test: InitializeAsync with no previous session → status stays NotSignedIn
- [ ] Test: SyncAsync when not signed in → sets NotSignedIn status
- [ ] Test: SyncAsync happy path → Syncing → Synced, sets LastSyncedUtc
- [ ] Test: SyncAsync guard — concurrent call returns immediately (no double-sync)
- [ ] Test: SyncAsync with LocalChanged → calls ReplaceAllAsync, fires OnDataMerged
- [ ] Test: SyncAsync with RemoteChanged → calls UploadSyncFileAsync
- [ ] Test: OnDataMerged subscriber exception does not fail sync
- [ ] Hand-rolled FakeDiaryService, FakeGoogleAuthService, FakeGoogleDriveService
- [ ] All tests pass

### US-008: SyncService unit tests — error handling and retry
**Description:** As a developer, I want tests for SyncService error paths, retry logic, and debounce.

**Acceptance Criteria:**
- [ ] Test: 412 conflict → retries with re-download and re-merge (up to 3 times)
- [ ] Test: 401 unauthorized → attempts silent re-auth and retries
- [ ] Test: Network error (StatusCode=null) → reverts to previous status, no error shown
- [ ] Test: Real HTTP error (e.g. 500) → sets SyncStatus=Error, captures LastError
- [ ] Test: General exception → sets SyncStatus=Error, captures LastError
- [ ] Test: LastError cleared at start of each SyncAsync call
- [ ] Test: Debounce cancels previous pending sync when new change arrives
- [ ] Test: OnDataChanged unsubscribe/resubscribe around ReplaceAllAsync is guaranteed (even on exception)
- [ ] Test: Dispose cancels and disposes debounce CTS
- [ ] All tests pass

### US-009: BristolScaleSelector component tests
**Description:** As a developer, I want tests for the BristolScaleSelector component to verify rendering and selection.

**Acceptance Criteria:**
- [ ] Test: Renders all 7 Bristol type cards with correct names
- [ ] Test: Selected type card has "bristol-card-selected" class
- [ ] Test: Clicking a card invokes SelectedTypeChanged callback with correct value
- [ ] Test: No card selected when SelectedType=0
- [ ] bUnit test context with MudBlazor services registered
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-010: TagInput component tests
**Description:** As a developer, I want tests for the TagInput component to verify tag add/remove and recent tags.

**Acceptance Criteria:**
- [ ] Test: Adding a tag via Enter key invokes TagsChanged callback
- [ ] Test: Duplicate tag (case-insensitive) is not added
- [ ] Test: Removing a tag invokes TagsChanged callback
- [ ] Test: Text field cleared after adding a tag
- [ ] Test: Recent tags loaded from DiaryService on init (top 10, deduped, excluding current)
- [ ] Reuse FakeDiaryService for recent tags data
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-011: Home page component tests
**Description:** As a developer, I want tests for the Home page to verify entry display, navigation, and sync banner.

**Acceptance Criteria:**
- [ ] Test: Empty entries shows "No entries yet" message and "Log Entry" button
- [ ] Test: Entries rendered as cards with Bristol type, timestamp, truncated notes (80 chars), tags
- [ ] Test: Clicking entry card navigates to `/entry/{Id}`
- [ ] Test: Sync banner shown when not signed in and not dismissed
- [ ] Test: Sync banner hidden when signed in
- [ ] Test: Sync banner hidden when previously dismissed
- [ ] Test: Subscribes to OnDataMerged and refreshes entries on event
- [ ] Test: Disposes OnDataMerged subscription
- [ ] Reuse hand-rolled fakes for IDiaryService, IGoogleAuthService, ISyncService, ILocalStorageService
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-012: Calendar page component tests
**Description:** As a developer, I want tests for the Calendar page to verify month navigation, date selection, and entry display.

**Acceptance Criteria:**
- [ ] Test: Calendar grid shows correct number of days for current month
- [ ] Test: Day-of-week offset is correct (Monday start)
- [ ] Test: Entry count badges shown on dates with entries
- [ ] Test: Clicking a date selects it and shows entries for that day
- [ ] Test: Previous/next month buttons navigate months
- [ ] Test: Empty selected date shows "No entries for this day"
- [ ] Test: Subscribes to OnDataMerged and refreshes on event
- [ ] Reuse hand-rolled fakes for IDiaryService, ISyncService
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-013: Stats page component tests
**Description:** As a developer, I want tests for the Stats page to verify chart data computation.

**Acceptance Criteria:**
- [ ] Test: Frequency chart computes correct daily counts for 7-day range
- [ ] Test: Bristol distribution chart tallies types 1–7 correctly
- [ ] Test: Time of day chart buckets entries by hour (0–23)
- [ ] Test: Time range chip selection (7/30/90 days) rebuilds frequency chart
- [ ] Test: Empty data renders without errors
- [ ] Test: Subscribes to OnDataMerged and rebuilds charts on event
- [ ] Reuse hand-rolled fakes for IDiaryService, ISyncService
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-014: Log page component tests
**Description:** As a developer, I want tests for the Log page to verify entry creation and validation.

**Acceptance Criteria:**
- [ ] Test: Save with BristolType=0 shows validation error, does not call AddAsync
- [ ] Test: Save with valid data calls AddAsync with correct entry and navigates to "/"
- [ ] Test: Entry timestamp combines selected date and time
- [ ] Test: Cancel button navigates to "/"
- [ ] Reuse FakeDiaryService; use bUnit's FakeNavigationManager
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-015: EntryDetail page component tests
**Description:** As a developer, I want tests for the EntryDetail page to verify view, edit, and delete flows.

**Acceptance Criteria:**
- [ ] Test: Loads and displays entry by ID (Bristol type, timestamp, notes, tags)
- [ ] Test: Entry not found shows error message
- [ ] Test: Edit button toggles to edit form with pre-populated fields
- [ ] Test: Save in edit mode calls UpdateAsync and exits edit mode
- [ ] Test: Cancel in edit mode exits without saving
- [ ] Test: Delete shows confirmation dialog; confirming calls DeleteAsync and navigates to "/"
- [ ] Reuse FakeDiaryService; use bUnit's FakeNavigationManager; hand-rolled FakeDialogService
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-016: Settings page component tests
**Description:** As a developer, I want tests for the Settings page to verify auth flows and sync controls.

**Acceptance Criteria:**
- [ ] Test: Sign in button calls SignInAsync, on success sets signed-in state and calls SyncAsync
- [ ] Test: Sign out button calls SignOutAsync then SyncAsync
- [ ] Test: Sync Now button calls SyncAsync
- [ ] Test: Buttons disabled while SyncStatus == Syncing
- [ ] Test: Sync status text and icon reflect current SyncStatus
- [ ] Test: LastSyncedUtc displayed when available
- [ ] Reuse hand-rolled fakes for IGoogleAuthService, ISyncService, IThemeService
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

### US-017: Export page component tests
**Description:** As a developer, I want tests for the Export page to verify date filtering and export trigger.

**Acceptance Criteria:**
- [ ] Test: All entries shown when no date filter applied
- [ ] Test: Start date filter excludes entries before that date
- [ ] Test: End date filter excludes entries after that date
- [ ] Test: Both filters combined works correctly
- [ ] Test: Export button calls window.print via JS interop
- [ ] Test: Subscribes to OnDataMerged and refreshes on event
- [ ] Reuse hand-rolled fakes for IDiaryService, ISyncService; use bUnit's JSInterop
- [ ] All tests pass

### US-018: MainLayout component tests
**Description:** As a developer, I want tests for the MainLayout to verify sync icon states, theme toggle, and navigation.

**Acceptance Criteria:**
- [ ] Test: Sync icon is CloudDone/Success when Synced
- [ ] Test: Sync icon is CloudSync/Inherit when Syncing, with spin class
- [ ] Test: Sync icon is CloudOff/Error when Error
- [ ] Test: Sync icon is CloudOff/Default when NotSignedIn
- [ ] Test: Clicking sync icon during Error shows snackbar with LastError
- [ ] Test: OnDataMerged shows "Sync complete" snackbar only when status is Synced/Idle
- [ ] Test: Theme toggle switches dark/light mode
- [ ] Test: Tooltip shows status and LastSyncedUtc
- [ ] Reuse hand-rolled fakes for ISyncService, IThemeService; hand-rolled FakeSnackbar
- [ ] All tests pass
- [ ] Verify in browser using dev-browser skill

## Functional Requirements

- FR-1: Test project must use xUnit as the test framework
- FR-2: Blazor component tests must use bUnit
- FR-3: All service dependencies must use hand-rolled fakes implementing the real interfaces (no mocking framework — no Moq, NSubstitute, etc.)
- FR-4: Tests must be deterministic — no dependence on system clock, network, or external state
- FR-5: GitHub Actions workflow must run `dotnet test` on every PR to `main`
- FR-6: Tests must complete in under 60 seconds total
- FR-7: Test file naming convention: `{ClassUnderTest}Tests.cs` in a mirrored folder structure
- FR-8: Each test method should test one behavior and have a descriptive name

## Non-Goals

- End-to-end browser automation tests (Playwright/Selenium)
- Performance/load testing
- Visual snapshot testing
- Testing third-party libraries (MudBlazor, Blazored.LocalStorage internals)
- Testing JS interop modules themselves (google-auth.js)

## Technical Considerations

- **Hand-rolled fakes only** — no Moq, NSubstitute, or any mocking framework. Each interface gets a `Fake*` class (e.g. `FakeDiaryService`, `FakeGoogleAuthService`) in a shared `Fakes/` folder in the test project. Fakes should store calls for assertion and allow configuring return values.
- Blazor WASM components render on the client — bUnit provides a test host that simulates this
- MudBlazor components need `AddMudServices()` in the bUnit test context for proper rendering
- `HttpClient` faking: use a hand-rolled `FakeHttpMessageHandler` extending `HttpMessageHandler` to intercept requests
- `IJSRuntime`: bUnit provides built-in `JSInterop` for setting up expected JS calls — use this rather than hand-rolling
- `ILocalStorageService` from Blazored: hand-roll `FakeLocalStorageService` backed by a `Dictionary<string, object>`
- `DateTime.UtcNow` usage in services means tests should verify timestamps are "recent" rather than exact matches
- MergeEngine is static and pure — no fakes needed, test directly
- No code coverage tooling — skip for now

## Success Metrics

- All tests pass on `dotnet test`
- CI workflow blocks PRs with failing tests
- Every public service method has at least one test
- Every page has tests for its core user interactions
- Sync state machine has tests for every transition and error path

## Open Questions

- None — all questions resolved.
