# Listener story map

Listener is a privacy-first Windows application for preserving spoken workplace interactions. It records only after informed activation, stores likely speech locally, and leaves interpretation to the user. It is not legal advice and does not determine whether conduct is toxic, retaliatory, or unlawful.

## Planning hierarchy

```text
Product outcome -> Domain -> Epic -> Vertical slice -> User story
```

- A **domain** is a stable area of business responsibility.
- An **epic** is a user outcome spanning several stories.
- A **vertical slice** is the smallest demonstrable end-to-end capability. It can contain thin parts of several stories.
- A **story** describes one user need and its acceptance criteria.

Databases, controllers, audio libraries, and other technical components are not epics or slices.

When more than one implementation can satisfy a story, the first slice uses the simplest adequate option. Optional automation, alternate modes, or added complexity must be represented and prioritized as a separate user story rather than silently expanding the current story.

## Product outcome and actors

**Outcome:** An employee can knowingly capture, privately review, manage, and selectively export likely speech from a chosen Windows headset without unbounded recording.

- **Employee:** configures capture and owns the local recordings.
- **Local application:** applies retention and reports failures.
- **Support engineer:** receives redacted diagnostics only when the employee chooses to provide them.

## Domain and epic map

| Domain | Epic | Outcome | Stories |
|---|---|---|---|
| D1. Activation and control | E1. Start safely | Understand the behavior and control when it runs | US-001–US-003 |
| D2. Capture and detection | E2. Preserve likely speech | Retain useful speech context without continuous storage | US-004 |
| D3. Recording library | E3. Understand what was captured | Find, play, describe, and compare recordings | US-005, US-017 |
| D4. Privacy and lifecycle | E4. Keep control of sensitive data | Keep data local, bounded, deletable, and explicitly exportable | US-006–US-009 |
| D5. Operations and trust | E5. Know whether it works | See faults and recover safely | US-010 |
| D6. Enrichment | E6. Organize more effectively | Add optional assistance without automated accusations | US-011–US-014 |

## Vertical release slices

Slices cut through UI, behavior, storage, and tests. They are ordered by learning value and dependency, not architectural layer.

| Slice | Demonstrable user outcome | Stories included | Depends on |
|---|---|---|---|
| S0. Informed test capture | Complete onboarding, choose a microphone, make a short explicit test, play it, and delete it | US-001, US-002, thin US-005/US-008 | Legal and device feasibility |
| S1. Controlled background capture | Start, pause, close/reopen the browser, and see the true capture state | US-003, thin US-010 | S0 |
| S2. Speech-only chunks | Run with bounded memory and see contextual speech chunks while silence is discarded | US-004, thin US-005/US-010 | S1 |
| S3. Private recording library | Browse, play, title, tag, annotate, and sort locally protected chunks by safe signal measurements | US-005, US-006, US-017 | S2 |
| S4. Bounded lifecycle | Apply age/space limits, protect items, handle low disk, and delete consistently | US-007, US-008, part US-010 | S3 |
| S5. User-directed export | Select recordings and create an audio/metadata package with digest manifest | US-009 | S3 |
| S6. Pilot-ready operation | Handle device loss, sleep/resume, interrupted writes, and recovery | US-010 across prior slices | S1–S5 |

S4 and S5 can proceed independently after S3. Health behavior grows in every slice; S6 finishes it rather than introducing it late.

## Cross-cutting definition of done

Every implemented feature includes an accurate **Monitoring block** in the UI. A feature is not complete when only its happy path exists.

The block must:

- read the same authoritative state used by the workflow, never a separate mocked status;
- show `Unknown`, `Unavailable`, or `Stale` when truth cannot be established;
- show current state, freshness, last success, last failure, last state change, and operation ID where applicable;
- expose safe explanatory measurements such as frames received, chunks completed, queue depth, disk usage, or cleanup count;
- update after commands and background changes and give failures a plain-language next action;
- be keyboard accessible and use text, not color alone;
- provide a stable machine-readable representation for automated workflow tests;
- link correlated structured events without exposing audio, notes, transcripts, secrets, or unnecessary personal data.

