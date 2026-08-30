# Listener product specification

## Problem

Important workplace interactions may occur verbally and leave no chat or email record. Continuous uncompressed recording, however, creates excessive data and serious privacy risk. Listener should provide user-controlled, local capture that retains likely speech segments while discarding irrelevant buffered audio.

## Product principles

1. **User agency:** listening state, pause, retention, export, and deletion remain under the local user's control.
2. **Data minimization:** keep a short volatile buffer and persist only promoted speech chunks.
3. **Local first:** no cloud dependency or implicit network transfer in the MVP.
4. **Neutrality:** preserve and organize material; do not decide whether conduct is toxic, unlawful, or retaliatory.
5. **Visible operation:** do not design the product as spyware or conceal its presence.
6. **Failure transparency:** uncertainty and degraded capture must be visible.
7. **Simplest sufficient delivery:** implement the least complex behavior that satisfies the current slice; capture optional complexity as a separately prioritized story.

## MVP scope

- Windows 11 laptop with a conventional wired, USB, or Bluetooth headset microphone.
- One local Windows user and one selected input device at a time.
- Capture only the explicitly selected microphone input device.
- Explicitly launched .NET 10 per-user background application that continues after its browser UI closes.
- ASP.NET Core MVC UI bound locally.
- Rolling memory buffer, voice-activity detection, speech chunking, encrypted local persistence.
- Review, playback, tags, notes, retention, deletion, export, and health reporting.
- MVP sorting/filtering by basic per-chunk signal measurements.

## Language and locale

- Supported countries/locales are configured as an array in `appsettings.json`.
- The initial array contains one option: Vietnam, with Vietnamese as the UI language.
- Vietnamese is the primary spoken-language scenario.
- Detection and later transcription evaluation must include natural Vietnamese speech containing English words or short English phrases within sentences.
- Language configuration is separate from the legal jurisdiction in which recording occurs.

## Initial jurisdiction

- The MVP is intended for use by employees physically located in Vietnam.
- Before any real workplace pilot, Vietnamese legal review must determine the required consent, notification, data-protection, workplace-policy, retention, and export behavior.
- The application must not infer that selecting Vietnam as a locale makes a recording lawful.
- Cross-border calls and recordings made while the user is outside Vietnam are outside the initial validated scope.

## Audio-source boundary

- The MVP captures microphone input only.
- It does not capture Windows system output, loopback audio, call playback, meeting audio, or application-specific audio streams.
- Call and meeting platforms are expected to provide their own recording workflows where applicable.
- The service never silently switches microphones. If the selected device is unavailable, it reports a degraded state and waits for reconnection or an explicit new selection.
- Saving likely speech, including whispers, is automatic. The MVP has no manual “save recent audio” command.
- Transient capture or processing recovery is automatic where safe; unrecovered conditions remain visibly Degraded or Faulted.

## Windows presence

- A Windows system-tray icon is always visible while Listener is running.
- The icon distinguishes Listening, Paused, Degraded, and Faulted states and provides equivalent text in its tooltip/accessibility label.
- Clicking the tray icon exposes an immediate Pause or Resume action appropriate to the current state.
- A tray command and the web UI use the same capture state; both surfaces update after the command succeeds or fails.
- The application provides no supported option to hide the tray icon while continuing to run.
- Closing the browser UI leaves the tray icon and service-state indication available.
- The initial implementation requires explicit user launch and start. Automatic launch/listening at Windows sign-in is a separate post-MVP story.

## Functional defaults

Defaults are provisional and must be validated with representative hardware and speech:

| Setting | Initial default |
|---|---:|
| Pre-roll | 3 seconds |
| Post-roll | 5 seconds |
| Silence gap before closing a chunk | 2 seconds |
| Maximum chunk duration | 15 minutes |
| Temporary buffer ceiling | 30 seconds |
| Retention age | 7 days, configurable in `appsettings.json` |
| Storage ceiling | 5 GB, configurable in `appsettings.json` |
| Automatic device fallback | Off |
| Network processing | Off and absent from MVP |

## User experience

The local web UI contains:

