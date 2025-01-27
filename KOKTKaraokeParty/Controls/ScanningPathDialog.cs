using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public interface IScanningPathDialog : IAcceptDialog
{
    void SetScanPathEntry(int scanPathEntryId);
}

[Meta(typeof(IAutoNode))]
public partial class ScanningPathDialog : AcceptDialog, IScanningPathDialog
{
    public override void _Notification(int what) => this.Notify(what);

    private LocalScanPathEntry _scanPathEntry;

    private CancellationTokenSource _cancellationTokenSource;

    private bool _isScanning;
    private int _mp4sFound;
    private int _mp3GsFound;
    private int _zipsFound;
    private int _zipsVerified;
    private int _filesFound;
    private int _filesProcessed;
    private Queue<string> _filesToProcess;
    private List<string> _orphanedCDGFiles;

    #region Nodes
    [Node] private ILabel PathLabel { get; set; } = default!;
    [Node] private IButton StartScanButton { get; set; } = default!;
    [Node] private IButton StopScanButton { get; set; } = default!;
    [Node] private ILabel PathsFoundLabel { get; set; } = default!;
    [Node] private ILabel CurrentPathLabel { get; set; } = default!;
    [Node] private ILabel FilesFoundLabel { get; set; } = default!;
    [Node] private ILabel ScanStatusLabel { get; set; } = default!;
    [Node] private ILabel MP4sFoundLabel { get; set; } = default!;
    [Node] private ILabel MP3GsFoundLabel { get; set; } = default!;
    [Node] private ILabel OrphansFoundLabel { get; set; } = default!;
    [Node] private ILabel ZIPsFoundLabel { get; set; } = default!;
    [Node] private ILabel QueueProgressLabel { get; set; } = default!;
    
    #endregion

    #region Initialized Dependencies

    private KOKTDbContext DbContext { get; set; }
    private ILocalFileScanner LocalFileScanner { get; set; }
    private ILocalFileValidator LocalFileValidator { get; set; }
    private ILocalFileNameMetadataParser MetadataParser { get; set; }

    public void SetupForTesting(KOKTDbContext dbContext, ILocalFileScanner localFileScanner, ILocalFileValidator localFileValidator, ILocalFileNameMetadataParser metadataParser)
    {
        DbContext = dbContext;
        LocalFileScanner = localFileScanner;
        LocalFileValidator = localFileValidator;
        MetadataParser = metadataParser;
    }

    public void Initialize()
    {
        DbContext = new KOKTDbContext();
        DbContext.Database.EnsureCreated();
        LocalFileScanner = new LocalFileScanner();
        LocalFileValidator = new LocalFileValidator();
        MetadataParser = new LocalFileNameMetadataParser();
    }

    #endregion
    
