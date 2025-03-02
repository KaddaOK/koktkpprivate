using System;
using System.Threading;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

namespace KOKTKaraokeParty;

public interface IDisplayScreen : IWindow
{
    public bool IsDismissed { get; }

    event DisplayScreen.LocalPlaybackFinishedEventHandler LocalPlaybackFinished;
    event DisplayScreen.LocalPlaybackProgressEventHandler LocalPlaybackProgress;
    event DisplayScreen.LocalPlaybackDurationChangedEventHandler LocalPlaybackDurationChanged;

    void SetMonitorId(int monitorId);
    void Dismiss();
    void ClearDismissed();
    void ShowDisplayScreen();
    void HideDisplayScreen();
    void ShowNextUp(string singer, string song, string artist, int launchCountdownLengthSeconds);
    void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining);
    void ToggleQueuePaused(bool isPaused);
    void UpdateBgMusicNowPlaying(string nowPlaying);
    void UpdateBgMusicPaused(bool isPaused);
    void ShowEmptyQueueScreen();
    void PlayLocal(QueueItem item);
    void SeekLocal(long positionMs);
    void CancelIfPlaying();

    Task GeneratePluginsCache();
    Task InitializeVlc();
}

[Meta(typeof(IAutoNode))]
public partial class DisplayScreen : Window, IDisplayScreen
{
    public override void _Notification(int what) => this.Notify(what);

    public bool IsDismissed { get; private set; }
    public int MonitorId { get; private set; }

    #region Nodes

    [Node] public INextUpDisplay NextUpScene { get; private set; } = default!;
    [Node] public IControl EmptyQueueScene { get; private set; } = default!;

    [Node] public IHBoxContainer BgMusicPlayingListing { get; private set; } = default!;
    [Node] public ILabel TemporarilyDismissLabel { get; private set; } = default!;
    [Node] public ILabel BgMusicNowPlayingLabel { get; private set; } = default!;
    [Node] public ILabel BgMusicPausedIndicator { get; private set; } = default!;

    [Node] public ICdgRendererNode CdgRendererNode { get; private set; } = default!;

    #endregion

    #region Signals
/*
    [Signal] public delegate void LocalPlaybackFinishedEventHandler(string wasPlaying);
    [Signal] public delegate void LocalPlaybackProgressEventHandler(long progressMs);
    [Signal] public delegate void LocalPlaybackDurationChangedEventHandler(long durationMs);
*/
    public delegate void LocalPlaybackFinishedEventHandler(string wasPlaying);
    public delegate void LocalPlaybackProgressEventHandler(long progressMs);
    public delegate void LocalPlaybackDurationChangedEventHandler(long durationMs);
    public event LocalPlaybackFinishedEventHandler LocalPlaybackFinished;
    public event LocalPlaybackProgressEventHandler LocalPlaybackProgress;
    public event LocalPlaybackDurationChangedEventHandler LocalPlaybackDurationChanged;
    #endregion

    private IVlcMp4Player VlcMp4Player { get; set; } // TODO: this properly

    public void OnReady()
    {
        VlcMp4Player = new VlcMp4Player(); // TODO: this properly
/*
        VlcMp4Player.PlaybackFinished += (wasPlaying) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackFinished, wasPlaying);
        VlcMp4Player.PlaybackProgress += (progressMs) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackProgress, progressMs);
        VlcMp4Player.PlaybackDurationChanged += (durationMs) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackDurationChanged, durationMs);
        */
        VlcMp4Player.PlaybackFinished += (wasPlaying) => LocalPlaybackFinished?.Invoke(wasPlaying);
        VlcMp4Player.PlaybackProgress += (progressMs) => LocalPlaybackProgress?.Invoke(progressMs);
        VlcMp4Player.PlaybackDurationChanged += (durationMs) => LocalPlaybackDurationChanged?.Invoke(durationMs);
        
        WindowInput += DisplayScreenWindowInput;
/*
        CdgRendererNode.PlaybackFinished += (wasPlaying) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackFinished, wasPlaying);
        CdgRendererNode.PlaybackProgress += (progressMs) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackProgress, progressMs);
        CdgRendererNode.PlaybackDurationChanged += (durationMs) => CallDeferred(nameof(EmitSignal), SignalName.LocalPlaybackDurationChanged, durationMs);
        */
        CdgRendererNode.PlaybackFinished += (wasPlaying) => LocalPlaybackFinished?.Invoke(wasPlaying);
        CdgRendererNode.PlaybackProgress += (progressMs) => LocalPlaybackProgress?.Invoke(progressMs);
        CdgRendererNode.PlaybackDurationChanged += (durationMs) => LocalPlaybackDurationChanged?.Invoke(durationMs);
    }

