# PRD: Turd Tracker — A Poo Diary

## Introduction

Turd Tracker is a Blazor WebAssembly PWA that serves as a personal bowel movement diary. Users log entries with automatic timestamps, select a type on the Bristol Stool Scale (with reference images and descriptions), add optional notes and tags, and review their history via a diary list or calendar view. The app is designed for personal health tracking, with the ability to export data for sharing with healthcare providers. It ships with dark mode as the default theme and includes basic stats and trends.

## Goals

- Provide a fast, frictionless way to log bowel movements with minimal taps
- Display the Bristol Stool Scale with standard medical chart imagery and descriptions
- Store data locally in the browser with a clear path to backend storage later
- Offer both list and calendar views for reviewing history
- Surface trends and patterns through simple stats dashboards
- Work as an installable PWA with offline support, optimised for mobile use
- Default to dark mode with a light mode toggle

## User Stories

### US-001: Log a new entry
**Description:** As a user, I want to quickly log a bowel movement so that I can track my habits over time.

**Acceptance Criteria:**
- [ ] "Log" button/FAB is always accessible from the main screen
- [ ] Date and time are auto-populated to current date/time
- [ ] Date and time can be manually adjusted if needed
- [ ] User must select a Bristol Stool Scale type (1-7) before saving
- [ ] Optional free-text notes field is available
- [ ] Entry saves to browser local storage
- [ ] After saving, user is returned to the diary list with the new entry visible
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-002: Bristol Stool Scale selector
**Description:** As a user, I want to see the Bristol Stool Scale types with images and descriptions so that I can accurately classify my stool.

**Acceptance Criteria:**
- [ ] All 7 Bristol Stool Scale types are displayed
- [ ] Each type shows: type number, standard medical chart image, name, and short description
- [ ] Selected type is visually highlighted
- [ ] Only one type can be selected at a time
- [ ] Selection is required before entry can be saved
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-003: Optional tags on entries
**Description:** As a user, I want to add quick tags to my entries (e.g., "after coffee", "stomach pain") so I can spot correlations later.

**Acceptance Criteria:**
- [ ] Tag input available on the log entry form
- [ ] Common/recent tags shown as quick-select chips
- [ ] User can type custom tags
- [ ] Multiple tags can be added per entry
- [ ] Tags are optional — entry can be saved without them
- [ ] Tags are stored with the entry and displayed in diary views
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-004: Diary list view
**Description:** As a user, I want to see a chronological list of all my entries so I can review my history.

**Acceptance Criteria:**
- [ ] Entries displayed in reverse chronological order (newest first)
- [ ] Each entry shows: date/time, Bristol type (number + icon), tags, and truncated notes
- [ ] Tapping an entry expands or navigates to a detail view
- [ ] List scrolls smoothly with many entries
- [ ] Empty state message shown when no entries exist
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-005: Calendar view
**Description:** As a user, I want to see my entries on a monthly calendar so I can visualise frequency and patterns at a glance.

**Acceptance Criteria:**
- [ ] Monthly calendar grid displayed with navigation between months
- [ ] Days with entries show a count or indicator dot(s)
- [ ] Clicking a day shows entries for that day
- [ ] Current day is visually highlighted
- [ ] Can switch between calendar view and diary list view
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-006: Edit an entry
**Description:** As a user, I want to edit a previously logged entry so I can correct mistakes.

**Acceptance Criteria:**
- [ ] Edit button available on entry detail view
- [ ] All fields (date/time, Bristol type, notes, tags) are editable
- [ ] Changes save to local storage and are immediately reflected in views
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-007: Delete an entry
**Description:** As a user, I want to delete an entry so I can remove incorrect or test entries.

**Acceptance Criteria:**
- [ ] Delete button available on entry detail view
- [ ] Confirmation dialog shown before deletion ("Are you sure?")
- [ ] Entry is removed from local storage and all views
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-008: Stats and trends dashboard
**Description:** As a user, I want to see stats about my bowel habits so I can identify patterns and share insights with my doctor.

**Acceptance Criteria:**
- [ ] Frequency chart: entries per day/week over a selectable time range
- [ ] Type distribution: pie or bar chart showing Bristol type breakdown
- [ ] Time-of-day pattern: chart showing when entries are most commonly logged
- [ ] Dashboard accessible from main navigation
- [ ] Charts render correctly with zero, few, and many entries
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-009: Export data
**Description:** As a user, I want to export my diary data so I can share it with my doctor or keep a backup.

**Acceptance Criteria:**
- [ ] Export button accessible from settings or diary view
- [ ] Exports to PDF with a clean, printable layout
- [ ] Export includes: date/time, Bristol type, notes, and tags for each entry
- [ ] Export can be filtered by date range
- [ ] Typecheck/build passes

### US-010: Dark mode (default) with light mode toggle
**Description:** As a user, I want the app to default to dark mode and let me switch to light mode if preferred.

**Acceptance Criteria:**
- [ ] App loads in dark mode by default on first visit
- [ ] Theme toggle available in settings or header
- [ ] Theme preference persists in local storage
- [ ] All views and components render correctly in both themes
- [ ] Typecheck/build passes
- [ ] Verify in browser using dev-browser skill