Every slice also includes a documented user workflow, an automated end-to-end equivalent, success and meaningful-failure coverage, structured events using stable names/operation IDs, and tests proving the displayed state is real and correctly becomes stale or unavailable.

The normal employee UI uses plain language and does not expose internal event names. Stable technical event names and identifiers are retained for the AI engineering/maintenance agent through protected diagnostics, database-level operational records, and Windows event logging.

## Dependency map

```text
US-001 ─┐
US-002 ─┴─> US-003 ─> US-004 ─> US-005 ─┬─> US-007 ─> US-008
                    │          │         └─> US-009
                    │          └────────────> US-006
                    └───────────────────────> US-010
```

- US-001 gates non-test listening; US-002 supplies the input for US-003.
- US-003 supplies the long-running lifecycle required by US-004.
- US-004 creates items consumed by US-005.
- US-006 arrives with the first real library, before pilot data exists.
- US-010 grows alongside every operational story.
- US-007 and US-009 both need library items but are independent.
- US-008 begins with test deletion in S0 and expands to full cleanup in S4.

## E1 — Start safely

### US-001 — Complete privacy and legal onboarding

As an employee, I want to understand and acknowledge how recording works, so that I can make an informed decision before listening starts.

- Explain continuous listening, volatile buffering, saved speech, retention, deletion, and visible operation.
- Require affirmation of responsibility for law and workplace policy before non-test capture.
- Make no claim that a recording is lawful, admissible, or proof of misconduct.
- Keep the notice accessible from Settings.
- Present onboarding in Vietnamese for the initial configured locale.
- Base the initial notice on reviewed requirements for recording while physically located in Vietnam.

### US-002 — Select and test an input device

As an employee, I want to select and test my headset microphone, so that I know the intended device is used.

- List Windows capture devices and persist the chosen stable identifier.
- Provide a level meter and explicitly initiated short test.
- Allow immediate playback and deletion of the test.
- Show device loss; keep automatic fallback off by default.
- Capture only the selected microphone, never Windows output or call/meeting playback.

### US-003 — Control background capture

As an employee, I want capture to continue while the UI is closed and remain easy to pause, so that relevant interactions are not missed and I stay in control.

- Continue in the explicitly launched .NET 10 background application when the browser closes.
- Show Starting, Listening, Paused, Degraded, or Faulted accurately.
- For the MVP slice, require the user to launch Listener and explicitly start listening.
- Keep pause/resume accessible with an unambiguous active indicator.
- Keep a Windows system-tray icon visible whenever Listener is running, with distinct Listening, Paused, Degraded, and Faulted states.
- Provide immediate Pause/Resume from the tray and reflect the result consistently in the tray and web UI.
- Do not allow background operation with the tray presence hidden.

## E2 — Preserve likely speech

### US-004 — Retain speech and discard silence

As an employee, I want likely human speech retained with nearby context, so that storage stays manageable.

- Hold recent audio only in a bounded in-memory rolling buffer.
- Configure voice sensitivity, pre/post-roll, silence gap, and maximum chunk length.
- Group speech separated by brief pauses.
- Discard unpromoted audio irrecoverably.
- Keep memory bounded independently of runtime.
- Validate detection with Vietnamese speech that naturally includes English words and short phrases.
- Detect and promote quiet or whispered speech automatically; do not require a manual save-recent-audio action.
- Recover automatically from transient detector/processing interruption when safe, while showing Degraded or Faulted if automatic recovery cannot restore capture.

## E3 — Understand what was captured

### US-005 — Review captured segments

As an employee, I want a chronological recording library, so that I can find and understand an interaction.

- Show time, duration, state, device, tags, and optional title.
- Support play, seek, rename, tags, private notes, and date/tag filtering.
- Never represent failed or incomplete material as complete.

### US-017 — Sort by audio measurements

As an employee, I want to sort and filter recordings using basic audio measurements, so that I can review likely low-value or storage-heavy pieces before deciding what to delete.

- Calculate duration, file size, average level, peak level, and speech-active percentage for every completed chunk.
- Include detector confidence only if the selected detector exposes a stable, explainable score.
- Sort ascending or descending by each available measurement and combine it with ordinary filters.
- Explain measurements in neutral acoustic language; never label a recording important or unimportant.
- Never use these measurements for automatic deletion or retention eligibility.
- Show missing/unavailable values honestly for older or failed records.

