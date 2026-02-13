using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep7PositionKarafunDisplay : IVBoxContainer
{
    event Action BackPressed;
    event Action FinishPressed;
    void Setup(SessionPrepWizardState state);
}

[Meta(typeof(IAutoNode))]
public partial class Step7PositionKarafunDisplay : VBoxContainer, IStep7PositionKarafunDisplay
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action FinishPressed;
	
	#region Nodes
	
	[Node] private IMarginContainer StepMarginContainer4 { get; set; }
	[Node] private IButton Step7PositionKarafunDisplayBackButton { get; set; }
	[Node] private IButton Step7PositionKarafunDisplayFinishButton { get; set; }
	
	#endregion
	
	public void OnReady()
	{
		Step7PositionKarafunDisplayBackButton.Pressed += () => BackPressed?.Invoke();
		Step7PositionKarafunDisplayFinishButton.Pressed += () => FinishPressed?.Invoke();
	}
	
	public void Setup(SessionPrepWizardState state)
	{
		StepMarginContainer4.Visible = state.KarafunMode == KarafunMode.ControlledBrowser;
	}
}