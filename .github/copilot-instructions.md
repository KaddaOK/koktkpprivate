# Copilot instructions

## Architecture snapshot
- `RootController` in KOKTKaraokeParty/RootController.cs is the scene-root orchestrator: it loads `Settings`, adds each service node as a child, wires queue/playback events, and launches the SessionPrepWizard before exposing the UI tabs.
- Services such as SessionPreparationService, QueueManagementService, BackgroundMusicService, PlaybackCoordinationService, and SessionUIService are Godot nodes that must be `AddChild`-ed before `Initialize` is called; they publish events (ItemAdded, NowPlayingChanged, PlaybackFinished, etc.) instead of exposing global state.

## Dependency plumbing
- Every node that participates in the dependency graph uses Chickensoft.AutoInject/Introspection (`[Meta(typeof(IAutoNode))]`, `[Dependency]`, `this.Provide()`, `this.DependOn<T>()`) so new dependencies should expose their provided interface via `IProvide<T>` in RootController rather than new singletons.
- When a background task needs to touch the UI, mirror existing practice with `Callable.From(...).CallDeferred()` and prefer `Settings.SaveToDisk`/`Utils.GetAppStoragePath()` helpers to keep user:// paths consistent.

## State & persistence
- Settings live in Models/Settings.cs and are serialized to `user://settings.json`; changing a `Settings` flag (monitor, countdown, bg music, service toggles) must be followed by a `SaveToDisk` call from the same helper to keep the file in sync.
- Queue state is persisted by QueueManagementService in `user://queue.json` via Utils helpers; the queue contains QueueItem instances (ItemType: KarafunWeb, KarafunRemote, Youtube, LocalMp3G, LocalMp3GZip, LocalMp4) so new queue-aware code needs to respect these types and the asynchronous YouTube download flag.

## Session prep & display flow
- SessionPrepWizard/Controls handles the multi-step flow described in Controls/SessionPrepWizard/SessionPrepWizard.cs, relying on WizardState/WizardLogic to decide whether to restore the queue, show monitor toggles, enable service checkboxes, run SessionPreparationService checks, and collect Karafun room codes before the main app unlocks.
- SessionPreparationService pushes status updates into SessionUIService, which drives the status tree and Next button disable/tooltip state; replicate this pattern whenever introducing new readiness checks for YouTube, Karafun, yt-dlp, or VLC.
- BackgroundMusicService uses Settings, IDisplayScreen, and NAudio to fade in/out playlists saved in Settings.BgMusicFiles and to surface now-playing info to the display screen—hook into `UpdateBgMusicNowPlaying`/`UpdateBgMusicPaused` there if you need to show state on the display.

## Browser & remote automation
- BrowserProviderNode (Web/BrowserProviderNode.cs) bootstraps PuppeteerSharp (`YoutubeAutomator`, `KarafunAutomator`), downloads Chromium into `user://browser`, checks YouTube/Karafun status, and exposes controlled/uncontrolled launch methods plus playback controls used by PlaybackCoordinationService for KarafunWeb and Youtube items.
- KarafunRemoteProviderNode (Web/KarafunRemoteProviderNode.cs) wraps KarafunRemoteClient, exposes Connect/Disconnect/Queue/Pause/Skip methods, validates 6-digit room codes, and feeds KarafunRemoteConnectionStatus/PlaybackState into PlaybackCoordinationService; prefer remote methods when `IsConnected` is true for smoother playback transitions.

## Local scanning & database
- Local scanning sits under the Local folder: LocalFileScanner walks directories for mp4/cdg+mp3/zip files and raises progress/orphaned CDG events, LocalScanManager writes LocalSongFileEntry/LocalScanPathEntry rows to the EF Core KOKTDbContext (`user://appdata.db`), and LocalSearcher queries LocalSongFiles for results.
- The DbContext defines indexes on file paths and keeps parent path relationships; keep migrations/updates focused on LocalSongFileEntry/LocalScanPathEntry/PerformanceHistory so scans stay performant.

## Build, tests & CI
- The workspace task `process: build` runs `dotnet build` from the solution root (Godot.NET.Sdk/4.4.1 targeting net8.0); stick to that command locally and let GitHub Actions follow suit.
- Only the pure xUnit tests can be executed via `dotnet test KOKTKaraokeParty.Tests/KOKTKaraokeParty.Tests.csproj`; those cover helpers and data classes that do not touch Godot/ChickenSoft APIs.
- Tests that rely on Godot scenes/services must use GoDotTest and run through Godot itself; use the same command as the "🧪 Debug Tests" launch configuration (e.g. `godot --path KOKTKaraokeParty.Tests --run-tests --headless --quit-on-finish`). When running GoDotTest you will see logs such as `GoTest: > ^^ >> Started testing! :3` plus per-class/method start/pass/fail markers and end-of-run summaries (`GoTest: > OK >> Test results: Passed: 63 | Failed: 0 | Skipped: 0`). A failed test will also emit the assertion failure detail between the finished line and the summary.
- For coverage, inspect KOKTKaraokeParty.Tests/coverage.ps1: it builds, runs GoDotTest via Godot (`--run-tests --coverage --quit-on-finish`), then coverlet on the same GoDot build and on the compiled test assembly before feeding both outputs to ReportGenerator.

## Testing patterns
- Every service test (see KOKTKaraokeParty.Tests/Services/QueueManagementServiceTests.cs, SessionPreparationServiceTests.cs, SessionUIServiceTests.cs) uses the Godot fixture and Moq to observe the public events rather than peeking into private fields; follow that pattern when adding new assertions (ItemAdded, QueueLoaded, PlaybackFinished, button state changes, etc.).
- SessionPreparationServiceTests focuses on usability checks (IsYouTubeUsable, IsKarafunUsable, IsLocalFilesUsable) while SessionUIServiceTests validates UI tooling (AreAllServicesReady, UpdateButtonStates, PopulateServiceConfirmationDialog), so keep readiness logic inside those services and surface user-facing strings via SessionUIService.
- To keep some logic testable via `dotnet test`, the code isolates Godot-free helpers in classes such as Controls/SessionPrepWizard/WizardLogic.cs; prefer writing new helpers in a similar style so they can be covered by the faster xUnit suite when they don't need Godot nodes.