- **Status:** listening state, device, live level, disk capacity, latest successful capture, and pause/resume.
- **Recordings:** chronological searchable list and audio player.
- **Recording details:** title, tags, notes, metadata, export, protect-from-cleanup, and delete.
- **Settings:** input device, detection sensitivity, chunk timing, startup behavior, retention, storage, and legal/privacy notice.
- **Diagnostics:** service health, recent redacted events, and diagnostic export.

Every feature surface includes a consistent Monitoring block showing actual state, freshness, last success/failure, and useful safe measurements in plain language. Users do not need to understand internal event names. A protected machine-readable view, bounded operational database records, and Windows event logging let the AI engineering/maintenance agent reconcile workflows, durable state, and structured events.

For the initial retention implementation, Settings displays the effective seven-day policy read-only. Editing retention through the UI is not required.

Users can mark individual recordings as Protected. The seven-day cleanup skips them until the user removes protection or manually deletes them. The library and Monitoring block show protected count and storage usage so protection cannot silently hide disk consumption.

The library supports selecting and deleting multiple recordings. Before deletion, it shows the number of selected recordings and their combined size. If any selection is Protected, the entire delete command is blocked and the protected items are identified; the user must unprotect them first. Completion and partial failure are visible without requiring the user to reconstruct the selection.

All recordings, including Protected items, count toward the configured 5 GB storage ceiling. As usage approaches the ceiling, the application shows a non-technical toast warning and a persistent Monitoring-block warning. At the ceiling, it must not silently exceed the limit or delete Protected items.

At the ceiling, Listener stops saving new recordings. It does not delete unprotected recordings before their seven-day expiry merely to create space. Existing recordings remain unchanged, and the UI must distinguish “microphone activity may still be observed” from “new audio is being saved.”

When managed storage falls below the ceiling, Listener automatically resumes saving without requiring restart or user action. The tray, Monitoring block, and operational events show the transition and the interval during which recordings were not saved.

## Recording signal measurements

Completed chunks can be evaluated using content-neutral measurements such as duration, file size, average signal level, peak level, percentage of frames classified as speech, and aggregate detector confidence where the chosen detector provides it reliably. These values support sorting and filtering alongside chronological order.

Signal measurements indicate acoustic properties, not importance or misconduct. A quiet whisper may be highly important. Measurements must never cause automatic deletion, and the UI must not label low-volume recordings as unimportant.

“Approaching” begins at 80% usage by default (4 GB with the default ceiling). Show the toast when usage crosses into the warning range rather than on every refresh; keep the persistent warning until usage falls below the threshold.

Visual styling may later adapt the user's AI Interview Prep project, but copying begins only after its location and asset licensing are confirmed. Styling must not couple the MVC UI to capture-domain behavior.

## Quality attributes

- Capture survives UI closure and ordinary browser failures.
- Working-set memory remains bounded during indefinite listening.
- Interrupted writes are recoverable or quarantined.
- The UI is keyboard operable and exposes meaningful status text, not color alone.
- Ordinary operation requires no internet access.
- Sensitive values never enter telemetry or normal logs.
- UI monitoring derives from authoritative application state, marks stale/unknown data explicitly, and is verified by automated workflows.

## Product risks and mitigations

| Risk | Mitigation |
|---|---|
| Recording violates law or policy | Mandatory onboarding, jurisdiction review before distribution, visible operation, no claim of legality |
| Whispered speech is missed | Tunable sensitivity, pre-roll, visible level meter, later quick-preserve action |
| Background noise is over-retained | VAD tuning, maximum duration, review and deletion controls |
| Device changes silently | Stable device ID, explicit degraded state, fallback disabled by default |
| Sensitive local disclosure | Encryption, least-privilege service identity, sanitized logs, local-only binding |
| Automated analysis harms users | Exclude verdicts from MVP; label later derived content as uncertain |

## MVP success measures

- At least 95% of scripted normal-volume speech events are retained in controlled headset tests.
- No unbounded memory growth during a 24-hour soak test.
- Service recovery after simulated process interruption produces no falsely complete chunks.
- A user can identify whether capture is healthy within ten seconds.
- No audio or sensitive text leaves the device during network-observed MVP testing.

The capture target is a product validation metric, not a guarantee that all speech or evidence will be preserved.
