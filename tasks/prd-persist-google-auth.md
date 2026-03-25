# PRD: Persist Google Auth Across Page Reloads

## Introduction

TurdTracker's Google Drive sync requires users to sign in with Google every time they reload the page. The OAuth access token is stored only in a JavaScript module-scoped variable, which is lost on navigation or refresh. This creates a frustrating experience for a PWA that should feel seamless. This feature adds silent token re-acquisition on startup so previously signed-in users are automatically reconnected.

## Goals

- Automatically re-acquire a Google OAuth token on page load for users who have previously signed in
- Eliminate the need for users to manually click "Sign in with Google" after every reload
- Gracefully fall back to "Not Signed In" state if silent re-auth fails (e.g., expired Google session)
- Maintain security by not persisting the access token itself — only a "previously connected" flag

## User Stories

### US-001: Add silent sign-in and session persistence to JS auth module
**Description:** As a developer, I need the JS auth module to support silent token re-acquisition and track whether the user has previously signed in, so the C# layer can attempt auto-reconnection on startup.

**Acceptance Criteria:**
- [ ] `google-auth.js` adds a `trySilentSignIn()` function that calls `tokenClient.requestAccessToken({ prompt: '' })` wrapped in a Promise
- [ ] `trySilentSignIn()` resolves with the access token string on success
- [ ] `trySilentSignIn()` resolves with `null` (does not reject) if silent sign-in fails or popup is blocked — this is expected when the Google session has expired
- [ ] `signIn()` persists `localStorage.setItem('google-auth-connected', 'true')` on successful sign-in
- [ ] `signOut()` removes the flag via `localStorage.removeItem('google-auth-connected')`
- [ ] New `hasPreviousSession()` function returns `localStorage.getItem('google-auth-connected') === 'true'`
- [ ] The access token itself is never stored in localStorage — only the boolean flag
- [ ] Typecheck passes

### US-002: Add silent sign-in methods to C# auth service
**Description:** As a developer, I need C# wrappers for the new JS silent sign-in and session check functions so the sync service can use them on startup.

**Acceptance Criteria:**
- [ ] `IGoogleAuthService` adds `Task<bool> TrySilentSignInAsync()` and `Task<bool> HasPreviousSessionAsync()`
- [ ] `GoogleAuthService.TrySilentSignInAsync()` calls `googleAuth.trySilentSignIn` via JS interop, returns true if a non-null token was obtained
- [ ] `GoogleAuthService.HasPreviousSessionAsync()` calls `googleAuth.hasPreviousSession` via JS interop
- [ ] `TrySilentSignInAsync()` calls `EnsureInitializedAsync()` before the JS interop call
- [ ] Typecheck passes

### US-003: Attempt silent re-auth on app startup
**Description:** As a user, I want the app to automatically reconnect to Google Drive when I reload, so I don't have to sign in again every time.

**Acceptance Criteria:**
- [ ] `SyncService.InitializeAsync()` checks `HasPreviousSessionAsync()` after initializing auth
- [ ] If `HasPreviousSessionAsync()` returns true, calls `TrySilentSignInAsync()`
- [ ] If silent sign-in succeeds (returns true), sets SyncStatus to Idle and triggers sync
- [ ] If silent sign-in fails (returns false), sets SyncStatus to NotSignedIn — user can manually sign in from Settings
- [ ] If `HasPreviousSessionAsync()` returns false, sets SyncStatus to NotSignedIn immediately (no sign-in attempt)
- [ ] No popup or interactive prompt is shown during the silent sign-in attempt
- [ ] Typecheck passes

## Functional Requirements

- FR-1: On successful interactive sign-in, persist a `google-auth-connected` flag in browser localStorage
- FR-2: On sign-out, remove the `google-auth-connected` flag from localStorage
- FR-3: On app startup, if the flag exists, attempt silent token re-acquisition via `requestAccessToken({ prompt: '' })`
- FR-4: If silent re-auth succeeds, proceed with sync as if the user just signed in
- FR-5: If silent re-auth fails (expired session, revoked consent, network error), fall back to NotSignedIn state without showing an error
- FR-6: The OAuth access token must never be stored in localStorage — only the boolean session flag

## Non-Goals

- No long-lived refresh tokens — GIS implicit flow doesn't support them
- No background token refresh timer — tokens are re-acquired on page load only
- No changes to the sign-in or sign-out UI — the mechanism is entirely transparent

## Technical Considerations

- Google Identity Services' `requestAccessToken({ prompt: '' })` requests a token without user interaction. It succeeds if the user has an active Google session and has previously granted consent to this app. It fails silently otherwise.
- The `error_callback` on the token client fires when the popup is blocked or consent is required — `trySilentSignIn` must catch this and resolve with null rather than rejecting.
- The localStorage flag `google-auth-connected` is a simple string, not the token. This is safe to persist.
- GIS access tokens expire after 1 hour. If the user keeps the app open longer, the existing 401-retry logic in SyncService handles re-auth.

## Success Metrics

- Users who have previously signed in are automatically reconnected on page reload without any popup or click
- Zero regressions — manual sign-in and sign-out continue to work as before
- Silent re-auth failure is invisible to the user (no error state, just shows NotSignedIn)

## Open Questions

None — scope is well-defined.
