using Godot;

public partial class DisplayScreen : Window
{
	public bool IsDismissed { get; private set; }
	public int MonitorId { get; private set; }

	public Control NextUpScene { get; private set;}
	public Control EmptyQueueScene { get; private set; }

	public Label NextUpSingerNameLabel { get; private set; }
	public Label NextUpSongNameLabel { get; private set; }
	public Label NextUpArtistNameLabel { get; private set; }
	public Label NextUpLaunchCountdownLabel { get; private set; }
	public Label CountdownPausedIndicator { get; private set; }
	public Label BgMusicNowPlayingLabel { get; private set; }
	public Label BgMusicPausedIndicator { get; private set; }

	public override void _Ready()
	{
		WindowInput += DisplayScreenWindowInput;

		NextUpScene = GetNode<Control>($"%{nameof(NextUpScene)}");
		EmptyQueueScene = GetNode<Control>($"%{nameof(EmptyQueueScene)}");

		NextUpSingerNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpSingerNameLabel)}");
		NextUpSongNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpSongNameLabel)}");
		NextUpArtistNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpArtistNameLabel)}");
		NextUpLaunchCountdownLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpLaunchCountdownLabel)}");
		CountdownPausedIndicator = NextUpScene.GetNode<Label>($"%{nameof(CountdownPausedIndicator)}");
		BgMusicNowPlayingLabel = GetNode<Label>($"%{nameof(BgMusicNowPlayingLabel)}");
		BgMusicPausedIndicator = GetNode<Label>($"%{nameof(BgMusicPausedIndicator)}");
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
		NextUpSingerNameLabel.Text = singer;
		NextUpSongNameLabel.Text = song;
		NextUpArtistNameLabel.Text = artist;
		NextUpLaunchCountdownLabel.Text = launchCountdownLengthSeconds.ToString();
		NextUpScene.Visible = true;
		EmptyQueueScene.Visible = false;
		ShowDisplayScreen();
	}

	public void UpdateLaunchCountdownSecondsRemaining(int secondsRemaining)
	{
		NextUpLaunchCountdownLabel.Text = secondsRemaining.ToString();
	}

	public void UpdateCountdownPausedIndicator(bool isPaused)
	{
		CountdownPausedIndicator.Visible = isPaused;
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
		ShowDisplayScreen();
	}

	public void DisplayScreenWindowInput(InputEvent @event)
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
