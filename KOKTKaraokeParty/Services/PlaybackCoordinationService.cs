using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Services;

[Meta(typeof(IAutoNode))]
public partial class PlaybackCoordinationService : Node
{
    public event Action<long> PlaybackDurationChanged;
    public event Action<long> PlaybackProgressChanged;
    public event Action<QueueItem> PlaybackFinished;
    public event Action<string, int, int, bool> ProgressSliderUpdateRequested;

    private Settings _settings;
    private IDisplayScreen _displayScreen;
    private IBrowserProviderNode _browserProvider;
    private IKarafunRemoteProviderNode _karafunRemoteProvider;
    private BackgroundMusicService _backgroundMusicService;

    public void Initialize(Settings settings, IDisplayScreen displayScreen, IBrowserProviderNode browserProvider, IKarafunRemoteProviderNode karafunRemoteProvider, BackgroundMusicService backgroundMusicService)
    {
        _settings = settings;
        _displayScreen = displayScreen;
        _browserProvider = browserProvider;
        _karafunRemoteProvider = karafunRemoteProvider;
        _backgroundMusicService = backgroundMusicService;
        SetupEventHandlers();
    }

    public void OnReady()
    {
        this.Provide();
    }

    private void SetupEventHandlers()
    {
        _displayScreen.LocalPlaybackFinished += OnLocalPlaybackFinished;
        _displayScreen.LocalPlaybackDurationChanged += (duration) => PlaybackDurationChanged?.Invoke(duration);
        _displayScreen.LocalPlaybackProgress += (progress) => PlaybackProgressChanged?.Invoke(progress);
        
        _browserProvider.PlaybackDurationChanged += (duration) => PlaybackDurationChanged?.Invoke(duration);
        _browserProvider.PlaybackProgress += (progress) => PlaybackProgressChanged?.Invoke(progress);
    }

