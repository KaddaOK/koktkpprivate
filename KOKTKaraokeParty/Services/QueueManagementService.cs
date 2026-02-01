using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Services;

[Meta(typeof(IAutoNode))]
public partial class QueueManagementService : Node
{
    public event Action<QueueItem> ItemAdded;
    public event Action<QueueItem> ItemRemoved;
    public event Action<QueueItem> NowPlayingChanged;
    public event Action<bool> PausedStateChanged;
    public event Action QueueLoaded;
    public event Action QueueReordered;

    private Queue<QueueItem> _queue;
    private QueueItem _nowPlaying;
    private bool _isPaused;
    private CancellationTokenSource _playingCancellationSource = new CancellationTokenSource();

    private IFileWrapper _fileWrapper;
    private IYtDlpProviderNode _ytDlpProvider;
    private string _savedQueueFileName;

    public QueueItem NowPlaying => _nowPlaying;
    public bool IsPaused => _isPaused;
    public int QueueCount => _queue.Count;

    public IEnumerable<QueueItem> GetQueueItems()
    {
        return _queue.ToList();
    }

    public void Initialize(IFileWrapper fileWrapper, IYtDlpProviderNode ytDlpProvider)
    {
        _fileWrapper = fileWrapper;
        _ytDlpProvider = ytDlpProvider;
        _queue = new Queue<QueueItem>();
        _savedQueueFileName = Path.Combine(Utils.GetAppStoragePath(), "queue.json");
        LoadQueueFromDiskIfExists();
    }

    public void OnReady()
    {
        this.Provide();
    }

    public void AddToQueue(QueueItem item)
    {
        _queue.Enqueue(item);
        ItemAdded?.Invoke(item);
        SaveQueueToDisk();

        // If it's a YouTube item, start downloading it in the background
        if (item.ItemType == ItemType.Youtube)
        {
            item.IsDownloading = true;
            _ = Task.Run(async () => await StartYoutubeDownload(item));
        }
    }

    public void RemoveFromQueue(QueueItem item)
    {
        var withoutItem = _queue.Where(q => q != item).ToList();
        _queue = new Queue<QueueItem>(withoutItem);
        ItemRemoved?.Invoke(item);
        SaveQueueToDisk();
    }

    /// <summary>
    /// Clears all items from the queue and deletes the saved queue file.
    /// </summary>
    public void ClearQueue()
    {
        _queue.Clear();
        _nowPlaying = null;
        Utils.DeleteSavedQueueFile();
        QueueLoaded?.Invoke(); // Trigger UI refresh
    }

    /// <summary>
    /// Removes the first item from the queue (useful for "Yes, except first" restore option).
    /// </summary>
    /// <returns>The removed item, or null if the queue was empty.</returns>
    public QueueItem RemoveFirstItem()
    {
        if (_queue.Count == 0)
        {
            return null;
        }
        
        var firstItem = _queue.Dequeue();
        ItemRemoved?.Invoke(firstItem);
        SaveQueueToDisk();
        return firstItem;
    }

    public void ReorderQueue(QueueItem draggedItem, QueueItem targetItem, int dropSection)
    {
        var withoutMovedItem = _queue.Where(q => q != draggedItem).ToList();
        var formerIndex = _queue.ToList().IndexOf(draggedItem);

        int targetIndex;
        // if it's the NowPlaying item, that means they're dragging it to be next
        if (_nowPlaying != null && _nowPlaying.PerformanceLink == draggedItem.PerformanceLink)
        {
            targetIndex = 0;
        }
        else
        {
            targetIndex = withoutMovedItem.IndexOf(targetItem) + (dropSection == 1 ? 1 : 0);
        }

        if (targetIndex >= withoutMovedItem.Count)
        {
            withoutMovedItem.Add(draggedItem);
        }
        else
        {
            withoutMovedItem.Insert(targetIndex, draggedItem);
        }

        _queue = new Queue<QueueItem>(withoutMovedItem);
        SaveQueueToDisk();
        QueueReordered?.Invoke();
    }

    public QueueItem GetNextInQueue()
    {
        if (_queue.Count == 0) return null;
        
        SaveQueueToDisk();
        var nextItem = _queue.Dequeue();
        _nowPlaying = nextItem;
        NowPlayingChanged?.Invoke(_nowPlaying);
        return nextItem;
    }

