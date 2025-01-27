using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;

public interface IScanPathEntryPanel
{
    public void SetButtonsEnabled(bool enabled);
    event ScanPathEntryPanel.EditScanPathEntryEventHandler EditScanPathEntry;
    event ScanPathEntryPanel.RescanEventHandler Rescan;
}

[Meta(typeof(IAutoNode))]
public partial class ScanPathEntryPanel : PanelContainer, IScanPathEntryPanel
{
    public override void _Notification(int what) => this.Notify(what);

    private LocalScanPathEntry _scanPathEntry;
    private int? _filesFoundCount;

    #region Nodes
    [Node] private ILabel PathLabel { get; set; } = default!;
    [Node] private ILabel FormatSpecifierLabel { get; set; } = default!;
    [Node] private ILabel LastScannedLabel { get; set; } = default!;
    [Node] private ILabel FilesFoundCountLabel { get; set; } = default!;
    [Node] private IButton EditButton { get; set; } = default!;
    [Node] private IButton RescanButton { get; set; } = default!;
    #endregion

    #region Signals

    [Signal]
    public delegate void EditScanPathEntryEventHandler(int scanPathEntryId);

    [Signal]
    public delegate void RescanEventHandler(int scanPathEntryId);

    #endregion

    public void OnReady()
    {
        EditButton.Pressed += () => EmitSignal(SignalName.EditScanPathEntry, _scanPathEntry.Id);
        RescanButton.Pressed += () => EmitSignal(SignalName.Rescan, _scanPathEntry.Id);
        UpdatePathEntryDisplay();
    }

    public void SetScanPathEntry(LocalScanPathEntry scanPathEntry, int filesFoundCount)
    {
        _scanPathEntry = scanPathEntry;
        _filesFoundCount = filesFoundCount;
        UpdatePathEntryDisplay();
    }

    private void UpdatePathEntryDisplay()
    {
        if (PathLabel != null)
        {
            PathLabel.Text = _scanPathEntry?.Path;
            FormatSpecifierLabel.Text = _scanPathEntry?.FormatSpecifier;
            LastScannedLabel.Text = _scanPathEntry?.LastFullScanCompleted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";
            FilesFoundCountLabel.Text = _filesFoundCount?.ToString() ?? "0";
        }
    }


    public void SetButtonsEnabled(bool enabled)
    {
        EditButton.Disabled = !enabled;
        RescanButton.Disabled = !enabled;
    }
}
