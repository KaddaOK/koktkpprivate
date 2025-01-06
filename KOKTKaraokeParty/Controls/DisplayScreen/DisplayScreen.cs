using System;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

namespace KOKTKaraokeParty;

public interface IDisplayScreen : IWindow
{
    public bool IsDismissed { get; }

    event DisplayScreen.LocalPlaybackFinishedEventHandler LocalPlaybackFinished;

    void SetMonitorId(int monitorId);
    void Dismiss();
    void ShowDisplayScreen();
    void HideDisplayScreen();
    void ShowNextUp(string singer, string song, string artist, int launchCountdownLengthSeconds);
    void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining);
    void ToggleCountdownPaused(bool isPaused);
    void UpdateBgMusicNowPlaying(string nowPlaying);
    void UpdateBgMusicPaused(bool isPaused);
    void ShowEmptyQueueScreen();
    void PlayLocal(QueueItem item);
}

[Meta(typeof(IAutoNode))]
public partial class DisplayScreen : Window, IDisplayScreen
{
    public override void _Notification(int what) => this.Notify(what);

    public bool IsDismissed { get; private set; }
    public int MonitorId { get; private set; }

    #region Nodes

    [Node] private INextUpDisplay NextUpScene { get; set; } = default!;
    [Node] private IControl EmptyQueueScene { get; set; } = default!;

    [Node] private IHBoxContainer BgMusicPlayingListing { get; set; } = default!;
    [Node] private ILabel BgMusicNowPlayingLabel { get; set; } = default!;
    [Node] private ILabel BgMusicPausedIndicator { get; set; } = default!;

    [Node] private CdgRendererNode CdgRendererNode { get; set; } = default!;

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
        if (item.ItemType == ItemType.LocalMp3G || item.ItemType == ItemType.LocalMp3GZip)
        {
            CdgRendererNode.Visible = true;
            ShowDisplayScreen();
            CdgRendererNode.Play(item.PerformanceLink);
        }
        else
        {
            throw new NotImplementedException();
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
    public void ShowDisplayScreen()
    {
        // TODO: make this configurable
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
        CdgRendererNode.Visible = false;
        ShowDisplayScreen();
    }

    public void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining)
    {
        NextUpScene.UpdateLaunchCountdownSecondsRemaining(secondsRemaining);
    }

    public void ToggleCountdownPaused(bool isPaused)
    {
        NextUpScene.ToggleCountdownPaused(isPaused);
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
        CdgRendererNode.Visible = false;
        ShowDisplayScreen();
    }

    private void DisplayScreenWindowInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
            {
                Dismiss();
            }
        }
    }
}