### US-011: PWA — installable and offline-capable
**Description:** As a user, I want to install the app on my phone and use it offline so it feels like a native app.

**Acceptance Criteria:**
- [ ] App serves a valid web app manifest
- [ ] Service worker caches app shell for offline use
- [ ] Install prompt appears on supported browsers
- [ ] App works fully offline (all data is local)
- [ ] App icon and splash screen are configured
- [ ] Typecheck/build passes

### US-012: Project scaffolding and setup
**Description:** As a developer, I need to set up the Blazor WASM project with the correct structure, dependencies, and tooling.

**Acceptance Criteria:**
- [ ] Blazor WebAssembly project created with .NET 10
- [ ] Project builds and runs successfully
- [ ] Local storage package integrated (e.g., Blazored.LocalStorage)
- [ ] CSS framework or component library chosen and configured
- [ ] PWA manifest and service worker configured
- [ ] Folder structure follows Blazor conventions (Pages, Components, Models, Services)
- [ ] Typecheck/build passes

## Functional Requirements

- FR-1: The app shall be a Blazor WebAssembly standalone application (no server-side hosting required)
- FR-2: The app shall auto-populate the current date and time when creating a new entry, with the option to manually adjust
- FR-3: The app shall display all 7 Bristol Stool Scale types with their standard medical chart images, names, and descriptions
- FR-4: The app shall require a Bristol type selection before allowing an entry to be saved
- FR-5: The app shall support an optional free-text notes field per entry
- FR-6: The app shall support optional tags per entry, with quick-select for recent/common tags
- FR-7: The app shall persist all data to browser local storage
- FR-8: The app shall display entries in a reverse-chronological diary list view
- FR-9: The app shall display entries in a monthly calendar view with day-level drill-down
- FR-10: The app shall allow editing all fields of an existing entry
- FR-11: The app shall allow deleting entries with a confirmation prompt
- FR-12: The app shall display stats: frequency over time, Bristol type distribution, and time-of-day patterns
- FR-13: The app shall export diary data to a printable/PDF format with optional date range filtering
- FR-14: The app shall default to dark mode and allow toggling to light mode, persisting the preference
- FR-15: The app shall function as an installable PWA with offline support

## Non-Goals

- No user accounts, authentication, or multi-user support in v1
- No cloud sync or backend API (local storage only for now)
- No shareable links or live sharing with healthcare providers
- No push notifications or reminders
- No photo attachments on entries
- No integration with health platforms (Apple Health, Google Fit, etc.)
- No automatic Bristol type classification from photos
- No medication or food tracking (may be a future addition)

## Design Considerations

- **Mobile-first layout:** All interactions should be comfortable on a phone screen (thumb-friendly tap targets, minimal scrolling on the log form)
- **Bristol Scale images:** Use the standard medical chart images if freely available; fall back to clean SVG illustrations if licensing is an issue
- **Quick logging:** The path from opening the app to saving an entry should be as short as possible (ideally 3 taps: open -> select type -> save)
- **Dark mode:** Use a muted, comfortable dark palette as default; avoid pure black backgrounds (use dark greys)
- **Colour coding:** Consider using consistent colours for Bristol types across all views (list, calendar, charts)
- **Accessible design:** Ensure sufficient contrast ratios in both themes; scale images should have alt text

## Technical Considerations

- **Framework:** Blazor WebAssembly (.NET 10, standalone)
- **Storage:** Browser local storage via Blazored.LocalStorage (or similar); data model should be designed so migration to a backend is straightforward later
- **Charting:** A Blazor-compatible charting library (e.g., MudBlazor charts, Radzen, or a lightweight JS interop option)
- **CSS/Components:** Consider MudBlazor or Radzen for UI components (dark mode support, responsive layouts, calendar components)
- **PWA:** Blazor WASM has built-in PWA template support — use it
- **Data model:** Design entry model with an ID, timestamp, Bristol type (1-7), notes (string), and tags (list of strings)
- **Export:** Use a PDF generation library compatible with Blazor WASM (e.g., jsPDF via JS interop) or generate a printable HTML page
- **Performance:** Local storage has a ~5-10MB limit; for heavy users this should be fine for years, but consider warning users if storage is getting full

## Success Metrics

- User can log a new entry in under 5 seconds (3 taps or fewer from app open)
- App installs and works fully offline on mobile browsers
- All 7 Bristol types are clearly distinguishable in the selector
- Calendar and list views load instantly with up to 1,000 entries
- Stats charts render correctly and update as new entries are added
- Export produces a clean, doctor-friendly PDF

## Open Questions

- What specific Bristol Stool Scale images are freely licensed for use? Need to research before implementation
- Should the calendar view show Bristol type colours on day indicators, or just a count?
- What charting library best fits Blazor WASM with minimal bundle size?
- Should we add a "quick log" feature (single tap, defaults to a common type)?
- Future: what does the backend migration path look like? (API shape, auth provider, database choice)
