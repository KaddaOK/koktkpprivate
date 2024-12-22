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

public partial class RootController : Node
{
	public bool IsPaused { get; private set; }
	public Queue<QueueItem> Queue { get; private set; }
	public QueueItem NowPlaying { get; private set; }

	public QueueItem ItemBeingAdded { get; private set; }

	private int _launchCountdownLengthSeconds = 10; // TODO: remember this setting

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SetupHistoryLogFile();
		SetupQueueTree();
		SetupMainQueueControls();
		LoadQueueFromDiskIfExists();
		SetupBackgroundMusicQueue();

		BindSearchScreenControls();
		BindDisplayScreenControls();

		SetupStartTabControls();
		GetTree().AutoAcceptQuit = false;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
		{
			Quit();
		}

	}

	public async void Quit()
	{
		await PuppeteerPlayer.CloseBrowser();
		GetTree().Quit();
	}

	public async void PlayItem(QueueItem item, CancellationToken cancellationToken)
	{
		NowPlaying = item;
		// TODO: fade in background music
		if (IsBackgroundMusicEnabled)
		{
			StartOrResumeBackgroundMusic();
		}

		ShowNextUp(item.SingerName, item.SongName, item.ArtistName);

		// Countdown logic
		int launchSecondsRemaining = _launchCountdownLengthSeconds;
		while (launchSecondsRemaining > 0)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				GD.Print("PlayItem cancelled.");
				return;
			}
			if (!IsPaused)
			{
				if (!DisplayScreen.Visible) // it may have been dismissed while paused
				{
					ShowDisplayScreen();
				}
				NextUpLaunchCountdownLabel.Text = launchSecondsRemaining.ToString();
				launchSecondsRemaining--;
			}
			await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		}
		// TODO: fade out background music
		if (IsBackgroundMusicEnabled)
		{
			PauseBackgroundMusic();
		}

		GD.Print($"Playing {item.SongName} by {item.ArtistName} ({item.CreatorName}) from {item.PerformanceLink}");
		HideDisplayScreen();
		switch (item.ItemType)
		{
			case ItemType.KarafunWeb:
				await PuppeteerPlayer.PlayKarafunUrl(item.PerformanceLink, cancellationToken);
				break;
			case ItemType.Youtube:
				await PuppeteerPlayer.PlayYoutubeUrl(item.PerformanceLink, cancellationToken);
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
		RemoveQueueTreeRow(NowPlaying);
		NowPlaying = null;
	}

	#region display screen stuff

	public Control NextUpScene { get; private set;}
	public Control EmptyQueueScene { get; private set; }
	public Window DisplayScreen { get; private set; }
	public Label NextUpSingerNameLabel { get; private set; }
	public Label NextUpSongNameLabel { get; private set; }
	public Label NextUpArtistNameLabel { get; private set; }
	public Label NextUpLaunchCountdownLabel { get; private set; }

	public bool DisplayScreenIsDismissed { get; private set; }

	public void BindDisplayScreenControls()
	{
		DisplayScreen = GetNode<Window>($"%{nameof(DisplayScreen)}");
		DisplayScreen.WindowInput += DisplayScreenWindowInput;

		NextUpScene = GetNode<Control>($"%{nameof(NextUpScene)}");
		EmptyQueueScene = GetNode<Control>($"%{nameof(EmptyQueueScene)}");

		NextUpSingerNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpSingerNameLabel)}");
		NextUpSongNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpSongNameLabel)}");
		NextUpArtistNameLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpArtistNameLabel)}");
		NextUpLaunchCountdownLabel = NextUpScene.GetNode<Label>($"%{nameof(NextUpLaunchCountdownLabel)}");
	}

	public void DisplayScreenWindowInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
			{
				DisplayScreenIsDismissed = true;
				HideDisplayScreen();
			}
		}
	}

	public void ShowEmptyQueue()
	{
		NextUpScene.Visible = false;
		EmptyQueueScene.Visible = true;
		ShowDisplayScreen();
		if (IsBackgroundMusicEnabled)
		{
			StartOrResumeBackgroundMusic();
		}
	}

	public void ShowNextUp(string singer, string song, string artist)
	{
		NextUpSingerNameLabel.Text = singer;
		NextUpSongNameLabel.Text = song;
		NextUpArtistNameLabel.Text = artist;
		NextUpLaunchCountdownLabel.Text = _launchCountdownLengthSeconds.ToString();
		NextUpScene.Visible = true;
		EmptyQueueScene.Visible = false;
		ShowDisplayScreen();
	}

	private void ShowDisplayScreen()
	{
		// TODO: make this configurable
		DisplayScreen.Mode = Window.ModeEnum.Fullscreen;

		DisplayScreen.InitialPosition = Window.WindowInitialPosition.CenterOtherScreen;
		DisplayScreen.CurrentScreen = MonitorId;
		DisplayScreen.Visible = true;
		DisplayScreenIsDismissed = false;
		DisplayScreen.Show();
		DisplayScreen.AlwaysOnTop = true;
		DisplayScreen.GrabFocus();
		// TODO: play empty queue playlist
	}

	public void HideDisplayScreen()
	{
		DisplayScreen.AlwaysOnTop = false;
		DisplayScreen.Visible = false;
		DisplayScreen.Hide();
		// TODO: pause empty queue playlist
	}

	#endregion

	#region background audio queue stuff
	private List<string> BackgroundMusicQueuePaths = new List<string>(); // TODO: get from settings
	private ItemList BgMusicItemList;
	private FileDialog BgMusicAddFileDialog;
	private string BackgroundMusicNowPlaying = null;
	private bool IsBackgroundMusicEnabled = true; // TODO: get from settings
	private AudioStreamPlayer BackgroundMusicPlayer;
	private CheckBox BgMusicEnabledCheckBox;
	private SpinBox BgMusicVolumeSpinBox;
	private Button BgMusicAddButton;

	public void SetupBackgroundMusicQueue()
	{
		BgMusicItemList = GetNode<ItemList>($"%{nameof(BgMusicItemList)}");
		BgMusicItemList.GuiInput += BgMusicItemListGuiInput;
		BackgroundMusicPlayer = GetNode<AudioStreamPlayer>($"%{nameof(BackgroundMusicPlayer)}");
		BackgroundMusicPlayer.Finished += BackgroundMusicPlayerFinished;
		BgMusicEnabledCheckBox = GetNode<CheckBox>($"%{nameof(BgMusicEnabledCheckBox)}");
		BgMusicEnabledCheckBox.Toggled += ToggleBackgroundMusic;
		BgMusicVolumeSpinBox = GetNode<SpinBox>($"%{nameof(BgMusicVolumeSpinBox)}");
		BgMusicVolumeSpinBox.ValueChanged += (value) => SetBgMusicVolumePercent(value);
		BgMusicAddFileDialog = GetNode<FileDialog>($"%{nameof(BgMusicAddFileDialog)}");
		BgMusicAddFileDialog.FileSelected += OnBgMusicFileSelected;
		BgMusicAddFileDialog.FilesSelected += OnBgMusicFilesSelected;
		BgMusicAddButton = GetNode<Button>($"%{nameof(BgMusicAddButton)}");
		BgMusicAddButton.Pressed += ShowBackgroundMusicAddDialog;
		// TODO: load background music queue from disk
		// TODO: if background music queue isn't empty, start playing it, if it's enabled
	}

	private void ToggleBackgroundMusic(bool enable)
	{
		IsBackgroundMusicEnabled = enable;
		if (IsBackgroundMusicEnabled)
		{
			StartOrResumeBackgroundMusic();
		}
		else
		{
			PauseBackgroundMusic();
		}
	}

	private void StartOrResumeBackgroundMusic()
	{
		GD.Print($"BG Music: {BackgroundMusicQueuePaths.Count} items in queue. Stream: {(BackgroundMusicPlayer.Stream == null ? "null" : "present")}, Playing: {BackgroundMusicPlayer.Playing}, Paused: {BackgroundMusicPlayer.StreamPaused}");
		if (BackgroundMusicQueuePaths.Count > 0)
		{
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
		}
	}


	private void SetBgMusicVolumePercent(double volumePercent)
	{
		BackgroundMusicPlayer.VolumeDb = (float)Mathf.LinearToDb(volumePercent/100D);
	}
	private void BackgroundMusicPlayerFinished()
	{
		if (BackgroundMusicQueuePaths.Count > 0)
		{
			var oldNowPlaying = BackgroundMusicNowPlaying;
			var previousPlaylistIndex = BackgroundMusicQueuePaths.IndexOf(oldNowPlaying);
			var nextIndexToPlay = previousPlaylistIndex + 1;
			if (nextIndexToPlay >= BackgroundMusicQueuePaths.Count)
			{
				nextIndexToPlay = 0;
			}
			StartPlayingBackgroundMusic(nextIndexToPlay);
		}
	}

	private void StartPlayingBackgroundMusic(int indexToPlay)
	{
			BackgroundMusicNowPlaying = BackgroundMusicQueuePaths[indexToPlay];
			BackgroundMusicPlayer.Stream = LoadAudioFromPath(BackgroundMusicNowPlaying);
			SetBgMusicVolumePercent(BgMusicVolumeSpinBox.Value);
			BackgroundMusicPlayer.Play();
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

	private void ShowBackgroundMusicAddDialog()
	{
		BgMusicAddFileDialog.Visible = true;
	}

	private void OnBgMusicFileSelected(string file)
	{
		OnBgMusicFilesSelected(new string[] { file });
	}
	private void OnBgMusicFilesSelected(string[] files)
	{
		BgMusicAddFileDialog.Visible = false;
		foreach (var file in files)
		{
			if (!BackgroundMusicQueuePaths.Contains(file))
			{
				BackgroundMusicQueuePaths.Add(file);
			}

            // Check if the file name already exists in BgMusicItemList
            bool existsInItemList = false;
            for (int i = 0; i < BgMusicItemList.ItemCount; i++)
            {
                if (BgMusicItemList.GetItemText(i) == file)
                {
                    existsInItemList = true;
                    break;
                }
            }

            if (!existsInItemList)
            {
                BgMusicItemList.AddItem(file);
            }
		}
	}

	public void BgMusicItemListGuiInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Pressed && keyEvent.Keycode == Key.Delete)
			{
				var selectedItemIndex = BgMusicItemList.GetSelectedItems().FirstOrDefault();
				if (selectedItemIndex > -1)
				{
					var pathToRemove = BgMusicItemList.GetItemText(selectedItemIndex);

					// remove from display list
					BgMusicItemList.RemoveItem(selectedItemIndex);
					// remove from actual queue
					BackgroundMusicQueuePaths.Remove(pathToRemove);
					// TODO: if the removed item was the one playing, skip it
				}
			}
		}
	}

	#endregion

	#region main queue stuff

	private Tree QueueTree;
	private TreeItem _queueRoot;
	private Button MainQueuePlayPauseButton;
	private Button MainQueueSkipButton;

	private CancellationTokenSource PlayingCancellationSource = new CancellationTokenSource();
	private void SetupQueueTree()
	{
		QueueTree = GetNode<Tree>($"%{nameof(QueueTree)}");
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
		}
		else
		{
			PauseQueue();
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
		//GD.Print($"Added queue item: {item.SingerName} - {item.SongName} - {item.ArtistName} - {item.CreatorName} - {item.PerformanceLink}");
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
			GD.Print($"Queue JSON: {queueJson}");
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

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!IsPaused && NowPlaying == null)
		{
			if (Queue.Count > 0)
			{
				// Save the queue to disk right before we pop it (in case playing fails)
				SaveQueueToDisk();
				PlayingCancellationSource.Cancel();
				PlayingCancellationSource = new CancellationTokenSource();
				PlayItem(Queue.Dequeue(), PlayingCancellationSource.Token);
				AppendToPlayHistory(NowPlaying);
			}
			else if (!DisplayScreen.Visible && !DisplayScreenIsDismissed)
			{
				ShowEmptyQueue();
				// Save the queue to disk because it's now empty
				SaveQueueToDisk();
				if (IsBackgroundMusicEnabled)
				{
					StartOrResumeBackgroundMusic();
				}
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

	#region Start tab stuff
	private Button LaunchUnautomatedButton;
	private Button LaunchAutomatedButton;
	private SpinBox MonitorSpinbox;
	private SpinBox WaitSpinbox;
	private Button ApplyMonitorButton;
	private Button HideDisplayScreenButton;
	private int MonitorId = 1;

	private void SetupStartTabControls()
	{
		LaunchUnautomatedButton = GetNode<Button>($"%{nameof(LaunchUnautomatedButton)}");
		LaunchAutomatedButton = GetNode<Button>($"%{nameof(LaunchAutomatedButton)}");
		MonitorSpinbox = GetNode<SpinBox>($"%{nameof(MonitorSpinbox)}");
		// Set the max value of the spinbox to the number of monitors
		MonitorSpinbox.MaxValue = DisplayServer.GetScreenCount() - 1;
		MonitorSpinbox.Value = MonitorId;

		WaitSpinbox = GetNode<SpinBox>($"%{nameof(WaitSpinbox)}");
		WaitSpinbox.Value = _launchCountdownLengthSeconds;
		WaitSpinbox.ValueChanged += (value) => _launchCountdownLengthSeconds = (int)value;


		ApplyMonitorButton = GetNode<Button>($"%{nameof(ApplyMonitorButton)}");
		HideDisplayScreenButton = GetNode<Button>($"%{nameof(HideDisplayScreenButton)}");

		LaunchUnautomatedButton.Pressed += LaunchUnautomatedButtonPressed;
		LaunchAutomatedButton.Pressed += LaunchAutomatedButtonPressed;
		ApplyMonitorButton.Pressed += ApplyMonitorButtonPressed;
		HideDisplayScreenButton.Pressed += HideMonitorButtonPressed;
	}

	private void LaunchUnautomatedButtonPressed()
	{
		// TODO: maybe make it configurable what URLs to launch
		PuppeteerPlayer.LaunchUnautomatedBrowser("https://www.karafun.com/my/", "https://www.youtube.com/account");
	}
	private void LaunchAutomatedButtonPressed()
	{
		PuppeteerPlayer.LaunchAutomatedBrowser();
	}
	private void ApplyMonitorButtonPressed()
	{
		MonitorId = (int)MonitorSpinbox.Value;
		GD.Print($"Monitor ID set to {MonitorId}");
		ShowDisplayScreen();
	}
	private void HideMonitorButtonPressed()
	{
		DisplayScreenIsDismissed = true;
		HideDisplayScreen();
	}
	#endregion

	#region search tab stuff
	private Tree KfnResultsTree;
	private Tree KNResultsTree;
	private TreeItem _kfnRoot;
	private TreeItem _knRoot;
	private LineEdit SearchText;
	private Button SearchButton;
	private Button ClearSearchButton;
	private List<KarafunSearchScrapeResultItem> KarafunResults;
	private List<KNSearchResultItem> KNResults;
	private Boolean IsStreamingResults = false;

	private ConfirmationDialog AddToQueueDialog;
	private LineEdit EnterSingerName;
	private bool IsAddToQueueResolvingPerformLink = false;
	private Label QueueAddSongNameLabel;
	private Label QueueAddArtistNameLabel;
	private Label QueueAddCreatorNameLabel;

	private void BindSearchScreenControls()
	{
		SetupKfnTree();
		SetupKNTree();
		SetupSearchText();
		SetupSearchButton();
		SetupAddToQueueDialog();
	}

	private void SetupSearchText()
	{
		SearchText = GetNode<LineEdit>($"%{nameof(SearchText)}");
		SearchText.TextSubmitted += Search;
	}
	
	private void SetupSearchButton()
	{
		SearchButton = GetNode<Button>($"%{nameof(SearchButton)}");
		SearchButton.Pressed += () => Search(SearchText.Text);

		ClearSearchButton = GetNode<Button>($"%{nameof(ClearSearchButton)}");
		ClearSearchButton.Pressed += () => {
			SearchText.Text = "";
			KfnResultsTree.Clear();
			KNResultsTree.Clear();
			SearchText.GrabFocus();
		};
	}

	private void SetupKfnTree()
	{
		KfnResultsTree = GetNode<Tree>($"%{nameof(KfnResultsTree)}");
		KfnResultsTree.Columns = 2;
		KfnResultsTree.SetColumnTitle(0, "Song Name");
		KfnResultsTree.SetColumnTitle(1, "Artist Name");
		KfnResultsTree.SetColumnTitlesVisible(true);
		KfnResultsTree.HideRoot = true;

		// Create the root of the tree
		_kfnRoot = KfnResultsTree.CreateItem();

		// Connect the double-click event
		KfnResultsTree.ItemActivated += OnKfnItemDoubleClicked;
	}

	private void SetupKNTree()
	{
		KNResultsTree = GetNode<Tree>($"%{nameof(KNResultsTree)}");
		KNResultsTree.Columns = 3;
		KNResultsTree.SetColumnTitle(0, "Song Name");
		KNResultsTree.SetColumnTitle(1, "Artist Name");
		KNResultsTree.SetColumnTitle(2, "Creator");
		KNResultsTree.SetColumnTitlesVisible(true);
		KNResultsTree.HideRoot = true;

		// Create the root of the tree
		_knRoot = KNResultsTree.CreateItem();

		// Connect the double-click event
		KNResultsTree.ItemActivated += OnKNItemDoubleClicked;
	}

	private void SetupAddToQueueDialog()
	{
		AddToQueueDialog = GetNode<ConfirmationDialog>($"%{nameof(AddToQueueDialog)}");
		EnterSingerName = AddToQueueDialog.GetNode<LineEdit>($"%{nameof(EnterSingerName)}");
		EnterSingerName.TextSubmitted += (_) => AddToQueueDialogConfirmed();
		QueueAddSongNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddSongNameLabel)}");
		QueueAddArtistNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddArtistNameLabel)}");
		QueueAddCreatorNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddCreatorNameLabel)}");
		AddToQueueDialog.Confirmed += AddToQueueDialogConfirmed;
		AddToQueueDialog.Canceled += CloseAddToQueueDialog;
	}

	private void CloseAddToQueueDialog()
	{
		ItemBeingAdded = null;
		AddToQueueDialog.Hide();
	}

	private void AddToQueueDialogConfirmed()
	{
		if (ItemBeingAdded != null && !IsAddToQueueResolvingPerformLink)
		{
			ItemBeingAdded.SingerName = EnterSingerName.Text;
			EnterSingerName.Text = "";
			Queue.Enqueue(ItemBeingAdded);
			// serialize queue to disk
			SaveQueueToDisk();
			AddQueueTreeRow(ItemBeingAdded);
			CloseAddToQueueDialog();
		}
	}

	private async Task ToggleIsSearching(bool isSearching)
	{
		SearchText.Editable = !isSearching;
		SearchButton.Disabled = isSearching;
		SearchButton.Text = isSearching ? "Searching..." : "Search";
		IsStreamingResults = isSearching;
		Input.SetDefaultCursorShape(isSearching ? Input.CursorShape.Busy : Input.CursorShape.Arrow);
		await ToSignal(GetTree(), "process_frame");
	}

	private async void Search(string query)
	{
		if (IsStreamingResults)
		{
			GD.Print("Already streaming results, skipping search.");
			return;
		}
		await ToggleIsSearching(true);

		var searchKaraokenerds = true; // TODO: Implement a setting to enable/disable searching Karaokenerds
		var searchKarafun = true; // TODO: Implement a setting to enable/disable searching Karafun

		var searchTasks = new List<Task>();
		if (searchKaraokenerds)
		{
			KNResultsTree.Clear();
			_knRoot = KNResultsTree.CreateItem(); // Recreate the root item after clearing the tree
			searchTasks.Add(GetResultsFromKaraokenerds(query));
		}
		if (searchKarafun)
		{
			KfnResultsTree.Clear();
			_kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
			searchTasks.Add(StreamResultsFromKarafun(query));
		}
		await Task.WhenAll(searchTasks);
		await ToggleIsSearching(false);
	}

	private async Task GetResultsFromKaraokenerds(string query)
	{
		GD.Print($"Searching KN for: {query}");
		var results = await KaraokenerdsSearchScrape.Search(query);
		GD.Print($"Received {results.Count} results from KN");
		KNResults = results;
		foreach (var result in KNResults)
		{
			AddKNResultsRow(result);
		}
		await ToSignal(GetTree(), "process_frame");
	}

	private async Task StreamResultsFromKarafun(string query)
	{
		GD.Print($"Searching Karafun for: {query}");
		IsStreamingResults = true;
		var mayHaveMore = false;
		var pageResults = new List<KarafunSearchScrapeResultItem>();
		var artistResults = new Dictionary<string, List<KarafunSearchScrapeResultItem>>();
		KarafunResults = new List<KarafunSearchScrapeResultItem>();
		await foreach (var result in KarafunSearchScrape.Search(query))
		{
			GD.Print($"Received {result.Results.Count} results from Karafun");
			if (result.MayHaveMore) {
				mayHaveMore = true;
			}
			if (result.PartOfArtistSet != null)
			{
				if (artistResults.ContainsKey(result.PartOfArtistSet))
				{
					artistResults[result.PartOfArtistSet].AddRange(result.Results);
				}
				else
				{
					artistResults.Add(result.PartOfArtistSet, result.Results);
				}
			}
			else
			{
				pageResults.AddRange(result.Results);
			}
			
			KarafunResults = new List<KarafunSearchScrapeResultItem>(pageResults);
			foreach (var artist in artistResults)
			{
				// Find the index of the Artist item and replace it with the new results
				int artistIndex = KarafunResults.FindIndex(a => a.ResultType == KarafunSearchScrapeResultItemType.Artist && a.ArtistLink == artist.Key);
				if (artistIndex != -1)
				{
					KarafunResults.RemoveAt(artistIndex);
					KarafunResults.InsertRange(artistIndex, artist.Value);
				}
			}
			KarafunResults = KarafunResults
				.Where(r => r.ResultType != KarafunSearchScrapeResultItemType.Artist)
				.DistinctBy(r => r.SongInfoLink)
				.OrderBy(item => item.ResultType == KarafunSearchScrapeResultItemType.UnlicensedSong ? 1 : 0)
				.ThenBy(item => KarafunResults.IndexOf(item)) // Preserve the original relative order
				.ToList();

			await UpdateKarafunResultsTree();
		}
		IsStreamingResults = false;
	}

	private async Task UpdateKarafunResultsTree()
	{
		// Track user selections
		var selectedItems = new List<string>();
		var selectedItem = KfnResultsTree.GetSelected();
		if (selectedItem != null)
		{
			selectedItems.Add(selectedItem.GetMetadata(0).ToString()); // Use metadata to track selections
		}

		//GD.Print($"Updating karafun tree with {KarafunResults.Count} results");

		//actually probably fine to just clear and re-add everything
		KfnResultsTree.Clear();
		_kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
		foreach (var result in KarafunResults)
		{
			AddKarafunResultsRow(result);
		}

		// Restore user selections
		foreach (var item in selectedItems)
		{
			var treeItem = FindTreeItemByMetadata(item);
			if (treeItem != null)
			{
				KfnResultsTree.SetSelected(treeItem, 0);
			}
		}

		await ToSignal(GetTree(), "process_frame");
	}

	private TreeItem FindTreeItemByMetadata(string metadata)
	{
		var items = _kfnRoot.GetChildren();
		var item = items.FirstOrDefault();
		while (item != null)
		{
			if (item.GetMetadata(0).ToString() == metadata)
			{
				return item;
			}
			item = item.GetNext();
		}
		return null;
	}

	private void AddKarafunResultsRow(KarafunSearchScrapeResultItem item)
	{
		if (_kfnRoot == null)
		{
			GD.Print("Kfn root item is disposed, recreating it.");
			_kfnRoot = KfnResultsTree.CreateItem();
		}
		var treeItem = KfnResultsTree.CreateItem(_kfnRoot);
		treeItem.SetText(0, item.SongName);
		treeItem.SetText(1, item.ArtistName);
		treeItem.SetMetadata(0, item.SongInfoLink);
	}

	private void AddKNResultsRow(KNSearchResultItem item)
	{
		if (_knRoot == null)
		{
			GD.Print("KN root item is disposed, recreating it.");
			_knRoot = KNResultsTree.CreateItem();
		}
		var treeItem = KNResultsTree.CreateItem(_knRoot);
		treeItem.SetText(0, item.SongName);
		treeItem.SetText(1, item.ArtistName);
		treeItem.SetText(2, item.CreatorBrandName);
		treeItem.SetMetadata(0, item.YoutubeLink);
	}

	private void ShowAddToQueueDialog(string songName, string artistName, string creatorName)
	{
		SetAddToQueueBoxText(songName, artistName, creatorName);
		AddToQueueDialog.PopupCentered();
		EnterSingerName.GrabFocus();
	}
	private void SetAddToQueueBoxText(string songName, string artistName, string creatorName)
	{
		QueueAddSongNameLabel.Text = songName;
		QueueAddArtistNameLabel.Text = artistName;
		QueueAddCreatorNameLabel.Text = creatorName;
	}

	private async void OnKfnItemDoubleClicked()
	{
		TreeItem selectedItem = KfnResultsTree.GetSelected();
		if (selectedItem != null)
		{
			string songName = selectedItem.GetText(0);
			string artistName = selectedItem.GetText(1);
			GD.Print($"Double-clicked: {songName} by {artistName}");
			string songInfoLink = selectedItem.GetMetadata(0).ToString();

			IsAddToQueueResolvingPerformLink = true;
			ShowAddToQueueDialog("Loading, please wait...", "Loading, please wait...", "Karafun (loading perform link)");
			await ToSignal(GetTree(), "process_frame");

			GD.Print($"Getting perform link for {songInfoLink}");
			var performLink = await KarafunSearchScrape.GetDirectPerformanceLinkForSong(songInfoLink);
			GD.Print($"Perform link: {performLink}");

			ItemBeingAdded = new QueueItem
			{
				SongName = songName,
				ArtistName = artistName,
				CreatorName = "Karafun",
				SongInfoLink = songInfoLink,
				PerformanceLink = performLink,
				ItemType = ItemType.KarafunWeb
			};

			IsAddToQueueResolvingPerformLink = false;
			SetAddToQueueBoxText(songName, artistName, "Karafun Web");
		}
	}

	private async void OnKNItemDoubleClicked()
	{
		TreeItem selectedItem = KNResultsTree.GetSelected();
		if (selectedItem != null)
		{
			string songName = selectedItem.GetText(0);
			string artistName = selectedItem.GetText(1);
			string creatorName = selectedItem.GetText(2);
			string youtubeLink = selectedItem.GetMetadata(0).ToString();
			GD.Print($"Double-clicked: {songName} by {artistName} ({creatorName}), {youtubeLink}");
			
			ItemBeingAdded = new QueueItem
			{
				SongName = songName,
				ArtistName = artistName,
				CreatorName = creatorName,
				PerformanceLink = youtubeLink,
				ItemType = ItemType.Youtube
			};

			IsAddToQueueResolvingPerformLink = false;
			ShowAddToQueueDialog(songName, artistName, creatorName);
		}
	}

	#endregion
}
