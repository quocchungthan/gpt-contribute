# Listener technical specification

## Approach: boring first

Start with conventional .NET 10: concrete classes, normal method calls, ASP.NET Core MVC, a hosted worker, EF Core with SQLite, and files on disk. Do not introduce ports/adapters, bridges, a message bus, CQRS, repositories over EF Core, separate services, or a domain-event framework without a demonstrated need.

When a more automated or flexible implementation is proposed, keep the current implementation simple and require a separately accepted user story before adding it.

The first design is a **modular monolith in one process and one deployable application**. Boundaries begin as folders and namespaces.

- Extract an interface for two real implementations, a necessary external-API test seam, or difficult resource ownership.
- Split a project when platform/build dependencies make it useful.
- Split a process only for a measured identity, reliability, or security reason.
- Record consequential decisions after evidence is available.

## Initial solution

```text
Listener.sln
src/Listener/
  Program.cs
  Capture/        audio capture, buffer, speech detection, chunking
  Recordings/     metadata, files, playback, notes, deletion, export
  Settings/       device and user configuration
  Operations/     health, recovery, retention, redacted logging
  Web/            MVC controllers, views, view models, static assets
  Data/           EF Core DbContext and migrations
tests/Listener.Tests/
tests/Listener.AudioTests/
```

One explicitly launched ASP.NET Core application hosts MVC on loopback, one capture `BackgroundService`, EF Core, SQLite, and local recording files. It continues while its browser UI is closed. Automatic Windows sign-in launch/listening is deferred to US-015. Introduce Windows Service hosting only if the accepted background-lifecycle requirements cannot be met by this simpler per-user application.

S1 also requires a per-user Windows system-tray presence. First test whether it can safely coexist with the chosen hosting/identity model. If Windows session isolation prevents a service-hosted tray icon, introduce the smallest per-user tray companion necessary; do not use that possibility to split other application responsibilities prematurely.

Tray Pause/Resume invokes the same application operation as MVC. It does not maintain its own capture-state flag. Command completion or failure updates the shared state read by the tray, web UI, Monitoring block, and diagnostics.

## Monitoring block contract

Every feature page includes one reusable Razor partial or View Component for operational truth. Feature code writes normal state and events; the block reads that state. It must not maintain a second UI-only reality.

The human-readable block contains feature/state, observation time and staleness, last successful operation, a plain-language failure summary and suggested action, and useful safe measurements. Internal event names, raw error codes, and technical identifiers are hidden from ordinary users.

Expose the underlying technical data through a protected loopback-only JSON endpoint for browser automation and the AI engineering/maintenance agent. Its schema and enum names are versioned and stable. Do not add a second command surface just for tests. Diagnostic access must use the application's local authorization boundary and is not an employee-facing workflow.

```json
{
  "schemaVersion": 1,
  "feature": "capture",
  "state": "Ready",
  "observedAtUtc": "2026-08-30T10:15:00Z",
  "isStale": false,
  "operationId": "01K...",
  "lastSuccess": {
    "event": "capture.chunk_completed",
    "atUtc": "2026-08-30T10:14:58Z"
  },
  "lastFailure": null,
  "measurements": {
    "framesReceived": 4200,
    "activeChunks": 0
  }
}
```

Return `Unavailable` or `isStale: true` when the source cannot be read. Never synthesize health merely because the HTTP request succeeded.

## Domain ownership in code

| Domain | Initial location | Owns |
|---|---|---|
| Activation and control | `Settings/`, `Web/` | onboarding, device, start/pause, startup preference |
| Capture and detection | `Capture/` | frames, bounded buffer, speech decision, chunk boundaries |
| Recording library | `Recordings/`, `Web/` | metadata, playback, titles, tags, notes |
| Privacy and lifecycle | `Recordings/`, `Operations/` | encryption, retention, deletion, export |
| Operations and trust | `Operations/`, `Web/` | health, recovery, redacted diagnostics |

These are ownership guides, not separate assemblies or “bounded contexts” yet.

## First vertical implementation: S0

1. Show onboarding and persist acceptance.
2. List Windows microphones and save one selection.
3. Let the user initiate a short test capture.
4. Write a finalized local audio file and one SQLite row.
5. List and play it in MVC.
6. Delete the row and file consistently.

S0 uses explicit test capture before background listening, VAD, chunking, or encryption complexity. It validates hardware, codec, browser playback, and identity assumptions early.

## Capture pipeline for S1–S2

Keep synchronous ownership in the background worker until measurement proves more concurrency is needed:

```text
audio frame -> rolling buffer -> speech check -> active chunk -> finalized file
```

1. Read fixed-duration PCM frames from the selected Windows endpoint.
2. Keep the configured pre-roll in a fixed-capacity circular buffer.
3. Run the chosen detector for each required frame window.
4. When speech begins, copy pre-roll into the active chunk.
5. Continue until silence gap, pause, device loss, or maximum duration.
6. Include bounded post-roll, encode, flush to a temporary file, then rename atomically.
7. Mark the database row Complete only after the final file exists.