## E4 — Keep control of sensitive data

### US-006 — Protect recordings locally

As an employee, I want recordings and notes protected locally, so that they are not casually exposed or uploaded.

- Keep content local with no implicit upload.
- Encrypt audio and sensitive metadata at rest with Windows-protected key material.
- Limit access to the intended local identity.
- Exclude content/secrets from logs and document protection limits.

### US-007 — Control retention and storage

As an employee, I want automatic age and size limits, so that recordings do not consume unlimited disk space.

- Apply a seven-day default age from `appsettings.json` and show the effective policy and current usage in the UI.
- Let the user protect or unprotect an important recording.
- Remove expired recordings while skipping protected items; protected recordings remain until unprotected or manually deleted.
- Stop permanent writes safely on low disk and show an alert.
- Use a 5 GB ceiling from `appsettings.json`; count protected recordings toward it.
- Show a toast and persistent Monitoring-block warning as usage approaches the ceiling.
- Treat 80% usage as the default warning threshold; toast on threshold crossing rather than every refresh.
- At the storage ceiling, stop saving new recordings; do not delete recordings before normal expiry to make room.
- Show clearly that new audio is not being saved.
- Automatically resume saving when managed storage falls below the ceiling, and show the unsaved interval.
- Log failures without sensitive content.

### US-008 — Delete recordings

As an employee, I want to delete one or multiple recordings I select, so that unwanted sensitive data is removed from the application efficiently.

- Support single and multi-select deletion from the recording library.
- Require explicit confirmation showing selected count and total storage size.
- Prevent deletion when any selected recording is Protected; identify the blocked items and require the user to unprotect them first.
- Treat metadata, audio, and derived artifacts as one deletion operation.
- Show per-item partial failure, preserve failed selections, and permit retry without reselecting them.
- Distinguish application deletion from SSD, backup, or forensic guarantees.

### US-009 — Export selected recordings

As an employee, I want to export recordings I explicitly select, so that I can give them to an adviser or representative.

- Share nothing automatically; require explicit items and destination.
- Include retained audio, timestamps, user notes, and a SHA-256 digest manifest.
- Make no assertion about authenticity, legality, identity, or misconduct.
- Do not retain the destination in ordinary logs.

## E5 — Know whether it works

### US-010 — Diagnose capture health

As an employee, I want clear health and recovery information, so that I know whether capture is functioning.

- Show service state, device availability, latest successful chunk, disk state, and backlog.
- Give faults a plain-language next action.
- Never publish a partial chunk as complete after restart.
- Exclude content, notes, transcripts, usernames, and unnecessary paths from diagnostics.
- Give every feature a consistent Monitoring block backed by current application state.
- Let the AI engineering/maintenance agent and automated tests correlate plain-language UI state, workflow operations, and structured events through stable IDs.

## E6 — Organize more effectively (post-MVP)

- **US-011 — Local transcription:** optional, clearly labeled, potentially inaccurate local transcript. Depends on S3 and a model/security assessment.
- **US-012 — Improve difficult-speech detection:** improve automatic preservation for whispers and difficult acoustic conditions using pilot evidence. Depends on S2; it does not add a manual save button.
- **US-013 — Import from another device:** import user-approved mobile recordings. Depends on S3 and a new import trust boundary.
- **US-014 — Assisted review:** uncertain, neutral summaries and user labels without declaring toxicity or retaliation. Depends on US-011 and bias/misuse evaluation.
- **US-015 — Start automatically at sign-in:** optionally launch Listener and begin its configured ready/listening workflow after Windows sign-in. Depends on S1 and explicit user opt-in.
- **US-016 — Edit retention in the UI:** let the employee change the retention period without editing configuration files. Depends on S4; the initial implementation is configured through `appsettings.json`.

## MVP non-goals

- Covert installation, hidden indicators, or bypassing employer controls.
- Automated findings about harassment, toxicity, intent, or legality.
- Biometric speaker identification.
- Cloud sync, remote monitoring, employer dashboards, or mobile capture.
- Windows loopback, system-output, or direct call/meeting audio capture.
- Tamper-proof or court-admissibility guarantees.
