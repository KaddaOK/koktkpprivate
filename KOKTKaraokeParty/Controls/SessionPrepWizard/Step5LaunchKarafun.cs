using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep5LaunchKarafun : IVBoxContainer
{
    event Action BackPressed;
    event Action NextPressed;
    void Setup(SessionPrepWizardState state, IBrowserProviderNode browserProvider, Settings settings);
}

[Meta(typeof(IAutoNode))]
public partial class Step5LaunchKarafun : VBoxContainer, IStep5LaunchKarafun
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action NextPressed;
	
	#region Nodes
	
	[Node] private IButton LaunchKarafunWebPlayerButton { get; set; }
	[Node] private IMarginContainer Step1WebLaunch { get; set; }
	[Node] private IMarginContainer Step1ManualLaunch { get; set; }
	[Node] private IButton Step5LaunchKarafunBackButton { get; set; }
	[Node] private IButton Step5LaunchKarafunNextButton { get; set; }
	
	#endregion
	
	private SessionPrepWizardState _state;
	private IBrowserProviderNode _browserProvider;
	private Settings _settings;
	
	public void OnReady()
	{
		LaunchKarafunWebPlayerButton.Pressed += OnLaunchKarafunWebPlayer;
		Step5LaunchKarafunBackButton.Pressed += () => BackPressed?.Invoke();
		Step5LaunchKarafunNextButton.Pressed += () => NextPressed?.Invoke();
	}
	
	public void Setup(SessionPrepWizardState state, IBrowserProviderNode browserProvider, Settings settings)
	{
		_state = state;
		_browserProvider = browserProvider;
		_settings = settings;
		
		Step1WebLaunch.Visible = _state.KarafunMode == KarafunMode.ControlledBrowser;
		Step1ManualLaunch.Visible = _state.KarafunMode == KarafunMode.InstalledApp;
		
		UpdateStep5LaunchKarafunNextButton();
	}
	
	private async void OnLaunchKarafunWebPlayer()
	{
		LaunchKarafunWebPlayerButton.Disabled = true;
		LaunchKarafunWebPlayerButton.Text = "Launching...";
		
		try
		{
			await _browserProvider.LaunchKarafunWebPlayer(_settings);
			_state.KarafunWebPlayerLaunched = true;
			LaunchKarafunWebPlayerButton.Text = "✔ Launched";
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to launch Karafun web player: {ex.Message}");
			LaunchKarafunWebPlayerButton.Disabled = false;
			LaunchKarafunWebPlayerButton.Text = "Launch Karafun Web Player";
		}
		
		UpdateStep5LaunchKarafunNextButton();
	}
	
	private void UpdateStep5LaunchKarafunNextButton()
	{
		Step5LaunchKarafunNextButton.Disabled = !SessionPrepWizardLogic.IsStep5LaunchKarafunNextEnabled(_state);
	}
}