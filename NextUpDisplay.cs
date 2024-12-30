using Godot;

public partial class NextUpDisplay : VBoxContainer
{
	[Export] private Label NextUpSingerNameLabel;
	[Export] private Button NextUpSongNameLabel;
	[Export] private Button NextUpArtistNameLabel;
	[Export] private Button NextUpLaunchCountdownLabel;

	public void SetNextUpInfo(string singerName, string songName, string artistName, int launchCountdown)
	{
		NextUpSingerNameLabel.Text = singerName;
		NextUpSongNameLabel.Text = songName;
		NextUpArtistNameLabel.Text = artistName;
	}
	public void SetLaunchCountdown(int launchCountdown)
	{
		NextUpLaunchCountdownLabel.Text = launchCountdown.ToString();
	}
}