Use one owner for capture buffers. Avoid pooling initially. If capture and encoding cannot keep up, add a bounded `Channel<T>` with an explicit drop/fault policy—never an unbounded queue.

The endpoint is an explicitly selected microphone capture endpoint. Do not open Windows render/loopback endpoints or application audio sessions. Device loss enters a degraded state and never triggers an unapproved fallback device.

Whisper handling remains part of automatic detection. The product does not expose a manual buffer-promotion command. Evaluate detector settings or implementation against the agreed quiet/whisper corpus, and report degraded/faulted state if the detector or pipeline cannot recover automatically. Do not respond to detector failure by saving the continuous stream indefinitely.

## Recording states

```text
Writing -> Complete
   |          |
   +-> Quarantined

Complete -> Deleting -> removed
```

Only Complete recordings are playable. At startup, a Writing item or temporary file becomes Quarantined or is safely removed by a documented rule. No general workflow engine is needed.

## Data and files

Use EF Core with SQLite directly. Controllers call small application services; those services use `ListenerDbContext`. Do not wrap EF Core in a generic repository.

Initial recording data includes random ID, UTC start/end and local offset, device ID/label, state, codec, duration, byte count, generated relative filename, optional title/notes, simple tag rows, protected flag, and created/modified timestamps.

`IsProtected` is an ordinary persisted recording property changed through the same authorized application operation used by MVC. Age-based cleanup queries only expired, complete, unprotected recordings. Protected recordings cannot be explicitly deleted until unprotected. Monitoring reports protected item count and bytes without exposing titles or notes.

Bulk deletion accepts an explicit bounded list of recording IDs and recalculates protection, count, and bytes server-side. If any current row is Protected, reject the entire command before changing state and return the blocked IDs. Otherwise process each item through the same deletion operation as single delete. Return a per-item result and keep failed IDs selected for retry. Do not make the operation a single SQLite transaction around filesystem deletion; record recoverable `Deleting` state so database/file inconsistencies can be reconciled.

Use random IDs for filenames, never user text. Keep audio outside SQLite. Write with a temporary suffix and finalize using a same-volume rename.

## Local web UI

- Bind Kestrel only to explicit loopback addresses.
- Validate Host headers, reject cross-origin requests, and use antiforgery protection.
- Establish intended-Windows-user authorization before Service mode.
- Keep controllers readable: validate, call one operation, return a result/view.
- Use Razor and CSS; no SPA framework is needed.

Reuse the AI Interview Prep theme only after locating it and confirming asset ownership. Styling remains inside `Web/`.

## Encryption timing

S0 uses disposable development recordings only. Before S3 permits real pilot recordings, encrypt audio and sensitive notes with maintained .NET cryptography and Windows-protected keys.

Choose and document the service/user identity, authenticated-encryption format, unique nonce handling, atomic write format, and key loss/uninstall/backup/rotation behavior. Do not invent a crypto framework. Document that encryption cannot protect playback memory or defend against a process already running as the authorized user.

## Configuration

- Put machine startup values in normal ASP.NET Core configuration.
- Define supported country/locale options in `appsettings.json`; begin with one Vietnam/Vietnamese entry and validate it at startup.
- Read `Retention:MaximumAgeDays` from `appsettings.json`, default it to `7`, require a positive bounded integer, and fail startup validation on invalid values.
- Read `Storage:MaximumBytes` from `appsettings.json`, default it to `5368709120` (5 GiB), require a positive bounded integer, and fail startup validation on invalid values.
- Read `Storage:WarningThresholdPercent`, default it to `80`, require a value from 1 through 99, and validate it at startup.
- Put user-adjustable settings in SQLite.
- Protect keys with Windows facilities, not plaintext config/database fields.
- Validate threshold, timing, and storage limits before saving.
- Apply capture changes between frames/chunks, not during unsafe writes.

Avoid a custom configuration provider until this proves inadequate.

An initial shape can remain simple:

```json
{
  "SupportedLocales": [
    {
      "CountryCode": "VN",
      "LanguageCode": "vi",
      "DisplayName": "Vietnam",
      "LanguageDisplayName": "Tiếng Việt"
    }
  ]
}
```

The initial configuration also includes:

```json
{
  "Retention": {
    "MaximumAgeDays": 7
  },
  "Storage": {
    "MaximumBytes": 5368709120,
    "WarningThresholdPercent": 80
  }
}
```

Storage usage includes complete, writing, quarantined, and protected recording files as applicable; diagnostics should reconcile database-accounted bytes with actual managed-directory bytes. The toast is a user notification, while the Monitoring block remains the persistent source of status. At the ceiling, finalize only when doing so cannot exceed the configured policy; otherwise stop new persistence safely and explain the action required.