    public async Task PlayItemAsync(QueueItem item, CancellationToken cancellationToken)
    {
        // Start background music fade if enabled
        if (_settings.BgMusicEnabled)
        {
            _backgroundMusicService.FadeIn();
        }

        // Show countdown
        await ShowCountdownAsync(item, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return;

        // Fade out background music before starting playback
        if (_settings.BgMusicEnabled)
        {
            _backgroundMusicService.FadeOut();
        }

        GD.Print($"Playing {item.SongName} by {item.ArtistName} ({item.CreatorName}) from {item.PerformanceLink}");

        // Update progress slider with current item
        ProgressSliderUpdateRequested?.Invoke($"{item.ArtistName} - {item.SongName}", 0, 0, false);

        // Play the item based on its type
        await PlayBasedOnTypeAsync(item, cancellationToken);
    }

    private async Task ShowCountdownAsync(QueueItem item, CancellationToken cancellationToken)
    {
        Callable.From(() => _displayScreen.ShowNextUp(item.SingerName, item.SongName, item.ArtistName, _settings.CountdownLengthSeconds)).CallDeferred();
        
        int remainingSeconds = _settings.CountdownLengthSeconds;
        while (remainingSeconds > 0 && !cancellationToken.IsCancellationRequested)
        {
            Callable.From(() => _displayScreen.UpdateLaunchCountdownSecondsRemaining(remainingSeconds)).CallDeferred();
            ProgressSliderUpdateRequested?.Invoke($"Next up is {item.SingerName} in {remainingSeconds} seconds...", _settings.CountdownLengthSeconds, _settings.CountdownLengthSeconds - remainingSeconds, false);
            remainingSeconds--;
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        }
    }

    private async Task PlayBasedOnTypeAsync(QueueItem item, CancellationToken cancellationToken)
    {
        switch (item.ItemType)
        {
            case ItemType.KarafunWeb:
                await PlayKarafunAsync(item, cancellationToken);
                break;
            case ItemType.Youtube:
                await PlayYoutubeAsync(item, cancellationToken);
                break;
            case ItemType.LocalMp3G:
            case ItemType.LocalMp3GZip:
            case ItemType.LocalMp4:
                PlayLocal(item);
                break;
            default:
                GD.PrintErr($"Unknown item type: {item.ItemType}");
                break;
        }
    }

    private async Task PlayKarafunAsync(QueueItem item, CancellationToken cancellationToken)
    {
        // If the Karafun remote client is connected, use it to queue the song
        // The user has already set up the web player on the display screen, so we just need to queue
        if (_karafunRemoteProvider.IsConnected)
        {
            GD.Print("Using Karafun remote control to queue song");
            
            // Queue the song via remote control - this will start playback on the already-open web player
            var success = await _karafunRemoteProvider.QueueSongFromUrlAsync(item.PerformanceLink, item.SingerName, cancellationToken);
            
            if (!success)
            {
                GD.PrintErr("Failed to queue song via Karafun remote, falling back to browser automation");
                // Fall back to browser automation
                await PlayKarafunViaBrowserAsync(item, cancellationToken);
                return;
            }
            
            // Wait for playback to complete via remote status events
            await WaitForKarafunRemotePlaybackToFinishAsync(cancellationToken);
            
            GD.Print("Karafun remote playback finished.");
            PlaybackFinished?.Invoke(item);
        }
        else
        {
            // Fall back to legacy browser automation approach
            await PlayKarafunViaBrowserAsync(item, cancellationToken);
        }
    }
    
    private async Task PlayKarafunViaBrowserAsync(QueueItem item, CancellationToken cancellationToken)
    {
        Callable.From(() => _displayScreen.HideDisplayScreen()).CallDeferred();
        await _browserProvider.PlayKarafunUrl(item.PerformanceLink, cancellationToken);
        GD.Print("Karafun browser playback finished.");
        PlaybackFinished?.Invoke(item);
    }
    
    private async Task WaitForKarafunRemotePlaybackToFinishAsync(CancellationToken cancellationToken)
    {
        // Wait for Karafun to indicate that the song has finished
        // The remote control status events will tell us when playback state changes
        var playbackStarted = false;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var currentState = _karafunRemoteProvider.CurrentPlaybackState;
            
            // Track if playback has started (state 4 = Playing)
            if (currentState == KOKTKaraokeParty.Web.KarafunPlaybackState.Playing)
            {
                if (!playbackStarted)
                {
                    // First time entering playing state - hide the next-up display
                    Callable.From(() => _displayScreen.HideDisplayScreen()).CallDeferred();
                    playbackStarted = true;
                }
            }
            
            // If we started playing and now we're idle (state 1), song is done
            if (playbackStarted && currentState == KOKTKaraokeParty.Web.KarafunPlaybackState.Idle)
            {
                GD.Print("Karafun remote detected playback finished (idle state)");
                break;
            }
            
            await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
        }
    }

    private async Task PlayYoutubeAsync(QueueItem item, CancellationToken cancellationToken)
    {
        // Check if we have a downloaded file to play locally
        if (!string.IsNullOrEmpty(item.TemporaryDownloadPath) && File.Exists(item.TemporaryDownloadPath))
        {
            GD.Print($"Playing downloaded YouTube video locally: {item.TemporaryDownloadPath}");
            var localQueueItem = CreateLocalQueueItemFromYoutube(item);
            PlayLocal(localQueueItem);
            return;
        }

        // Wait for download if in progress
        if (item.IsDownloading)
        {
            await WaitForDownloadAsync(item, cancellationToken);
            if (!string.IsNullOrEmpty(item.TemporaryDownloadPath) && File.Exists(item.TemporaryDownloadPath))
            {
                var localQueueItem = CreateLocalQueueItemFromYoutube(item);
                PlayLocal(localQueueItem);
                return;
            }
        }

        // Fallback to browser playback
        GD.Print("Download not available, falling back to browser playback");
        Callable.From(() => _displayScreen.HideDisplayScreen()).CallDeferred();
        await _browserProvider.PlayYoutubeUrl(item.PerformanceLink, cancellationToken);
        GD.Print("YouTube playback finished.");
        PlaybackFinished?.Invoke(item);
    }

