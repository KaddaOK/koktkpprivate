using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep3SelectServices : IVBoxContainer
{
    event Action BackPressed;
    event Action NextPressed;
    void Setup(SessionPrepWizardState state, Settings settings);
}

[Meta(typeof(IAutoNode))]
public partial class Step3SelectServices : VBoxContainer, IStep3SelectServices
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action NextPressed;
	
	#region Nodes
	
	[Node] private ICheckBox UseLocalFilesCheckBox { get; set; }
	[Node] private ILabel LocalFilesInfoLabel { get; set; }
	[Node] private ICheckBox UseYouTubeCheckBox { get; set; }
	[Node] private ICheckBox UseKarafunCheckBox { get; set; }
	[Node] private IButton ControlledBrowserRadio { get; set; }
	[Node] private IButton InstalledAppRadio { get; set; }
	[Node] private IControl KarafunModeContainer { get; set; }
	[Node] private IButton Step3SelectServicesBackButton { get; set; }
	[Node] private IButton Step3SelectServicesNextButton { get; set; }
	
	#endregion
	
	private SessionPrepWizardState _state;
	private Settings _settings;
	
	public void OnReady()
	{
		UseLocalFilesCheckBox.Toggled += OnServiceCheckboxToggled;
		UseYouTubeCheckBox.Toggled += OnServiceCheckboxToggled;
		UseKarafunCheckBox.Toggled += OnKarafunCheckboxToggled;
		ControlledBrowserRadio.Pressed += () => OnKarafunModeChanged(KarafunMode.ControlledBrowser);
		InstalledAppRadio.Pressed += () => OnKarafunModeChanged(KarafunMode.InstalledApp);
		Step3SelectServicesBackButton.Pressed += () => BackPressed?.Invoke();
		Step3SelectServicesNextButton.Pressed += () => NextPressed?.Invoke();
	}
	
	public void Setup(SessionPrepWizardState state, Settings settings)
	{
		_state = state;
		_settings = settings;
		
		// Set checkbox states
		UseLocalFilesCheckBox.ButtonPressed = _state.UseLocalFiles;
		UseYouTubeCheckBox.ButtonPressed = _state.UseYouTube;
		UseKarafunCheckBox.ButtonPressed = _state.UseKarafun;
		
		// Disable checkboxes for services required by restored queue
		UseLocalFilesCheckBox.Disabled = _state.LocalFilesRequiredByQueue;
		UseYouTubeCheckBox.Disabled = _state.YouTubeRequiredByQueue;
		UseKarafunCheckBox.Disabled = _state.KarafunRequiredByQueue;
		
		// Setup Karafun mode radio buttons
		ControlledBrowserRadio.ButtonPressed = _state.KarafunMode == KarafunMode.ControlledBrowser;
		InstalledAppRadio.ButtonPressed = _state.KarafunMode == KarafunMode.InstalledApp;
		KarafunModeContainer.Visible = _state.UseKarafun;
		
		// Update local files info label
		if (_state.LocalSongCount > 0)
		{
			var songWord = _state.LocalSongCount == 1 ? "song" : "songs";
			var artistWord = _state.LocalArtistCount == 1 ? "artist" : "artists";
			var pathWord = _state.LocalPathCount == 1 ? "path" : "paths";
			LocalFilesInfoLabel.Text = $"{_state.LocalSongCount} {songWord} by {_state.LocalArtistCount} {artistWord} across {_state.LocalPathCount} file {pathWord}";
		}
		else
		{
			LocalFilesInfoLabel.Text = "No files scanned yet. Do this on the Local Files tab once the app has started.";
		}
		
		UpdateStep3SelectServicesNextButton();
	}
	
	private void OnServiceCheckboxToggled(bool pressed)
	{
		_state.UseLocalFiles = UseLocalFilesCheckBox.ButtonPressed;
		_state.UseYouTube = UseYouTubeCheckBox.ButtonPressed;
		
		// Save to settings
		_settings.UseLocalFiles = _state.UseLocalFiles;
		_settings.UseYouTube = _state.UseYouTube;
		_settings.SaveToDisk(new FileWrapper());
		
		UpdateStep3SelectServicesNextButton();
	}
	
	private void OnKarafunCheckboxToggled(bool pressed)
	{
		_state.UseKarafun = UseKarafunCheckBox.ButtonPressed;
		KarafunModeContainer.Visible = _state.UseKarafun;
		
		_settings.UseKarafun = _state.UseKarafun;
		_settings.SaveToDisk(new FileWrapper());
		
		UpdateStep3SelectServicesNextButton();
	}
	
	private void OnKarafunModeChanged(KarafunMode mode)
	{
		_state.KarafunMode = mode;
		ControlledBrowserRadio.ButtonPressed = mode == KarafunMode.ControlledBrowser;
		InstalledAppRadio.ButtonPressed = mode == KarafunMode.InstalledApp;
		
		_settings.KarafunMode = mode;
		_settings.SaveToDisk(new FileWrapper());
	}
	
	private void UpdateStep3SelectServicesNextButton()
	{
		Step3SelectServicesNextButton.Disabled = !SessionPrepWizardLogic.IsStep3SelectServicesNextEnabled(_state);
	}
}