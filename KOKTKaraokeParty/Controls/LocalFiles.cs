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
                entry.screenControl.Free();
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
            scanPathEntryPanel.RescanEntry += RescanEntry;
            scanPathEntryPanel.RemoveEntry += RemoveEntry;
            LocalFilePathsContainer.AddChild(scanPathEntryPanel);
            _scanPathEntries.Add((entry.dbEntry, scanPathEntryPanel));
        });
        PathsEmptyContainer.Visible = _scanPathEntries?.Count == 0;
    }

    private void RescanEntry(int scanPathEntryId)
    {
        ScanningPathDialog.SetScanPathEntry(scanPathEntryId);
        ScanningPathDialog.Show();
    }

    private void RemoveEntry(int scanPathEntryId)
    {
        var scanPathEntry = _scanPathEntries.FirstOrDefault(x => x.dbEntry.Id == scanPathEntryId);
        if (scanPathEntry == default)
        {
            GD.Print($"Scan path entry {scanPathEntryId} not found for deletion.");
            return;
        }
        var countForEntry = DbContext.LocalSongFiles.Count(file => file.ParentPathId == scanPathEntryId);
        var dialog = new ConfirmationDialog();
        dialog.DialogText = $"Are you sure you want to remove this scan path entry and its {countForEntry} files from the locally scanned catalog?";
        dialog.OkButtonText = "Remove";
        dialog.CancelButtonText = "Cancel";
        dialog.MinSize = new Vector2I(400, 100);
        AddChild(dialog);

        dialog.Confirmed += () => {
            var filesForEntry = DbContext.LocalSongFiles.Where(file => file.ParentPathId == scanPathEntryId).ToList();
            if (filesForEntry.Count > 0)
            {
                GD.Print($"Deleting {filesForEntry.Count} files for scan path entry {scanPathEntryId}");
            }
            else
            {
                GD.Print($"No files to delete for scan path entry {scanPathEntryId}");
            }
            DbContext.LocalSongFiles.RemoveRange(filesForEntry);
            GD.Print($"Deleting scan path entry {scanPathEntryId}");
            DbContext.LocalScanPaths.Remove(scanPathEntry.dbEntry);
            DbContext.SaveChanges();
            RefreshFromSqlite();
            dialog.QueueFree();
        };

        dialog.Canceled += () => {
            GD.Print($"Deletion canceled for scan path entry {scanPathEntryId}");
            dialog.QueueFree();
        };

        dialog.PopupCentered();
    }

    private void EditScanPathDialog_ScanPathSaved(int scanPathEntryId)
    {
        RefreshFromSqlite();

        var dialog = new ConfirmationDialog();
        dialog.DialogText = $"Would you like to scan the new path now?";
        dialog.OkButtonText = "Scan";
        dialog.CancelButtonText = "Cancel";
        dialog.MinSize = new Vector2I(400, 100);
        AddChild(dialog);

        dialog.Confirmed += () => {
            GD.Print($"Immediate scan accepted for new path entry {scanPathEntryId}");
            RescanEntry(scanPathEntryId);
            dialog.QueueFree();
        };

        dialog.Canceled += () => {
            GD.Print($"Immediate scan declined for new path entry {scanPathEntryId}");
            dialog.QueueFree();
        };

        dialog.PopupCentered();

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