    public async Task GeneratePluginsCache()
    {
        await VlcMp4Player.GeneratePluginsCache();
    }
    public async Task InitializeVlc()
    {
        await VlcMp4Player.InitializeVlc();
    }

    public void PlayLocal(QueueItem item)
    {

        NextUpScene.Visible = false;
        EmptyQueueScene.Visible = false;
        BgMusicPlayingListing.Visible = false;
        TemporarilyDismissLabel.Visible = false;

        if (item.ItemType == ItemType.LocalMp3G || item.ItemType == ItemType.LocalMp3GZip)
        {
            CdgRendererNode.Visible = true;
            ShowDisplayScreen();
            CdgRendererNode.Start(item.PerformanceLink, CancellationToken.None);
        }
        else if (item.ItemType == ItemType.LocalMp4)
        {
            VlcMp4Player.Start(item.PerformanceLink, CancellationToken.None);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void SeekLocal(long positionMs)
    {
        // TODO: why don't you save the item being played so you can do it on item.ItemType instead of this garbage
        if (CdgRendererNode.Visible)
        {
            CdgRendererNode.Seek(positionMs);
        }
        else if (VlcMp4Player.IsPlaying || VlcMp4Player.IsPaused)
        {
            VlcMp4Player.Seek(positionMs);
        }
    }

    public void CancelIfPlaying()
    {
        GD.Print("CancelIfPlaying");
        if (CdgRendererNode.Visible)
        {
            GD.Print("CdgRendererNode is visible; calling Stop...");
            CdgRendererNode.Stop();
        }
        if (VlcMp4Player.IsPlaying || VlcMp4Player.IsPaused)
        {
            GD.Print("VlcMp4Player was ready to do something; calling Stop...");
            VlcMp4Player.Stop();
        }
    }

    public void SetMonitorId(int monitorId)
    {
        MonitorId = monitorId;
    }
    public void Dismiss()
    {
        IsDismissed = true;
        HideDisplayScreen();
    }
    public void ClearDismissed()
    {
        IsDismissed = false;
    }
    public void ShowDisplayScreen()
    {
        // TODO: make this configurable?
        Mode = Window.ModeEnum.Fullscreen;

        InitialPosition = Window.WindowInitialPosition.CenterOtherScreen;
        CurrentScreen = MonitorId;
        Visible = true;
        IsDismissed = false;
        Show();
        AlwaysOnTop = true;
        GrabFocus();
    }

    public void HideDisplayScreen()
    {
        AlwaysOnTop = false;
        Visible = false;
        Hide();
    }

    public void ShowNextUp(string singer, string song, string artist, int launchCountdownLengthSeconds)
    {
        NextUpScene.SetNextUpInfo(singer, song, artist, launchCountdownLengthSeconds);
        NextUpScene.Visible = true;
        EmptyQueueScene.Visible = false;
        BgMusicPlayingListing.Visible = true;
        TemporarilyDismissLabel.Visible = true;
        CdgRendererNode.Visible = false;
        ShowDisplayScreen();
    }

    public void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining)
    {
        NextUpScene.UpdateLaunchCountdownSecondsRemaining(secondsRemaining);
    }

    public void ToggleQueuePaused(bool isPause)
    {
        NextUpScene.ToggleCountdownPaused(isPause);
        if (CdgRendererNode.Visible)
        {
            CdgRendererNode.TogglePaused(isPause);
        }
        VlcMp4Player.TogglePaused(isPause);
        /*if (VlcMp4Player.IsPlaying && isPause)
        {
            VlcMp4Player.Pause();
        }
        else if (VlcMp4Player.IsPaused && !isPause)
        {
            VlcMp4Player.Resume();
        }*/
    }

    public void UpdateBgMusicNowPlaying(string nowPlaying)
    {
        BgMusicNowPlayingLabel.Text = nowPlaying;
    }

    public void UpdateBgMusicPaused(bool isPaused)
    {
        BgMusicPausedIndicator.Visible = isPaused;
    }

    public void ShowEmptyQueueScreen()
    {
        NextUpScene.Visible = false;
        EmptyQueueScene.Visible = true;
        BgMusicPlayingListing.Visible = true;
        TemporarilyDismissLabel.Visible = true;
        CdgRendererNode.Visible = false;
        ShowDisplayScreen();
    }

    private void DisplayScreenWindowInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            GD.Print($"Key event: {keyEvent.Pressed}, {keyEvent.Keycode}");
            if (keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
            {
                Dismiss();
            }
        }
    }
}
