using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep2SetDisplay : IVBoxContainer
{
    event Action BackPressed;
    event Action NextPressed;
    void Setup(SessionPrepWizardState state, IDisplayScreen displayScreen, IMonitorIdentificationManager monitorIdManager, Settings settings);
}

[Meta(typeof(IAutoNode))]
public partial class Step2SetDisplay : VBoxContainer, IStep2SetDisplay
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action NextPressed;
	
	#region Nodes
	
	[Node] private ISpinBox MonitorSpinBox { get; set; }
	[Node] private IButton IdentifyMonitorsButton { get; set; }
	[Node] private ILabel MonitorWarningLabel { get; set; }
	[Node] private IButton Step2SetDisplayBackButton { get; set; }
	[Node] private IButton Step2SetDisplayNextButton { get; set; }
	
	#endregion
	
	private SessionPrepWizardState _state;
	private IDisplayScreen _displayScreen;
	private IMonitorIdentificationManager _monitorIdManager;
	private Settings _settings;
	
	public void OnReady()
	{
		MonitorSpinBox.ValueChanged += OnMonitorChanged;
		IdentifyMonitorsButton.Pressed += OnIdentifyMonitors;
		Step2SetDisplayBackButton.Pressed += () => BackPressed?.Invoke();
		Step2SetDisplayNextButton.Pressed += () => NextPressed?.Invoke();
	}
	
	public void Setup(SessionPrepWizardState state, IDisplayScreen displayScreen, IMonitorIdentificationManager monitorIdManager, Settings settings)
	{
		_state = state;
		_displayScreen = displayScreen;
		_monitorIdManager = monitorIdManager;
		_settings = settings;
		
		MonitorSpinBox.MinValue = 0;
		MonitorSpinBox.MaxValue = Math.Max(0, _state.AvailableMonitorCount - 1);
		MonitorSpinBox.Value = _state.SelectedMonitor;
		
		// Hide back button if Step 1 wasn't shown (nothing to go back to)
		Step2SetDisplayBackButton.Visible = _state.HasSavedQueue;
		
		UpdateMonitorWarning();
	}
	
	private void OnMonitorChanged(double value)
	{
		_state.SelectedMonitor = (int)value;
		_settings.DisplayScreenMonitor = _state.SelectedMonitor;
		_settings.SaveToDisk(new FileWrapper());
		
		Callable.From(() => _displayScreen.SetMonitorId(_state.SelectedMonitor)).CallDeferred();
		UpdateMonitorWarning();
	}
	
	private void OnIdentifyMonitors()
	{
		_monitorIdManager.ShowAllMonitors();
	}
	
	private void UpdateMonitorWarning()
	{
		var singleMonitor = _state.AvailableMonitorCount <= 1;
		MonitorWarningLabel.Visible = singleMonitor;
		MonitorWarningLabel.TooltipText = singleMonitor 
			? "Warning: Only one monitor detected. The display screen will cover this window."
			: "";
	}
}
