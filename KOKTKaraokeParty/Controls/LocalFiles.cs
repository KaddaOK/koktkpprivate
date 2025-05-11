using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public interface ILocalFiles {}
[Meta(typeof(IAutoNode))]
public partial class LocalFiles : MarginContainer, ILocalFiles
{
    public override void _Notification(int what) => this.Notify(what);

    private List<(LocalScanPathEntry dbEntry, ScanPathEntryPanel screenControl)> _scanPathEntries;

    private PackedScene _scanPathEntryPanelScene;

    #region Nodes
    [Node] private IButton AddNewPathButton { get; set; } = default!;
    [Node] private IVBoxContainer LocalFilePathsContainer { get; set; } = default!;
    [Node] private IPanelContainer PathsEmptyContainer { get; set; } = default!;
    [Node] private IEditScanPathDialog EditScanPathDialog { get; set; } = default!;
    [Node] private IScanningPathDialog ScanningPathDialog { get; set; } = default!;
    #endregion

    #region Initialized Dependencies

    private KOKTDbContext DbContext { get; set; }

    public void SetupForTesting(KOKTDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public void Initialize()
    {
        DbContext = new KOKTDbContext();
        DbContext.Database.EnsureCreated();
    }

    #endregion

    public void OnReady()
    {
        _scanPathEntryPanelScene = GD.Load<PackedScene>("res://Controls/ScanPathEntryPanel.tscn");
        RefreshFromSqlite();
        AddNewPathButton.Pressed += AddNewPathButtonPressed;
        EditScanPathDialog.ScanPathSaved += EditScanPathDialog_ScanPathSaved;
        ScanningPathDialog.LocalFileScanPathsStale += RefreshFromSqlite;
    }

    private void RefreshFromSqlite()
    {
        if (_scanPathEntries != null)
        {
            _scanPathEntries.ForEach(entry =>
            {
                entry.screenControl.QueueFree();
            });
        }
        _scanPathEntries = new List<(LocalScanPathEntry dbEntry, ScanPathEntryPanel screenControl)>();
        DbContext.LocalScanPaths
        .Select(s => new { dbEntry = s, FileCount = s.Files.Count()})
        .ToList().ForEach(entry =>
        {
            var scanPathEntryPanel = _scanPathEntryPanelScene.Instantiate<ScanPathEntryPanel>();
            scanPathEntryPanel.SetScanPathEntry(entry.dbEntry, entry.FileCount);
            scanPathEntryPanel.EditScanPathEntry += EditScanPathEntry;
            scanPathEntryPanel.Rescan += Rescan;
            LocalFilePathsContainer.AddChild(scanPathEntryPanel);
            _scanPathEntries.Add((entry.dbEntry, scanPathEntryPanel));
        });
        PathsEmptyContainer.Visible = _scanPathEntries?.Count == 0;
    }

    private void Rescan(int scanPathEntryId)
    {
        ScanningPathDialog.SetScanPathEntry(scanPathEntryId);
        ScanningPathDialog.Show();
    }

    private void EditScanPathDialog_ScanPathSaved(int scanPathEntryId)
    {
        RefreshFromSqlite();
        // TODO: prompt for scan?
    }

    public void AddNewPathButtonPressed()
    {
        EditScanPathDialog.SetScanPathEntry(new LocalScanPathEntry());
        EditScanPathDialog.Show();
    }

    public void EditScanPathEntry(int scanPathEntryId)
    {
        var scanPathEntry = _scanPathEntries.FirstOrDefault(x => x.dbEntry.Id == scanPathEntryId);
        if (scanPathEntry != default)
        {
            EditScanPathDialog.SetScanPathEntry(scanPathEntry.dbEntry);
            EditScanPathDialog.Show();
        }
    }
}
