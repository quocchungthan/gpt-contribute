# Listener

Privacy-first Windows microphone listener built with .NET 10 and ASP.NET Core MVC.

## Developer preview

This branch implements the local MVP workflows: Vietnamese onboarding, selected-device configuration, Windows microphone capture through NAudio, bounded pre-roll and automatic RMS speech chunking, WAV/SQLite persistence, monitoring JSON and UI, signal measurement sorting, protected-safe bulk deletion, seven-day retention, 5 GiB capacity behavior, diagnostics, and ZIP export.

It is not ready for real workplace use. The following require Windows implementation/validation before a pilot:

- always-visible system-tray UI and tray Pause/Resume;
- encryption at rest and Windows-bound key handling;
- Windows user authorization for technical diagnostics;
- real headset, whisper, sleep/resume, reconnect, and long-running soak tests;
- Vietnam-specific legal review and finalized notice text.

## Run

```bash
dotnet restore Listener.sln
dotnet run --project src/Listener/Listener.csproj
```

Open `http://127.0.0.1:5187`. Microphone capture is intentionally unavailable on non-Windows hosts.

## Test

```bash
dotnet test Listener.sln
```

Runtime data is stored below `src/Listener/data` and excluded from Git.
