# Incremental delivery specification

Each increment delivers vertical slices from the [story map](user-stories/README.md). Work is organized by demonstrable user outcomes, not by completing technical layers in isolation.

## Required output from every increment

Every slice ships with its Monitoring block, stable structured event names, safe diagnostics, and matching user and automated workflows. The block reflects authoritative state, identifies stale/unavailable data, and correlates with logs through an operation ID. A slice is not accepted if UI state disagrees with durable or background state.

Use the simplest adequate implementation for each slice. If an alternate mode or automation is not required for that outcome, add it as a separate story instead of enlarging the slice.

## Increment 0 — Feasibility and decisions

Deliverables:

- Confirm target Windows versions, deployment model, and service identity.
- Obtain Vietnam-specific review of consent, notification, workplace, and data-protection requirements before pilot use.
- Test Windows audio capture against representative wired, USB, and Bluetooth headsets.
- Compare candidate voice-activity detectors with normal and whispered Vietnamese speech, Vietnamese/English code-switching, typing, music, meetings, and background noise; automatic whisper preservation is required and there is no manual fallback button.
- Decide audio codec/container and encryption/key-protection approach.
- Locate and review the AI Interview Prep styles before reusing them.
- Validate the initial Vietnam/Vietnamese `SupportedLocales` configuration and UI text behavior.
- Document that physical use outside Vietnam and cross-border recording scenarios are not validated for the MVP.

Exit criteria:

- Architecture decision records cover capture API, VAD, storage format, encryption, and local authentication.
- A disposable spike demonstrates bounded capture and chunk creation; it is not distributed to users.

## Increment 1 — S0: informed test capture

Stories: US-001, US-002, and thin parts of US-005 and US-008.

Deliverables:

- One .NET 10 ASP.NET Core MVC application with a hosted capture worker.
- Onboarding, device enumeration, selection, level meter, and explicit short test.
- One local test recording and metadata row, playable and deletable through MVC.
- Structured, content-free operational logging.
- Monitoring block for onboarding, device, test capture, durable completion, and deletion.
- Human and automated record/play/delete workflows with correlated events.

Exit criteria:

- A user completes the microphone-to-playback-to-deletion path.
- No background listening starts in this slice.
- Hardware, codec, playback, and local identity assumptions are recorded.

## Increment 2 — S1 and S2: controlled speech capture

Stories: US-003, US-004, and growing parts of US-010.

Deliverables:

- Explicitly launched per-user background application with start, pause, resume, and visible state.
- Always-visible Windows system-tray presence with accessible state text.
- Immediate tray Pause/Resume synchronized with the web UI and Monitoring block.
- Configurable speech detection, pre-roll/post-roll, silence grouping, and maximum chunk length.
- Atomic chunk finalization and startup recovery for incomplete files.
- Metadata database and local recording store.
- Test corpus and repeatable detection measurements.
- Monitoring blocks for service, device, frame flow, buffer, detector, active chunk, and finalization.

Exit criteria:

- Silence is not durably retained outside promoted chunks.
- Test corpus results and false-positive/false-negative tradeoffs are documented.
- Forced termination cannot make an incomplete file appear complete.
- UI closure does not stop capture, and a 24-hour soak shows bounded memory.

## Increment 3 — S3: private recording library

Stories: US-005, US-006, and US-017.

Deliverables:

- Recording list, detail view, playback, tags, titles, and notes.
- Per-chunk duration, size, average/peak level, speech-active percentage, and supported detector-confidence measurements.
- Ascending/descending measurement sorting and filtering with neutral labels.
- Encryption at rest and protected key storage.
- Loopback-only UI, authorization boundary, and log-redaction tests.
- Accessible baseline styling.
- Library Monitoring block for database/file consistency, playback readiness, and encryption state.

Exit criteria:

- Sensitive files cannot be interpreted directly without the protected key.
- No content appears in normal logs.
- Core workflows pass keyboard and screen-reader smoke tests.

## Increment 4 — S4 and S5: lifecycle and user-directed export

Stories: US-007, US-008, US-009, remaining US-010.

Deliverables:

- Age and capacity retention policies, protected-item behavior, disk monitoring.
- Seven-day age default loaded from `appsettings.json`, with effective value and next cleanup shown in the Monitoring block.
- Protect/unprotect action in recording details; age cleanup demonstrably skips protected recordings.
- Single and multi-select deletion with count/size confirmation and retryable per-item failure.
- Protected-selection guard that blocks the whole delete command until all selected recordings are unprotected.
- Five-gigabyte configured storage ceiling with approaching-limit toast, persistent warning, and protected-byte accounting.
- Configurable 80% warning threshold with one toast per threshold crossing.
- Hard-ceiling workflow that stops new recording persistence without early deletion and exposes the condition in tray/UI monitoring.
- Automatic persistence recovery below the ceiling, with the unsaved interval visible in monitoring and diagnostics.
- Consistent deletion workflow.
- Explicit export with metadata and digest manifest.
- Redacted diagnostics and recovery guidance.
- Monitoring blocks for retention, disk pressure, deletion, and export progress/result.

Exit criteria:

- Low-disk and partial-delete scenarios are integration tested.
- Export hashes validate against exported audio.
- No automatic share or upload path exists.

## Increment 5 — Pilot hardening

Slice: S6, completing US-010 across all prior slices.

Deliverables:

- Signed packaging, safe upgrade/uninstall behavior, backup guidance, and support runbook.
- Performance, interruption, sleep/resume, headset reconnect, Bluetooth profile-change, and OS-restart testing.
- Threat-model review and independent security test of local access and key handling.
- Pilot feedback focused on discoverability, missed speech, excess capture, and user control.
- Reconciliation tests across UI monitoring data, database/file state, and structured logs.

Exit criteria:

- No unresolved critical security or data-loss defects.
- Legal/privacy requirements for the chosen pilot jurisdiction are reflected in the product.
- Metrics meet the targets in the product specification on supported hardware.

## Deferred increments

Only after the local capture MVP is validated:

1. Evidence-driven improvement of automatic difficult-speech detection (US-012).
2. Optional local transcription (US-011).
3. User-controlled import from mobile devices (US-013).
4. Carefully evaluated neutral summarization and organization (US-014).
5. Optional automatic Windows sign-in startup (US-015).
6. User-editable retention settings in the UI (US-016).
