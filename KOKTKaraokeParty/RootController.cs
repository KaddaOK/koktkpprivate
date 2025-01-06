using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;
using NAudio.Flac;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KOKTKaraokeParty;

[Meta(typeof(IAutoNode))]
public partial class RootController : Node, 
IProvide<IPuppeteerPlayer>, IProvide<Settings>
{
    #region Local State

    private bool IsPaused { get; set; }
    private bool IsWaitingToReturnFromBrowserControl { get; set; }
    private bool IsPlayingLocalFile => NowPlaying?.ItemType == ItemType.LocalMp3G || NowPlaying?.ItemType == ItemType.LocalMp3GZip;
    private Queue<QueueItem> Queue { get; set; }
    private QueueItem NowPlaying { get; set; }
    private string BackgroundMusicNowPlayingFilePath { get; set;}

    #endregion

    #region Initialized Dependencies

    private IPuppeteerPlayer PuppeteerPlayer { get; set; }
    IPuppeteerPlayer IProvide<IPuppeteerPlayer>.Value() => PuppeteerPlayer;

    private Settings Settings { get; set; }
    Settings IProvide<Settings>.Value() => Settings;

    public void SetupForTesting(IPuppeteerPlayer puppeteerPlayer, Settings settings)
    {
        PuppeteerPlayer = puppeteerPlayer;
        Settings = settings;
    }

    public void Initialize()
    {
        PuppeteerPlayer = new PuppeteerPlayer();
        Settings = Settings.LoadFromDiskIfExists();
    }

    #endregion

    #region Nodes

    [Node("%Setup")] // TODO: this name is annoying to me
    private ISetupTab SetupTab { get; set; } = default!;

    [Node("%Search")] // TODO: this name is annoying to me
    private ISearchTab SearchTab { get; set; } = default!;

    [Node] private IDisplayScreen DisplayScreen { get; set; } = default!;
    [Node] private AudioStreamPlayer BackgroundMusicPlayer { get; set; } = default!;

    #endregion

	public void OnReady()
    {
        SetupHistoryLogFile();
        SetupQueueTree();
        SetupMainQueueControls();
        LoadQueueFromDiskIfExists();
        BindDisplayScreenControls();
        SetupBackgroundMusicQueue();

        BindSearchScreenControls();

        SetupStartTab();
        GetTree().AutoAcceptQuit = false;

        DisplayScreen.LocalPlaybackFinished += (wasPlaying) =>
        {
            GD.Print($"Local playback finished: {wasPlaying}");
            if (wasPlaying == NowPlaying?.PerformanceLink)
            {
                RemoveQueueTreeRow(NowPlaying);
                NowPlaying = null;
            }
        };

        this.Provide();
        SetProcess(true);
    }

    private void BindSearchScreenControls()
    {
        SearchTab.ItemAddedToQueue += SearchTabItemAddedToQueue;
    }

    private void SearchTabItemAddedToQueue(QueueItem item)
    {
        Queue.Enqueue(item);
        AddQueueTreeRow(item);
        SaveQueueToDisk();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            Quit();
        }
        else
        {
            this.Notify(what);
        }
    }

    public async void Quit()
    {
        var cleanupTasks = new List<Task>
        {
            PuppeteerPlayer.CloseAutomatedBrowser()
        };
        if (BackgroundMusicPlayer.Playing)
        {
            BackgroundMusicPlayer.Stop();
            cleanupTasks.Add(Task.Delay(100));
        }
        await Task.WhenAll(cleanupTasks);
        GetTree().Quit();
    }

    public async void PlayItem(QueueItem item, CancellationToken cancellationToken)
    {
        NowPlaying = item;
        if (Settings.BgMusicEnabled)
        {
            FadeInBackgroundMusic();
        }

        DisplayScreen.ShowNextUp(item.SingerName, item.SongName, item.ArtistName, Settings.CountdownLengthSeconds);

        // Countdown logic
        int launchSecondsRemaining = Settings.CountdownLengthSeconds;
        while (launchSecondsRemaining > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                GD.Print("PlayItem cancelled.");
                return;
            }
            if (!IsPaused)
            {
                DisplayScreen.UpdateLaunchCountdownSecondsRemaining(launchSecondsRemaining);
                launchSecondsRemaining--;
            }
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        }
        if (Settings.BgMusicEnabled)
        {
            FadeOutBackgroundMusic();
        }

        GD.Print($"Playing {item.SongName} by {item.ArtistName} ({item.CreatorName}) from {item.PerformanceLink}");
        switch (item.ItemType)
        {
            case ItemType.KarafunWeb:
                IsWaitingToReturnFromBrowserControl = true;
                DisplayScreen.HideDisplayScreen();
                await PuppeteerPlayer.PlayKarafunUrl(item.PerformanceLink, cancellationToken);
                IsWaitingToReturnFromBrowserControl = false;
                GD.Print("Karafun playback finished.");
                RemoveQueueTreeRow(NowPlaying);
                NowPlaying = null;
                break;
            case ItemType.Youtube:
                IsWaitingToReturnFromBrowserControl = true;
                DisplayScreen.HideDisplayScreen();
                await PuppeteerPlayer.PlayYoutubeUrl(item.PerformanceLink, cancellationToken);
                IsWaitingToReturnFromBrowserControl = false;
                GD.Print("Youtube playback finished.");
                RemoveQueueTreeRow(NowPlaying);
                NowPlaying = null;
                break;
            case ItemType.LocalMp3G:
            case ItemType.LocalMp3GZip:
                DisplayScreen.PlayLocal(item);
                break;
            default:
                GD.PrintErr($"Unknown item type: {item.ItemType}");
                break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            GD.Print("PlayItem cancelled.");
            return;
        }
    }

    #region display screen stuff

    public void BindDisplayScreenControls()
    {
        DisplayScreen.SetMonitorId(Settings.DisplayScreenMonitor);
    }

    public void ShowEmptyQueueScreenAndBgMusic()
    {
        DisplayScreen.ShowEmptyQueueScreen();
        if (Settings.BgMusicEnabled)
        {
            StartOrResumeBackgroundMusic();
        }
    }

    #endregion

    #region background audio queue stuff

    public void SetupBackgroundMusicQueue()
    {
        BackgroundMusicPlayer.Finished += BackgroundMusicPlayerFinished;
        ToggleBackgroundMusic(Settings.BgMusicEnabled);
    }

    private void ToggleBackgroundMusic(bool enable)
    {
        if (Settings.BgMusicEnabled != enable)
        {
            Settings.BgMusicEnabled = enable;
            Settings.SaveToDisk();
        }
        if (Settings.BgMusicEnabled && !IsWaitingToReturnFromBrowserControl && !IsPlayingLocalFile)
        {
            StartOrResumeBackgroundMusic();
        }
        else
        {
            PauseBackgroundMusic();
        }
    }

    private async void FadeInBackgroundMusic()
    {
        if (Settings.BgMusicVolumePercent == 0)
        {
            GD.PrintErr("BG Music volume is 0, not fading in.");
        }
        else
        {
            BackgroundMusicPlayer.VolumeDb = PercentToDb(0);
            var finalVolumeInDb = PercentToDb(Settings.BgMusicVolumePercent);
            GD.Print($"Fading in background music to {Settings.BgMusicVolumePercent}% ({finalVolumeInDb} dB)");
            var tween = GetTree().CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(BackgroundMusicPlayer, "volume_db", finalVolumeInDb, 2).From(PercentToDb(0.01));
        }
        StartOrResumeBackgroundMusic();
    }

    private async void FadeOutBackgroundMusic()
    {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(BackgroundMusicPlayer, "volume_db", PercentToDb(0.01), 5);
        tween.Finished += () => PauseBackgroundMusic();
    }

    private void StartOrResumeBackgroundMusic()
    {
        GD.Print($"BG Music: {Settings.BgMusicFiles.Count} items in queue. Stream: {(BackgroundMusicPlayer.Stream == null ? "null" : "present")}, Playing: {BackgroundMusicPlayer.Playing}, Paused: {BackgroundMusicPlayer.StreamPaused}");
        if (Settings.BgMusicFiles.Count > 0)
        {
            DisplayScreen.UpdateBgMusicPaused(false);

            // if it's already playing, don't do anything
            if (BackgroundMusicPlayer.Playing)
            {
                return;
            }

            if (BackgroundMusicPlayer.Stream == null)
            {
                StartPlayingBackgroundMusic(0);
            }

            if (BackgroundMusicPlayer.StreamPaused)
            {
                BackgroundMusicPlayer.StreamPaused = false;
            }
        }
    }

    private void PauseBackgroundMusic()
    {
        if (BackgroundMusicPlayer.Playing)
        {
            BackgroundMusicPlayer.StreamPaused = true;
            DisplayScreen.UpdateBgMusicPaused(true);
        }
    }

    private float PercentToDb(double percent)
    {
        return (float)Mathf.LinearToDb(percent / 100D);
    }
    private void SetBgMusicVolumePercent(double volumePercent)
    {
        Settings.BgMusicVolumePercent = volumePercent;
        Settings.SaveToDisk();
        BackgroundMusicPlayer.VolumeDb = PercentToDb(volumePercent);
        GD.Print($"BG Music volume set to {volumePercent}% ({BackgroundMusicPlayer.VolumeDb} dB)");
    }
    private void BackgroundMusicPlayerFinished()
    {
        if (Settings.BgMusicFiles.Count > 0)
        {
            var oldNowPlaying = BackgroundMusicNowPlayingFilePath;
            var previousPlaylistIndex = Settings.BgMusicFiles.IndexOf(oldNowPlaying);
            var nextIndexToPlay = previousPlaylistIndex + 1;
            if (nextIndexToPlay >= Settings.BgMusicFiles.Count)
            {
                nextIndexToPlay = 0;
            }
            StartPlayingBackgroundMusic(nextIndexToPlay);
        }
        else
        {
            DisplayScreen.UpdateBgMusicNowPlaying("None");
        }
    }

    private void StartPlayingBackgroundMusic(int indexToPlay)
    {
        BackgroundMusicNowPlayingFilePath = Settings.BgMusicFiles[indexToPlay];
        BackgroundMusicPlayer.Stream = LoadAudioFromPath(BackgroundMusicNowPlayingFilePath);
        BackgroundMusicPlayer.Play();
        DisplayScreen.UpdateBgMusicNowPlaying(Path.GetFileNameWithoutExtension(BackgroundMusicNowPlayingFilePath));
        DisplayScreen.UpdateBgMusicPaused(false);
    }

    public AudioStream LoadAudioFromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        switch (extension)
        {
            case ".ogg":
                return AudioStreamOggVorbis.LoadFromFile(path);
            case ".mp3":
                return LoadMP3(path);
            case ".flac":
                return LoadFLAC(path);
            case ".wav":
                return LoadWAV(path);
            default:
                throw new Exception("Unsupported file format: " + extension);
        }
    }
    public AudioStreamMP3 LoadMP3(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamMP3();
        sound.Data = file.GetBuffer((long)file.GetLength());
        return sound;
    }

    private AudioStreamWav LoadFLAC(string path)
    {
        using (var flacReader = new FlacReader(path))
        using (var memoryStream = new MemoryStream())
        {
            WaveFileWriter.WriteWavFileToStream(memoryStream, flacReader);
            return new AudioStreamWav
            {
                Data = memoryStream.ToArray(),
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                Stereo = true,
                MixRate = 44100
            };
        }
    }

    private AudioStreamWav LoadWAV(string path)
    {
        using (var wavReader = new WaveFileReader(path))
        using (var memoryStream = new MemoryStream())
        {
            // TODO: reject higher bit depths
            WaveFileWriter.WriteWavFileToStream(memoryStream, wavReader);
            return new AudioStreamWav
            {
                Data = memoryStream.ToArray(),
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                Stereo = wavReader.WaveFormat.Channels == 2,
                MixRate = wavReader.WaveFormat.SampleRate
            };
        }
    }

    #endregion

    #region main queue stuff

    private DraggableTree QueueTree;
    private TreeItem _queueRoot;
    private Button MainQueuePlayPauseButton;
    private Button MainQueueSkipButton;

    private CancellationTokenSource PlayingCancellationSource = new CancellationTokenSource();
    private void SetupQueueTree()
    {
        QueueTree = GetNode<DraggableTree>($"%{nameof(QueueTree)}");
        QueueTree.Columns = 4;
        QueueTree.SetColumnTitle(0, "Singer");
        QueueTree.SetColumnTitle(1, "Song");
        QueueTree.SetColumnTitle(2, "Artist");
        QueueTree.SetColumnTitle(3, "Creator");
        QueueTree.SetColumnTitlesVisible(true);
        QueueTree.HideRoot = true;

        // Create the root of the tree
        _queueRoot = QueueTree.CreateItem();

        QueueTree.GuiInput += QueueTreeGuiInput;
        QueueTree.Reorder += QueueTreeReorder;
    }

    private void QueueTreeReorder(string draggedItemMetadata, string targetItemMetadata, int dropSection)
    {
        // find the dragged item in the queue
        var draggedItem = Queue.FirstOrDefault(i => i.PerformanceLink == draggedItemMetadata);
        if (draggedItem == null)
        {
            GD.PrintErr($"Could not find dragged item in the queue");
            return;
        }

        var withoutMovedItem = Queue.Where(q => q != draggedItem).ToList();
        var formerIndex = Queue.ToList().IndexOf(draggedItem);

        int targetIndex = -1;
        // if it's the NowPlaying item, that means they're dragging it to be next
        if (NowPlaying != null && NowPlaying.PerformanceLink == draggedItemMetadata)
        {
            targetIndex = 0;
        }
        else
        {
            // find the destination item in the queue
            var targetItem = Queue.FirstOrDefault(i => i.PerformanceLink == targetItemMetadata);
            if (targetItem == null)
            {
                GD.PrintErr($"Could not find target item in the queue");
                return;
            }
            targetIndex = withoutMovedItem.IndexOf(targetItem) + (dropSection == 1 ? 1 : 0);
            GD.Print($"Item was dropped {(dropSection == 1 ? "after" : "before")} {targetItem.SingerName}");
        }
        if (targetIndex >= withoutMovedItem.Count)
        {
            withoutMovedItem.Add(draggedItem);
            GD.Print($"Moved queue item from index {formerIndex} to the bottom");
        }
        else
        {
            withoutMovedItem.Insert(targetIndex, draggedItem);
            GD.Print($"Moved queue item from index {formerIndex} to {targetIndex}");
        }
        Queue = new Queue<QueueItem>(withoutMovedItem);
        SaveQueueToDisk();

        // rebuild the tree
        QueueTree.Clear();
        _queueRoot = QueueTree.CreateItem();
        AddQueueTreeRow(NowPlaying);
        foreach (var item in Queue)
        {
            AddQueueTreeRow(item);
        }
    }

    private void SetupMainQueueControls()
    {
        MainQueuePlayPauseButton = GetNode<Button>($"%{nameof(MainQueuePlayPauseButton)}");
        MainQueuePlayPauseButton.Pressed += MainQueuePlayPauseButtonPressed;

        MainQueueSkipButton = GetNode<Button>($"%{nameof(MainQueueSkipButton)}");
        MainQueueSkipButton.Pressed += MainQueueSkipButtonPressed;
    }

    public void MainQueuePlayPauseButtonPressed()
    {
        GD.Print($"MainQueuePlayPauseButton pressed, IsPaused: {IsPaused}");
        if (IsPaused)
        {
            ResumeQueue();
            DisplayScreen.ToggleCountdownPaused(false);
        }
        else
        {
            PauseQueue();
            DisplayScreen.ToggleCountdownPaused(true);
        }
    }

    public void MainQueueSkipButtonPressed()
    {
        GD.Print($"MainQueueSkipButton pressed, IsPaused: {IsPaused}");
        if (!IsPaused)
        {
            PlayingCancellationSource.Cancel();
            RemoveQueueTreeRow(NowPlaying);
            NowPlaying = null; // this will cause _Process to dequeue the next item
        }
        else
        {
            // TODO: it's much more of a pain to make this work while we're paused tbh so for now it does nothing
            GD.Print("Sorry, I didn't implement skipping while paused yet.");
        }
    }

    public void QueueTreeGuiInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Pressed && keyEvent.Keycode == Key.Delete)
            {
                var selectedItem = QueueTree.GetSelected();
                if (selectedItem != null)
                {
                    // TODO: confirm deletion
                    var performanceLink = selectedItem.GetMetadata(0).ToString();
                    var singer = selectedItem.GetText(0);
                    GD.Print($"Queue tree input: performanceLink {performanceLink}");
                    // remove from queue tree
                    _queueRoot.RemoveChild(selectedItem);
                    // remove from actual queue
                    var itemInQueueTree = Queue.FirstOrDefault(i => i.PerformanceLink == performanceLink && i.SingerName == singer);
                    if (itemInQueueTree != null)
                    {
                        Queue = new Queue<QueueItem>(Queue.Except(new[] { itemInQueueTree }));
                        SaveQueueToDisk();
                    }
                    else
                    {
                        GD.PrintErr($"Could not find item to remove from actual queue: {singer} - {performanceLink}");
                    }
                }
            }
        }
    }

    private async void PauseQueue()
    {
        if (!IsPaused)
        {
            IsPaused = true;
            GD.Print("Paused queue.");
            MainQueuePlayPauseButton.Text = "▶️";
            if (NowPlaying != null)
            {
                switch (NowPlaying.ItemType)
                {
                    case ItemType.KarafunWeb:
                        await PuppeteerPlayer.PauseKarafun();
                        break;
                    case ItemType.Youtube:
                        await PuppeteerPlayer.ToggleYoutube();
                        break;
                    default:
                        GD.PrintErr($"Unknown item type: {NowPlaying.ItemType}");
                        break;
                }
            }
        }
    }

    private async void ResumeQueue()
    {
        if (IsPaused)
        {
            IsPaused = false;
            GD.Print("Resumed queue.");
            MainQueuePlayPauseButton.Text = "⏸️";
            if (NowPlaying != null)
            {
                switch (NowPlaying.ItemType)
                {
                    case ItemType.KarafunWeb:
                        await PuppeteerPlayer.ResumeKarafun();
                        break;
                    case ItemType.Youtube:
                        await PuppeteerPlayer.ToggleYoutube();
                        break;
                    default:
                        GD.PrintErr($"Unknown item type: {NowPlaying.ItemType}");
                        break;
                }
            }
        }
    }

    private void AddQueueTreeRow(QueueItem item)
    {
        if (item == null)
        {
            return;
        };

        if (_queueRoot == null)
        {
            GD.Print("Queue root item is disposed, recreating it.");
            _queueRoot = QueueTree.CreateItem();
        }
        var treeItem = QueueTree.CreateItem(_queueRoot);
        treeItem.SetText(0, item.SingerName);
        treeItem.SetText(1, item.SongName);
        treeItem.SetText(2, item.ArtistName);
        treeItem.SetText(3, item.CreatorName);
        treeItem.SetMetadata(0, item.PerformanceLink);
    }

    private void RemoveQueueTreeRow(QueueItem item)
    {
        if (_queueRoot == null)
        {
            GD.PrintErr("Queue root item is disposed while trying to remove item from it.");
            return;
        }
        var items = _queueRoot.GetChildren();
        var treeitem = items.FirstOrDefault(i => i.GetMetadata(0).ToString() == item.PerformanceLink && i.GetText(0) == item.SingerName);
        if (treeitem != null)
        {
            _queueRoot.RemoveChild(treeitem);
        }
        else
        {
            GD.PrintErr($"Could not find item to remove from display queue: {item.SingerName} - {item.PerformanceLink}");
        }
    }

    private string savedQueueFileName = Path.Combine(Utils.GetAppStoragePath(), "queue.json");

    private void SaveQueueToDisk()
    {
        try
        {
            // Serialize the Queue object to JSON
            var queueList = Queue.ToArray();
            GD.Print($"Getting JSON for queue ({queueList.Length} items)...");
            var queueJson = JsonConvert.SerializeObject(queueList, Formatting.Indented);
            //GD.Print($"Queue JSON: {queueJson}");
            // Write the JSON to the file
            File.WriteAllText(savedQueueFileName, queueJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save queue to disk: {ex.Message}");
        }
    }

    private void LoadQueueFromDiskIfExists()
    {
        try
        {
            // Check if the queue file exists
            if (File.Exists(savedQueueFileName))
            {
                GD.Print("Loading queue from disk...");
                // Read the JSON content from the file
                var queueJson = File.ReadAllText(savedQueueFileName);

                // Deserialize the JSON back into the Queue object
                var queueList = JsonConvert.DeserializeObject<QueueItem[]>(queueJson);
                GD.Print($"Loaded {queueList?.Length} items from disk.");
                Queue = new Queue<QueueItem>(queueList);
            }
            else
            {
                // If the file doesn't exist, initialize an empty queue
                Queue = new Queue<QueueItem>();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to load queue from disk: {ex.Message}");
            // Initialize an empty queue in case of failure
            Queue = new Queue<QueueItem>();
        }
        foreach (var item in Queue)
        {
            AddQueueTreeRow(item);
        }
    }

	public void OnProcess(double delta)
    {
        if (!IsPaused && NowPlaying == null)
        {
            if (Queue.Count > 0)
            {
                GD.Print($"Queue has {Queue.Count} items, playing next.");
                // Save the queue to disk right before we pop it (in case playing fails)
                SaveQueueToDisk();
                PlayingCancellationSource.Cancel();
                PlayingCancellationSource = new CancellationTokenSource();
                PlayItem(Queue.Dequeue(), PlayingCancellationSource.Token);
                AppendToPlayHistory(NowPlaying);
            }
            else if (!DisplayScreen.Visible && !DisplayScreen.IsDismissed)
            {
                GD.Print("Queue is empty, showing empty queue screen.");
                ShowEmptyQueueScreenAndBgMusic();
                // Save the queue to disk because it's now empty
                SaveQueueToDisk();
            }
        }
    }

    #endregion

    #region Logging stuff
    private string sessionPlayHistoryFileName;

    private void SetupHistoryLogFile()
    {
        var appStoragePath = Utils.GetAppStoragePath();
        Directory.CreateDirectory(appStoragePath);
        sessionPlayHistoryFileName = Path.Combine(appStoragePath, $"history_{DateTime.Now:yyyy-MM-dd_HHmm}.log");
        using (File.Create(sessionPlayHistoryFileName)) { }
    }

    private void AppendToPlayHistory(QueueItem queueItem)
    {
        var nowPlaying = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{queueItem.SingerName}|{queueItem.SongName}|{queueItem.ArtistName}|{queueItem.CreatorName}|{queueItem.PerformanceLink}";
        File.AppendAllText(sessionPlayHistoryFileName, nowPlaying + "\n");
    }
    #endregion

    private void SetupStartTab()
    {
        // TODO: should these be functions rather than lambdas?
        SetupTab.CountdownLengthChanged += (value) =>
        {
            Settings.CountdownLengthSeconds = (int)value;
            Settings.SaveToDisk();
        };
        SetupTab.DisplayScreenMonitorChanged += (value) =>
        {
            Settings.DisplayScreenMonitor = (int)value;
            Settings.SaveToDisk();
            GD.Print($"Monitor ID set to {Settings.DisplayScreenMonitor}");
            DisplayScreen.SetMonitorId(Settings.DisplayScreenMonitor);
            DisplayScreen.ShowDisplayScreen();
        };
        SetupTab.DisplayScreenDismissed += () =>
        {
            DisplayScreen.Dismiss();
        };
        SetupTab.BgMusicItemRemoved += (pathToRemove) =>
        {
            Settings.BgMusicFiles.Remove(pathToRemove);
            Settings.SaveToDisk();
            // TODO: if the removed item was the one playing, skip it
        };
        SetupTab.BgMusicItemsAdded += (pathsToAdd) =>
        {
            foreach (var file in pathsToAdd)
            {
                if (!Settings.BgMusicFiles.Contains(file))
                {
                    Settings.BgMusicFiles.Add(file);
                }
            }
            Settings.SaveToDisk();
        };
        SetupTab.BgMusicToggle += ToggleBackgroundMusic;
        SetupTab.BgMusicVolumeChanged += SetBgMusicVolumePercent;

        SetupTab.SetBgMusicItemsUIValues(Settings.BgMusicFiles);
        SetupTab.SetBgMusicEnabledUIValue(Settings.BgMusicEnabled);
        SetupTab.SetBgMusicVolumePercentUIValue(Settings.BgMusicVolumePercent);
        SetupTab.SetDisplayScreenMonitorUIValue(Settings.DisplayScreenMonitor);
        SetupTab.SetDisplayScreenMonitorMaxValue(DisplayServer.GetScreenCount() - 1);
        SetupTab.SetCountdownLengthSecondsUIValue(Settings.CountdownLengthSeconds);
    }
}
