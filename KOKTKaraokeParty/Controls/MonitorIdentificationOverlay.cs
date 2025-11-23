using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System.Collections.Generic;

namespace KOKTKaraokeParty.Controls;

public interface IMonitorIdentificationOverlay : IWindow
{
    void ShowForMonitor(int monitorId);
    new void Hide();
}

[Meta(typeof(IAutoNode))]
public partial class MonitorIdentificationOverlay : Window, IMonitorIdentificationOverlay
{
    public override void _Notification(int what) => this.Notify(what);

    [Node("%MonitorNumberLabel")] private ILabel MonitorNumberLabel { get; set; } = default!;
    private Label _fallbackLabel;

    private int _currentMonitorId = -1;

    public override void _Ready()
    {
        // Don't modify ANY window properties - let the scene file handle them
        
        // Make it non-interactive
        SetProcessInput(false);
        
        this.Provide();
        
        // Fallback label reference if AutoInject fails
        if (MonitorNumberLabel == null)
        {
            _fallbackLabel = GetNode<Label>("%MonitorNumberLabel");
        }
    }

    public void ShowForMonitor(int monitorId)
    {
        _currentMonitorId = monitorId;
        
        // Get monitor bounds
        var screenRect = DisplayServer.ScreenGetUsableRect(monitorId);
        
        // Position window in top-left corner of the monitor
        Position = new Vector2I((int)screenRect.Position.X + 50, (int)screenRect.Position.Y + 50);
        // Use a larger size to accommodate the label's offset positioning
        Size = new Vector2I(300, 300);
        
        // Set the monitor number - use scene file labels
        if (MonitorNumberLabel != null)
        {
            MonitorNumberLabel.Text = monitorId.ToString();
        }
        else if (_fallbackLabel != null)
        {
            _fallbackLabel.Text = monitorId.ToString();
        }
        
        // Show the window
        Show();
        
        // Auto-hide after 5 seconds for better visibility
        GetTree().CreateTimer(5.0f).Timeout += () => Hide();
    }

    public new void Hide()
    {
        base.Hide();
        _currentMonitorId = -1;
    }
}

// Manager class to handle multiple monitor overlays
public class MonitorIdentificationManager
{
    private readonly PackedScene _overlayScene;
    private readonly List<MonitorIdentificationOverlay> _activeOverlays = new();
    private readonly Node _parent;

    public MonitorIdentificationManager(Node parent, PackedScene overlayScene)
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