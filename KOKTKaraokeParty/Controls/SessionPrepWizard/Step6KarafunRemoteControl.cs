using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep6KarafunRemoteControl : IVBoxContainer
{
    event Action BackPressed;
    event Action NextPressed;
    void Setup(SessionPrepWizardState state, IKarafunRemoteProviderNode karafunRemoteProvider, Settings settings);
}

[Meta(typeof(IAutoNode))]
public partial class Step6KarafunRemoteControl : VBoxContainer, IStep6KarafunRemoteControl
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action NextPressed;
	
	#region Nodes
	
	[Node] private ILineEdit RoomCodeEdit { get; set; }
	[Node] private IButton ConnectButton { get; set; }
	[Node] private ILabel ConnectionStatusLabel { get; set; }
	[Node] private IButton Step6KarafunRemoteControlBackButton { get; set; }
	[Node] private IButton Step6KarafunRemoteControlNextButton { get; set; }
	
	#endregion
	
	private SessionPrepWizardState _state;
	private IKarafunRemoteProviderNode _karafunRemoteProvider;
	private Settings _settings;
	private bool _eventSubscribed = false;
	
	public void OnReady()
	{
		RoomCodeEdit.TextChanged += OnRoomCodeChanged;
		ConnectButton.Pressed += OnConnectRemote;
		Step6KarafunRemoteControlBackButton.Pressed += () => BackPressed?.Invoke();
		Step6KarafunRemoteControlNextButton.Pressed += () => NextPressed?.Invoke();
	}
	
	public void Setup(SessionPrepWizardState state, IKarafunRemoteProviderNode karafunRemoteProvider, Settings settings)
	{
		_state = state;
		_karafunRemoteProvider = karafunRemoteProvider;
		_settings = settings;
		
		// Subscribe to remote provider events (deferred from OnReady)
		if (!_eventSubscribed)
		{
			_karafunRemoteProvider.ConnectionStatusChanged += OnKarafunConnectionStatusChanged;
			_eventSubscribed = true;
		}
		
		RoomCodeEdit.Text = _state.KarafunRoomCode;
		ConnectionStatusLabel.Text = _state.KarafunRemoteConnected 
			? "✔ Connected" 
			: "Not connected";
		
		UpdateStep6KarafunRemoteControlNextButton();
	}
	
	private void OnRoomCodeChanged(string newText)
	{
		_state.KarafunRoomCode = newText;
		_settings.KarafunRoomCode = newText;
		_settings.SaveToDisk(new FileWrapper());
		
		ConnectButton.Disabled = !SessionPrepWizardLogic.IsValidRoomCodeFormat(newText);
	}
	
	private async void OnConnectRemote()
	{
		if (!SessionPrepWizardLogic.IsValidRoomCodeFormat(_state.KarafunRoomCode))
		{
			ConnectionStatusLabel.Text = "Invalid room code format (must be 6 digits)";
			return;
		}
		
		ConnectButton.Disabled = true;
		ConnectionStatusLabel.Text = "⏳ Connecting...";
		
		var success = await _karafunRemoteProvider.ConnectAsync(_state.KarafunRoomCode);
		
		if (success)
		{
			_state.KarafunRemoteConnected = true;
			ConnectionStatusLabel.Text = "✔ Connected";
		}
		else
		{
			ConnectionStatusLabel.Text = "❌ Connection failed";
			ConnectButton.Disabled = false;
		}
		
		UpdateStep6KarafunRemoteControlNextButton();
	}
	
	private void OnKarafunConnectionStatusChanged(KarafunRemoteConnectionStatus status)
	{
		Callable.From(() =>
		{
			_state.KarafunRemoteConnected = status == KarafunRemoteConnectionStatus.Connected;
			_state.KarafunRemoteMessage = status.ToString();
			
			ConnectionStatusLabel.Text = status switch
			{
				KarafunRemoteConnectionStatus.Connected => "✔ Connected",
				KarafunRemoteConnectionStatus.Connecting => "⏳ Connecting...",
				KarafunRemoteConnectionStatus.FetchingSettings => "⏳ Fetching settings...",
				KarafunRemoteConnectionStatus.Reconnecting => "⏳ Reconnecting...",
				KarafunRemoteConnectionStatus.Error => "❌ Connection error",
				_ => "Not connected"
			};
			
			UpdateStep6KarafunRemoteControlNextButton();
		}).CallDeferred();
	}
	
	private void UpdateStep6KarafunRemoteControlNextButton()
	{
		Step6KarafunRemoteControlNextButton.Disabled = !SessionPrepWizardLogic.IsStep6KarafunRemoteControlNextEnabled(_state);
	}
}