    public void FinishedPlaying(QueueItem item)
    {
        if (_nowPlaying == item)
        {
            // Clean up temporary download files if this was a downloaded YouTube video
            if (!string.IsNullOrEmpty(_nowPlaying.TemporaryDownloadPath) && File.Exists(_nowPlaying.TemporaryDownloadPath))
            {
                try
                {
                    File.Delete(_nowPlaying.TemporaryDownloadPath);
                    GD.Print($"Cleaned up temporary download file: {_nowPlaying.TemporaryDownloadPath}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to clean up temporary file {_nowPlaying.TemporaryDownloadPath}: {ex.Message}");
                }
            }

            _nowPlaying = null;
            NowPlayingChanged?.Invoke(null);
            SaveQueueToDisk();
        }
    }

    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            PausedStateChanged?.Invoke(true);
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            PausedStateChanged?.Invoke(false);
        }
    }

    public void Skip()
    {
        _playingCancellationSource.Cancel();
        _playingCancellationSource = new CancellationTokenSource();
    }

    public CancellationToken GetPlaybackCancellationToken()
    {
        return _playingCancellationSource.Token;
    }

    private async Task StartYoutubeDownload(QueueItem item)
    {
        try
        {
            item.IsDownloading = true;
            
            var tempDir = Path.Combine(Utils.GetAppStoragePath(), "temp_downloads");
            Directory.CreateDirectory(tempDir);

            var videoId = ExtractYoutubeVideoId(item.PerformanceLink);
            var safeFileName = $"{videoId}.%(ext)s";
            var outputTemplate = Path.Combine(tempDir, safeFileName);

            GD.Print($"Starting background download for YouTube video: {item.PerformanceLink}");

            var result = await _ytDlpProvider.DownloadFromUrl(item.PerformanceLink, outputTemplate);

            var downloadedFiles = Directory.GetFiles(tempDir, $"{videoId}.*");
            if (downloadedFiles.Length > 0)
            {
                item.TemporaryDownloadPath = downloadedFiles[0];
                GD.Print($"Successfully downloaded YouTube video to: {item.TemporaryDownloadPath}");
            }
            else
            {
                GD.PrintErr($"Could not find downloaded file for: {item.PerformanceLink}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to download YouTube video {item.PerformanceLink}: {ex.Message}");
        }
        finally
        {
            item.IsDownloading = false;
        }
    }

    private string ExtractYoutubeVideoId(string youtubeUrl)
    {
        try
        {
            var uri = new Uri(youtubeUrl);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var videoId = query["v"];
            
            if (!string.IsNullOrEmpty(videoId))
            {
                return SanitizeFileName(videoId);
            }
            
            return Math.Abs(youtubeUrl.GetHashCode()).ToString();
        }
        catch
        {
            return Math.Abs(youtubeUrl.GetHashCode()).ToString();
        }
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    private void SaveQueueToDisk()
    {
        try
        {
            var queueList = _queue.ToArray();
            var queueJson = JsonConvert.SerializeObject(queueList, Formatting.Indented);
            _fileWrapper.WriteAllText(_savedQueueFileName, queueJson);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to save queue to disk: {ex.Message}");
        }
    }

    private void LoadQueueFromDiskIfExists()
    {
        try
        {
            if (_fileWrapper.Exists(_savedQueueFileName))
            {
                GD.Print("Loading queue from disk...");
                var queueJson = _fileWrapper.ReadAllText(_savedQueueFileName);
                var queueList = JsonConvert.DeserializeObject<QueueItem[]>(queueJson);
                GD.Print($"Loaded {queueList?.Length} items from disk.");
                _queue = new Queue<QueueItem>(queueList);
                
                // Check YouTube items and restart downloads if needed
                foreach (var item in _queue)
                {
                    if (item.ItemType == ItemType.Youtube)
                    {
                        bool needsDownload = false;
                        
                        if (string.IsNullOrEmpty(item.TemporaryDownloadPath) || 
                            !File.Exists(item.TemporaryDownloadPath))
                        {
                            needsDownload = true;
                            GD.Print($"YouTube item missing download file, will restart: {item.PerformanceLink}");
                        }
                        else if (item.IsDownloading)
                        {
                            needsDownload = true;
                            GD.Print($"YouTube item was downloading when saved, will restart: {item.PerformanceLink}");
                        }
                        
                        if (needsDownload)
                        {
                            item.TemporaryDownloadPath = null;
                            item.IsDownloading = false;
                            _ = Task.Run(async () => await StartYoutubeDownload(item));
                        }
                    }
                }
                
                // Fire QueueLoaded event so UI can refresh
                QueueLoaded?.Invoke();
            }
            else
            {
                _queue = new Queue<QueueItem>();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to load queue from disk: {ex.Message}");
            _queue = new Queue<QueueItem>();
        }
    }
}