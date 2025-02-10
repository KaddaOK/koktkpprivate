using System;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using LibVLCSharp.Shared;

namespace KOKTKaraokeParty;

public interface IDisplayScreen : IWindow
{
    public bool IsDismissed { get; }

    event DisplayScreen.LocalPlaybackFinishedEventHandler LocalPlaybackFinished;

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
    void CancelIfPlaying();
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

    [Signal] public delegate void LocalPlaybackFinishedEventHandler(string wasPlaying);

    #endregion

    public void OnReady()
    {
        WindowInput += DisplayScreenWindowInput;
        CdgRendererNode.PlaybackFinished += (wasPlaying) => EmitSignal(SignalName.LocalPlaybackFinished, wasPlaying);
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
            CdgRendererNode.Play(item.PerformanceLink);
        }
        else if (item.ItemType == ItemType.LocalMp4)
        {
            PlayVideo(item.PerformanceLink);
        }
        else
        {
            throw new NotImplementedException();
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
        if (vlcMediaPlayer?.IsPlaying ?? false)
        {
            GD.Print("vlcMediaPlayer is playing; calling StopVideo...");
            StopVideo();
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

    public void ToggleQueuePaused(bool isPaused)
    {
        NextUpScene.ToggleCountdownPaused(isPaused);
        if (CdgRendererNode.Visible)
        {
            CdgRendererNode.TogglePaused(isPaused);
        }
        if (vlcMediaPlayer != null)
        {
            if (vlcMediaPlayer.IsPlaying && isPaused)
            {
                vlcMediaPlayer.Pause();
            }
            else if (!vlcMediaPlayer.IsPlaying && !isPaused)
            {
                vlcMediaPlayer.Play();
            }
        }
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

    #region libvlc stuff
    private Media _media;
    private IntPtr windowHandle;
    private MediaPlayer vlcMediaPlayer;
    private LibVLC libVLC;
    private Window newWindow;
    private string videoPathPlaying;

    public void PlayVideo(string videoPath)
    {
        libVLC ??= new LibVLC();
        vlcMediaPlayer = new MediaPlayer(libVLC);
        // Create a new Media instance with the path to the video file
        _media = new Media(libVLC, new Uri(videoPath));

        // Set the window handle of the video player to the Godot window handle
        windowHandle = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, 1);
       
        vlcMediaPlayer.Hwnd = windowHandle;

        // Play the video
        videoPathPlaying = videoPath;
        vlcMediaPlayer.Play(_media);
        // TODO: report progress: vlcMediaPlayer.PositionChanged += (sender, args) => GD.Print($"Position: {vlcMediaPlayer.Position}");
        // Subscribe to the EndReached event so we can clean up
        vlcMediaPlayer.EndReached += MediaPlayerOnEndReached;
        // We can clean up the Media instance now
        _media.Dispose();
    }

    private async void MediaPlayerOnEndReached(object sender, EventArgs e)
    {
        // There will be issues if you try to clean up exactly when the video ends
        // so we'll wait a tiny bit before cleaning up
        await Task.Delay(100);
        // Clean up
        vlcMediaPlayer?.Dispose();
        vlcMediaPlayer = null;
        EmitSignal(SignalName.LocalPlaybackFinished, videoPathPlaying);
        videoPathPlaying = null;
    }

    private async void StopVideo()
    {
        if (vlcMediaPlayer?.IsPlaying ?? false)
        {
            vlcMediaPlayer?.Stop();
        }
        await Task.Delay(100);
        vlcMediaPlayer?.Dispose();
        vlcMediaPlayer = null;
    }

    #endregion
}
