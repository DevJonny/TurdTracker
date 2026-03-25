# PRD: Sync UI Refresh & Status Visibility

## Introduction

When TurdTracker syncs diary entries from Google Drive (e.g. entries created on another device), the data is correctly downloaded and saved to localStorage, but the UI never updates to show the new entries. Users must manually refresh the page or navigate away and back to see synced data. This makes sync appear broken even though it's working at the data layer.

This PRD addresses the bug and adds better sync status visibility so users can tell what's happening.

## Goals

- Pages automatically refresh their displayed data when sync downloads new/updated entries
- Users can see sync activity and know when new data has arrived
- No sync loops: refreshing page data after sync must not re-trigger another sync
- Zero manual page refreshes required to see synced data

## User Stories

### US-001: Add OnDataMerged event to SyncService
**Description:** As a developer, I need SyncService to expose an event when sync has merged and saved new data locally, so that pages can subscribe and refresh.

**Acceptance Criteria:**
- [ ] `ISyncService` exposes `event Action? OnDataMerged`
- [ ] `SyncService` fires `OnDataMerged` after `ReplaceAllAsync()` completes (only when `localChanged` is true)
- [ ] Event is NOT fired when sync finds no local changes needed
- [ ] Typecheck/build passes

### US-002: Home page refreshes entries after sync
**Description:** As a user, I want the Home page to automatically show new entries from other devices after sync completes, so I don't have to refresh the page.

**Acceptance Criteria:**
- [ ] Home.razor subscribes to `SyncService.OnDataMerged`
- [ ] On merge event, re-fetches entries via `DiaryService.GetAllAsync()` and updates the displayed list
- [ ] Uses `InvokeAsync(StateHasChanged)` pattern for thread-safe UI update
- [ ] Unsubscribes from the event in `Dispose()` to prevent memory leaks
- [ ] Typecheck/build passes
- [ ] **Verify in browser using dev-browser skill**

### US-003: Calendar page refreshes after sync
**Description:** As a user, I want the Calendar page to show synced entries without a page refresh.

**Acceptance Criteria:**
- [ ] Calendar.razor subscribes to `SyncService.OnDataMerged`
- [ ] On merge event, re-fetches and re-renders calendar data
- [ ] Uses `InvokeAsync(StateHasChanged)` pattern
- [ ] Unsubscribes in `Dispose()`
- [ ] Typecheck/build passes
- [ ] **Verify in browser using dev-browser skill**

### US-004: Stats page refreshes after sync
**Description:** As a user, I want the Stats page to reflect synced data without a page refresh.

**Acceptance Criteria:**
- [ ] Stats.razor subscribes to `SyncService.OnDataMerged`
- [ ] On merge event, re-computes statistics from updated data
- [ ] Uses `InvokeAsync(StateHasChanged)` pattern
- [ ] Unsubscribes in `Dispose()`
- [ ] Typecheck/build passes
- [ ] **Verify in browser using dev-browser skill**

### US-005: Export page refreshes after sync
**Description:** As a user, I want the Export page to include synced entries without a page refresh.

**Acceptance Criteria:**
- [ ] Export.razor subscribes to `SyncService.OnDataMerged`
- [ ] On merge event, re-fetches entry data
- [ ] Uses `InvokeAsync(StateHasChanged)` pattern
- [ ] Unsubscribes in `Dispose()`
- [ ] Typecheck/build passes

### US-006: Sync status shows "new data" indicator
**Description:** As a user, I want to see when sync has pulled new data so I know my app is up to date across devices.

**Acceptance Criteria:**
- [ ] After a sync that downloads new data, the sync status briefly shows a distinct state (e.g. snackbar "Sync complete — new entries loaded" or a brief badge/indicator)
- [ ] The indicator auto-dismisses after a few seconds
- [ ] Normal syncs with no new data just show "Synced" as before
- [ ] Typecheck/build passes
- [ ] **Verify in browser using dev-browser skill**

## Functional Requirements

- FR-1: `ISyncService` must expose an `OnDataMerged` event that fires when sync has downloaded and saved new/changed entries locally
- FR-2: `SyncService` must fire `OnDataMerged` only when `MergeResult.LocalChanged` is true after `ReplaceAllAsync()` completes
- FR-3: All pages that display diary data (Home, Calendar, Stats, Export) must subscribe to `OnDataMerged` and re-fetch their data
- FR-4: Page subscriptions must use `InvokeAsync(StateHasChanged)` for thread-safe Blazor rendering
- FR-5: All event subscriptions must be cleaned up in `Dispose()` to prevent memory leaks
- FR-6: Re-fetching data after sync must NOT trigger `OnDataChanged` (which would cause a sync loop) — this is already handled by `ReplaceAllAsync()` not firing `OnDataChanged`
- FR-7: When sync downloads new data, a brief notification must be shown to the user indicating new data was loaded

## Non-Goals

- Real-time push sync (e.g. WebSocket or polling for remote changes)
- Conflict resolution UI (merge engine handles conflicts automatically)
- Per-entry change notifications ("Entry X was updated on your other device")
- Sync progress bar or percentage
- Background/periodic auto-sync (sync is triggered by local changes or manual button)

## Technical Considerations

- The existing pattern of `SyncService` temporarily unsubscribing from `OnDataChanged` during `ReplaceAllAsync()` (to prevent sync loops) must be preserved
- `OnDataMerged` should fire AFTER re-subscription to `OnDataChanged` is restored, to ensure the system is in a consistent state
- Pages already implement `IDisposable` for other event cleanup — follow the same pattern
- MudBlazor `ISnackbar` is already injected in MainLayout for error messages — reuse for "new data" notification
- The `EntryDetail` page shows a single entry and may also need refreshing if the viewed entry was updated by sync

## Success Metrics

- Synced entries appear on all pages without manual refresh
- Users see clear feedback when new data arrives from sync
- No sync loops triggered by UI refresh
- No memory leaks from event subscriptions

## Open Questions

- Should `EntryDetail.razor` also refresh if the currently-viewed entry was updated by sync, or is that an edge case we can defer?
- Should the "new data loaded" notification include a count of new/updated entries?