    public void OnReady()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        StartScanButton.Pressed += StartScanButtonPressed;
        StopScanButton.Pressed += StopScanButtonPressed;
        LocalFileScanner.UpdatePathsFoundCount += UpdatePathsFoundCount;
        LocalFileScanner.UpdateFilesFoundCount += UpdateFilesFoundCount;
        LocalFileScanner.UpdateOrphanedCDGFilesFound += UpdateOrphanedCDGFilesFound;
        Confirmed += CloseMaybe;
        CloseRequested += CloseMaybe;
    }

    private void CloseMaybe()
    {
        if (!_isScanning)
        {
            Hide();
        }
    }


    private void UpdateOrphanedCDGFilesFound(string[] orphanedCDGFiles)
    {
        _orphanedCDGFiles = orphanedCDGFiles.ToList();
        OrphansFoundLabel.Text = _orphanedCDGFiles.Count.ToString();
        OrphansFoundLabel.TooltipText = string.Join("\n", _orphanedCDGFiles);
    }


    private void StopScanButtonPressed()
    {
        if (!_isScanning)
        {
            return;
        }
        _cancellationTokenSource.Cancel();
        SetIsScanning(false);
    }

    private void SetIsScanning(bool isScanning, bool isStillProcessing = false)
    {
        _isScanning = isScanning;
        StartScanButton.Disabled = isScanning || isStillProcessing;
        StopScanButton.Disabled = !isScanning && !isStillProcessing;
        if (isScanning)
        {
            ScanStatusLabel.Text = "Scanning...";
        }
        else if (isStillProcessing)
        {
            ScanStatusLabel.Text = "Processing...";
        }
        else
        {
            ScanStatusLabel.Text = "";
        }
    }

    private void ResetTotals()
    {
        _mp4sFound = 0;
        MP4sFoundLabel.Text = "0";
        _mp3GsFound = 0;
        MP3GsFoundLabel.Text = "0";
        _zipsFound = 0;
        ZIPsFoundLabel.Text = "0";
        _zipsVerified = 0;
        _orphanedCDGFiles = new List<string>();
        UpdateFilesFoundCount(0);
        UpdatePathsFoundCount(0, "");
        UpdateOrphanedCDGFilesFound(Array.Empty<string>());
        _filesToProcess = new Queue<string>();
    }

    private async void StartScanButtonPressed()
    {
        if (_isScanning)
        {
            return;
        }
        ResetTotals();
        SetIsScanning(true);
        _cancellationTokenSource = new CancellationTokenSource();

        _scanPathEntry.LastFullScanStarted = DateTime.Now;
        await DbContext.SaveChangesAsync();

        var processingTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));

        await foreach (var result in LocalFileScanner.FindAllFilesAsync(_scanPathEntry.Path, _cancellationTokenSource.Token))
        {
            switch (Path.GetExtension(result).ToLowerInvariant())
            {
                case ".mp4":
                    _mp4sFound++;
                    MP4sFoundLabel.Text = _mp4sFound.ToString();
                    break;
                case ".cdg":
                    _mp3GsFound++;
                    MP3GsFoundLabel.Text = _mp3GsFound.ToString();
                    break;
                case ".zip":
                    _zipsFound++;
                    ZIPsFoundLabel.Text = _zipsFound.ToString();
                    break;
            }
            _filesToProcess.Enqueue(result);
            // TODO: how do I process the queue one at a time even if it runs behind?
        }

        if (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            ScanStatusLabel.Text = "Scan cancelled";
        }
        else
        {
            SetIsScanning(false, true);
            await processingTask;
            _scanPathEntry.LastFullScanCompleted = DateTime.Now;
            await DbContext.SaveChangesAsync();
            SetIsScanning(false);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested 
            && (_filesToProcess.Count > 0 || _isScanning))
        {
            if (_filesToProcess.TryDequeue(out var file))
            {
                await ProcessFile(file, cancellationToken);
                _filesProcessed++;
                var progressText = $"{_filesProcessed}/{_filesProcessed + _filesToProcess.Count}";
                QueueProgressLabel.CallDeferred("set_text", [progressText]);
            }
            else
            {
                await Task.Delay(1); // Adjust the delay as needed
            }
        }
    }

    private async Task ProcessFile(string path, CancellationToken cancellationToken)
    {
        try
        {
            //var validationResult = LocalFileValidator.IsValid(path);
            //if (!validationResult.isValid)
            //{
                // TODO: add to dead ones 
            //}
            
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var metadataResult = MetadataParser.Parse(path, _scanPathEntry.FormatSpecifier);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var dbEntry = await DbContext.LocalSongFiles.SingleOrDefaultAsync(f => f.FullPath == path);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (dbEntry == null)
            {
                dbEntry = new LocalSongFileEntry
                {
                    FullPath = path,
                    FileNameWithoutExtension = Path.GetFileNameWithoutExtension(path),

                    ArtistName = metadataResult.ArtistName,
                    SongName = metadataResult.SongTitle,
                    CreatorName = metadataResult.CreatorName,
                    Identifier = metadataResult.Identifier,

                    ParentPath = _scanPathEntry,
                    ParentPathId = _scanPathEntry.Id
                };

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await DbContext.LocalSongFiles.AddAsync(dbEntry);
            }
            else
            {
                // TODO: update?
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (_filesProcessed % 100 == 0) // TODO: assess if this is sane or not
            {
                await DbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error processing {path}: {ex.Message}");
        }
    }


    private void UpdateFilesFoundCount(int totalFilesFound)
    {
        FilesFoundLabel.Text = totalFilesFound.ToString();
    }


    private void UpdatePathsFoundCount(int totalPathsFound, string currentPath)
    {
        string relativePath = currentPath.StartsWith(_scanPathEntry.Path) 
            ? currentPath.Substring(_scanPathEntry.Path.Length)
            : currentPath;

        PathsFoundLabel.Text = totalPathsFound.ToString();
        CurrentPathLabel.Text = relativePath;
    }


    public void SetScanPathEntry(int scanPathEntryId)
    {
        _scanPathEntry = DbContext.LocalScanPaths.SingleOrDefault(p => p.Id == scanPathEntryId);
        if (_scanPathEntry == null)
        {
            // TODO: freak out
        }
        PathLabel.Text = _scanPathEntry.Path;
    }

}
