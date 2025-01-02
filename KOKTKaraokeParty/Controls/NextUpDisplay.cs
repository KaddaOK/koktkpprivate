using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

namespace KOKTKaraokeParty;

public interface INextUpDisplay : IVBoxContainer
{
    void SetNextUpInfo(string singer, string song, string artist, int launchCountdownLengthSeconds);
    void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining);
    void ToggleCountdownPaused(bool isPaused);
}

[Meta(typeof(IAutoNode))]
public partial class NextUpDisplay : VBoxContainer, INextUpDisplay
{
    public override void _Notification(int what) => this.Notify(what);

    [Node] private Label NextUpSingerNameLabel { get; set; } = default!;
    [Node] private Label NextUpSongNameLabel { get; set; } = default!;
    [Node] private Label NextUpArtistNameLabel { get; set; } = default!;
    [Node] private Label NextUpLaunchCountdownLabel { get; set; } = default!;
    [Node] private Label CountdownPausedIndicator { get; set; } = default!;

    public void SetNextUpInfo(string singer, string song, string artist, int launchCountdownLengthSeconds)
    {
        NextUpSingerNameLabel.Text = singer;
        NextUpSongNameLabel.Text = song;
        NextUpArtistNameLabel.Text = artist;
        NextUpLaunchCountdownLabel.Text = launchCountdownLengthSeconds.ToString();
    }

    public void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining)
    {
        NextUpLaunchCountdownLabel.Text = secondsRemaining.ToString();
    }

    public void ToggleCountdownPaused(bool isPaused)
    {
        CountdownPausedIndicator.Visible = isPaused;
    }
}