    private async Task WaitForDownloadAsync(QueueItem item, CancellationToken cancellationToken)
    {
        GD.Print("Waiting for YouTube download to complete...");
        
        while (item.IsDownloading && !cancellationToken.IsCancellationRequested)
        {
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        }
        
        if (!string.IsNullOrEmpty(item.TemporaryDownloadPath) && File.Exists(item.TemporaryDownloadPath))
        {
            GD.Print($"Download completed! Playing locally: {item.TemporaryDownloadPath}");
        }
    }

    private QueueItem CreateLocalQueueItemFromYoutube(QueueItem youtubeItem)
    {
        return new QueueItem
        {
            PerformanceLink = youtubeItem.TemporaryDownloadPath,
            SingerName = youtubeItem.SingerName,
            SongName = youtubeItem.SongName,
            ArtistName = youtubeItem.ArtistName,
            CreatorName = youtubeItem.CreatorName,
            ItemType = ItemType.LocalMp4
        };
    }

    private void PlayLocal(QueueItem item)
    {
        Callable.From(() => _displayScreen.PlayLocal(item)).CallDeferred();
        // Note: Local playback completion is handled via DisplayScreen events
    }

    private void OnLocalPlaybackFinished(string filePath)
    {
        // This will be handled by the main controller through the event
        GD.Print($"Local playback finished: {filePath}");
    }

    public async Task PauseCurrentPlaybackAsync(QueueItem currentItem)
    {
        if (currentItem == null) return;

        switch (currentItem.ItemType)
        {
            case ItemType.KarafunWeb:
                // Prefer remote control if connected, otherwise fall back to browser
                if (_karafunRemoteProvider.IsConnected)
                {
                    await _karafunRemoteProvider.PauseAsync();
                }
                else
                {
                    await _browserProvider.PauseKarafun();
                }
                break;
            case ItemType.Youtube:
                if (string.IsNullOrEmpty(currentItem.TemporaryDownloadPath))
                {
                    await _browserProvider.ToggleYoutubePlayback();
                }
                // Local playback pause is handled by DisplayScreen internally
                break;
            case ItemType.LocalMp3G:
            case ItemType.LocalMp3GZip:
            case ItemType.LocalMp4:
                // Local playback pause is handled by DisplayScreen internally
                break;
        }
    }

    public async Task ResumeCurrentPlaybackAsync(QueueItem currentItem)
    {
        if (currentItem == null) return;

        switch (currentItem.ItemType)
        {
            case ItemType.KarafunWeb:
                // Prefer remote control if connected, otherwise fall back to browser
                if (_karafunRemoteProvider.IsConnected)
                {
                    await _karafunRemoteProvider.ResumeAsync();
                }
                else
                {
                    await _browserProvider.ResumeKarafun();
                }
                break;
            case ItemType.Youtube:
                if (string.IsNullOrEmpty(currentItem.TemporaryDownloadPath))
                {
                    await _browserProvider.ToggleYoutubePlayback();
                }
                // Local playback resume is handled by DisplayScreen internally
                break;
            case ItemType.LocalMp3G:
            case ItemType.LocalMp3GZip:
            case ItemType.LocalMp4:
                // Local playback resume is handled by DisplayScreen internally
                break;
        }
    }

    public void SeekCurrentPlayback(QueueItem currentItem, long positionMs)
    {
        if (currentItem == null) return;

        switch (currentItem.ItemType)
        {
            case ItemType.Youtube:
                if (string.IsNullOrEmpty(currentItem.TemporaryDownloadPath))
                {
                    _ = Task.Run(async () => await _browserProvider.SeekYouTube(positionMs));
                }
                else
                {
                    Callable.From(() => _displayScreen.SeekLocal(positionMs)).CallDeferred();
                }
                break;
            case ItemType.KarafunWeb:
                _ = Task.Run(async () => await _browserProvider.SeekKarafun(positionMs));
                break;
            case ItemType.LocalMp3G:
            case ItemType.LocalMp3GZip:
            case ItemType.LocalMp4:
                Callable.From(() => _displayScreen.SeekLocal(positionMs)).CallDeferred();
                break;
        }
    }
}