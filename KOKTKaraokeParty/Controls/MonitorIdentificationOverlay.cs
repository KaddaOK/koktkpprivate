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
        // MINIMAL window configuration - remove all fancy properties
        // AlwaysOnTop = true;
        // Borderless = true;
        // TransparentBg = true;
        // Unresizable = true;
        
        // Make it non-interactive
        SetProcessInput(false);
        
        // TEMPORARY: Create UI programmatically to test if scene file is the issue
        CreateTestUI();
        
        this.Provide();
        
        // Debug: Check what nodes we have after creation
        GD.Print($"After CreateTestUI - Window children count: {GetChildren().Count}");
        foreach (Node child in GetChildren())
        {
            GD.Print($"Child: {child.Name} ({child.GetType().Name})");
        }
        
        GD.Print("MonitorIdentificationOverlay _Ready completed");
    }
    
    private void CreateTestUI()
    {
        // Remove any existing children first
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        
        // Create a simple test UI programmatically
        var background = new ColorRect();
        background.Name = "TestBackground";
        background.Color = Colors.Red; // Make it bright red so we can definitely see it
        background.Size = new Vector2(300, 300);
        background.Position = Vector2.Zero;
        AddChild(background);
        
        var label = new Label();
        label.Name = "TestLabel";
        label.Text = "TEST";
        label.Position = new Vector2(100, 100);
        label.Size = new Vector2(100, 100);
        
        // Make the text very visible
        var labelSettings = new LabelSettings();
        labelSettings.FontSize = 48;
        labelSettings.FontColor = Colors.White;
        label.LabelSettings = labelSettings;
        
        AddChild(label);
        
        GD.Print("CreateTestUI completed - created red background and white TEST label");
    }

    public void ShowForMonitor(int monitorId)
    {
        _currentMonitorId = monitorId;
        
        // Get monitor bounds
        var screenRect = DisplayServer.ScreenGetUsableRect(monitorId);
        
        GD.Print($"ShowForMonitor {monitorId}: screenRect = {screenRect}");
        
        // Position window in top-left corner of the monitor
        Position = new Vector2I((int)screenRect.Position.X + 50, (int)screenRect.Position.Y + 50);
        // Use a larger size to accommodate the label's offset positioning
        Size = new Vector2I(300, 300);
        
        GD.Print($"Window positioned at {Position}, size {Size}");
        
        // Set the monitor number - use test label
        var testLabel = GetNode<Label>("TestLabel");
        if (testLabel != null)
        {
            testLabel.Text = monitorId.ToString();
        }
        else
        {
            GD.PrintErr("TestLabel not found!");
        }
        
        // Force window to be visible and to front
        Visible = true;
        Show();
        GrabFocus();
        
        GD.Print($"Window shown - Monitor {monitorId}, Position: {Position}, Size: {Size}, Visible: {Visible}");
        GD.Print($"Window properties - AlwaysOnTop: {AlwaysOnTop}, Borderless: {Borderless}, TransparentBg: {TransparentBg}");
        
        // Try multiple ways to make the window visible
        RequestAttention();
        
        // Add a simple method to verify the window is actually showing
        CallDeferred("verify_visibility");
        
        // Auto-hide after 5 seconds for better visibility
        GetTree().CreateTimer(5.0f).Timeout += () => Hide();
    }

    public new void Hide()
    {
        base.Hide();
        _currentMonitorId = -1;
    }
    
    private void verify_visibility()
    {
        GD.Print($"VERIFICATION: Window Visible = {Visible}, Size = {Size}, Position = {Position}");
        var testBg = GetNodeOrNull<ColorRect>("TestBackground");
        if (testBg != null)
        {
            GD.Print($"TestBackground found - Color: {testBg.Color}, Visible: {testBg.Visible}, Size: {testBg.Size}");
        }
        else
        {
            GD.Print("TestBackground NOT FOUND!");
        }
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