Capacity cleanup only applies the normal seven-day policy to expired unprotected recordings. It never deletes a younger recording just to admit a new one. While persistence is stopped, do not accumulate audio in memory beyond the normal bounded rolling buffer. The tray, UI, Monitoring JSON, database operational event, and Windows Event Log must report a stable storage-capacity error without including recording content.

Recheck managed storage after cleanup, deletion, and on a modest periodic interval. Resume persistence automatically when usage is below the hard ceiling. Persist the start/end of the no-save interval as redacted operational state so the UI and AI maintenance agent can identify the gap.

For US-017, calculate measurements once during chunk finalization and persist numeric values with the recording row. Start with values already available from captured PCM and detector output: duration, bytes, RMS or equivalent average level, peak level, speech-active frame ratio, and detector confidence only if its semantics are stable. Do not introduce frequency-domain analysis or a general signal-processing framework until a story requires it. Indexed numeric columns provide ordinary ascending/descending sorting. None participate in retention eligibility.

Persist whether the application is currently inside the warning range so restarts and page refreshes do not repeat the toast. Reset that notification state only after usage returns below the threshold; crossing it again may create a new toast.

Speech test fixtures must include Vietnamese/English code-switching. Voice activity detection should not depend on language, but any later transcription or analysis feature must treat mixed-language speech as a first-class requirement.

## Failure behavior

- **Device loss:** end safely, show Degraded, and retry the selected device with bounded delay.
- **Sleep/resume:** end the timing sequence and reopen the device.
- **Disk full:** stop durable capture, discard bounded transient audio, and show a fault.
- **Crash:** never infer that a temporary file is complete.
- **SQLite failure:** stop creating untracked files.
- **Detector failure:** fail visibly instead of reverting to unlimited recording.

## Logging and health

Use built-in structured `ILogger`. Log state changes, counts, durations, queue depth if present, disk state, device availability, exception type, and stable error codes. Never log audio, transcripts, notes, titles, tags, keys, raw usernames, or unrestricted paths. Add an observability stack only if local logs and the health page prove insufficient.

Use stable event names such as `capture.started`, `capture.chunk_completed`, `recording.delete_failed`, and `retention.completed`. Give each command or background operation an operation ID and carry it through technical HTTP responses, persisted operation state, and logs. The employee UI translates these into plain-language status and never requires users to understand event names or IDs.

Retain redacted operational records in a bounded database table for local diagnosis and emit appropriate service lifecycle, warning, and error events to Windows Event Log. The database and Windows events support the AI engineering/maintenance agent; neither may contain audio, transcripts, notes, titles, tags, keys, or other sensitive content. Logs help explain business state but are not its source of truth.

## Testable workflows

For every slice, keep one plain-language workflow and an automated end-to-end equivalent. Automation uses the same MVC/HTTP surface as the product, then verifies that Monitoring JSON, durable state, and structured events agree.

Use deterministic seams only where the OS makes a scenario unreliable—for example, a small fake audio source for known speech/silence fixtures or injectable time for retention. A concrete testing need does not justify abstracting every class.

Every feature workflow covers:

1. command acceptance and returned operation ID;
2. visible working transition when applicable;
3. durable success reflected by UI and Monitoring JSON;
4. one controlled failure with the same ID and actionable state;
5. stale/unavailable behavior when the state source stops responding;
6. absence of sensitive content from logs and diagnostics.

## Testing in slice order

- **S0:** onboarding, device enumeration, record/play/delete, safe filenames.
- **S1:** browser closure, pause/resume, service restart, device removal.
- **S2:** buffer bounds, speech corpus, pre/post-roll, maximum chunks, 24-hour soak.
- **S3:** encryption, access control, playback, notes/tags, content-free logs.
- **S4:** retention, protected items, low disk, partial deletion.
- **S5:** explicit export, digest verification, no implicit network use.
- **S6:** crash, sleep/resume, reconnect, upgrade, uninstall, diagnostics.

Fixtures use consenting participants or licensed synthetic audio, never real workplace incidents.

## Decisions only when needed

| Decision | Needed by | Evidence |
|---|---|---|
| Windows capture library/API | S0 | headset compatibility and maintained .NET support |
| Audio container/codec | S0 | reliable capture and browser playback |
| Windows Service hosting/identity | Not assumed | proof the per-user application cannot satisfy accepted background lifecycle |
| Speech detector | S2 | whisper recall, noise false positives, CPU, license |
| Encryption/key scope | S3 | threat model and service identity |
| Vietnam consent/notice behavior | Before workplace pilot | qualified Vietnam-specific legal review |
| Separate UI process/IPC | Not assumed | demonstrated isolation, identity, or reliability problem |
| More projects/interfaces | Not assumed | second implementation or painful platform/build coupling |
| Bus/CQRS/plugin model | Outside MVP | concrete need simple calls cannot satisfy |
