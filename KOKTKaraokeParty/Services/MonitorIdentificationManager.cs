// Manager class to handle multiple monitor overlays
using System.Collections.Generic;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Controls;

namespace KOKTKaraokeParty.Services;

public interface IMonitorIdentificationManager : INode
{
	void Initialize(Node parent, PackedScene overlayScene);
	void ShowAllMonitors();
	void HideAll();
}

[Meta(typeof(IAutoNode))]
public partial class MonitorIdentificationManager : Node, IMonitorIdentificationManager
{
	private PackedScene _overlayScene;
	private readonly List<MonitorIdentificationOverlay> _activeOverlays = new();
	private Node _parent;

	public void Initialize(Node parent, PackedScene overlayScene)
	{
		_parent = parent;
		_overlayScene = overlayScene;
	}

	public void ShowAllMonitors()
	{
		HideAll();
		
		var screenCount = DisplayServer.GetScreenCount();
		for (int i = 0; i < screenCount; i++)
		{
			var overlay = _overlayScene.Instantiate<MonitorIdentificationOverlay>();
			_parent.AddChild(overlay);
			_activeOverlays.Add(overlay);
			overlay.ShowForMonitor(i);
		}
	}

	public void HideAll()
	{
		foreach (var overlay in _activeOverlays)
		{
			overlay?.QueueFree();
		}
		_activeOverlays.Clear();
	}
}
