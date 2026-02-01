using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using KOKTKaraokeParty.Controls;
using KOKTKaraokeParty.Controls.SessionPrepWizard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KOKTKaraokeParty;

[Meta(typeof(IAutoNode))]
public partial class RootController : Node, 
IProvide<IBrowserProviderNode>, 
IProvide<IYtDlpProviderNode>,
IProvide<IKarafunRemoteProviderNode>,
IProvide<Settings>, 
IProvide<IMonitorIdentificationManager>,
IProvide<IDisplayScreen>
{
	#region Service Dependencies

	private SessionPreparationService _sessionPreparation;
	private QueueManagementService _queueManagement;
	private BackgroundMusicService _backgroundMusic;
	private PlaybackCoordinationService _playbackCoordination;
	private SessionUIService _sessionUI;


	#endregion

	#region Provided Dependencies
	IBrowserProviderNode IProvide<IBrowserProviderNode>.Value() => BrowserProvider;
	IYtDlpProviderNode IProvide<IYtDlpProviderNode>.Value() => YtDlpProvider;
	IKarafunRemoteProviderNode IProvide<IKarafunRemoteProviderNode>.Value() => KarafunRemoteProvider;
    IMonitorIdentificationManager IProvide<IMonitorIdentificationManager>.Value() => MonitorIdManager;
	IDisplayScreen IProvide<IDisplayScreen>.Value() => DisplayScreen;

	private Settings Settings { get; set; }
	Settings IProvide<Settings>.Value() => Settings;

	private ILocalFileValidator LocalFileValidator { get; set; }
	private IFileWrapper FileWrapper { get; set;}

	public void SetupForTesting(
		Settings settings,
		ILocalFileValidator localFileValidator,
		IFileWrapper fileWrapper)
	{
		Settings = settings;
		LocalFileValidator = localFileValidator;
		FileWrapper = fileWrapper;
	}

	public void Initialize()
	{
		FileWrapper = new FileWrapper();
		Settings = Settings.LoadFromDiskIfExists(FileWrapper);
		LocalFileValidator = new LocalFileValidator();
	}

	#endregion

	#region Nodes

	[Node("%Setup")] // TODO: this name is annoying to me
	private ISetupTab SetupTab { get; set; } = default!;

	[Node("%Search")] // TODO: this name is annoying to me
	private ISearchTab SearchTab { get; set; } = default!;

	[Node] private ITabContainer MainTabs { get; set; } = default!;
	[Node] private IDisplayScreen DisplayScreen { get; set; } = default!;
	[Node] private IAcceptDialog MessageDialog { get; set; } = default!;

	[Node] private ILabel ProgressSliderLabel { get; set; } = default!;
	[Node] private ILabel CurrentTimeLabel { get; set; } = default!;
	[Node] private ILabel DurationLabel { get; set; } = default!;
	[Node] private IHSlider MainWindowProgressSlider { get; set; } = default!;

	// Session preparation UI
	[Node] private IBrowserProviderNode BrowserProvider { get; set; } = default!;
	[Node] private IYtDlpProviderNode YtDlpProvider { get; set; } = default!;
	[Node] private IKarafunRemoteProviderNode KarafunRemoteProvider { get; set; } = default!;
	[Node] private IWindow PrepareSessionDialog { get; set; } = default!;
	[Node] private ITree SessionPreparationTree { get; set; } = default!;
	[Node] private IButton PrepareSessionOKButton { get; set; } = default!;
	[Node] private IButton LaunchForLoginsButton { get; set; } = default!;
	[Node] private IWindow WaitingForBrowserDialog { get; set; } = default!;
	[Node] private IButton PrepareQuitButton { get; set; } = default!;
	[Node] private ISpinBox MonitorSpinBox { get; set; } = default!;
	[Node] private ILabel MonitorWarningLabel { get; set; } = default!;
	[Node] private IButton IdentifyMonitorsButton { get; set; } = default!;
    [Node] private IMonitorIdentificationManager MonitorIdManager { get; set; } = default!;
	
	// Session Prep Wizard (new startup flow)
	[Node("%SessionPrepWizard")] private ISessionPrepWizard SessionPrepWizard { get; set; } = default!;
	
	// Karafun Web Player controls in PrepareSessionDialog
	[Node] private IButton LaunchKarafunWebPlayerButton { get; set; } = default!;
	[Node] private ILineEdit KarafunRoomCodeEdit { get; set; } = default!;
	[Node] private IButton ConnectKarafunRemoteButton { get; set; } = default!;
	[Node] private ILabel KarafunRemoteStatusLabel { get; set; } = default!;
	
	// Service confirmation dialog (when not all services are ready)
	[Node] private IConfirmationDialog ServiceConfirmationDialog { get; set; } = default!;
	[Node] private ILabel ServiceConfirmationMessage { get; set; } = default!;

	// Queue management UI
	private DraggableTree QueueTree;
	private TreeItem _queueRoot;
	private Button MainQueuePlayPauseButton;
	private Button MainQueueSkipButton;

	#endregion

	#region State

	private bool IsWaitingToReturnFromBrowserControl { get; set; }
	private string sessionPlayHistoryFileName;

	#endregion

	public void OnReady()
	{
		Initialize();
		
		DisplayServer.WindowSetMinSize(new Vector2I(1000, 700));

		SetupHistoryLogFile();
		SetupServices();
		SetupUI();
		BindEvents();
		
		GetTree().AutoAcceptQuit = false;
		GetTree().Root.FilesDropped += FilesDropped;

		this.Provide();
		
		// Start the session prep wizard instead of old dialog
		// Use CallDeferred to allow the wizard to initialize its dependencies first
		Callable.From(StartSessionPrepWizard).CallDeferred();
	}

	private void StartSessionPrepWizard()
	{
		// Get saved queue items for wizard preview
		var savedQueueItems = Utils.GetSavedQueueItemsFromDisk();
		
		// Wire up wizard events
		SessionPrepWizard.WizardCompleted += OnWizardCompleted;
		SessionPrepWizard.WizardCancelled += OnWizardCancelled;
		
		// Start the wizard
		SessionPrepWizard.StartWizard(savedQueueItems);
	}
	
	private void OnWizardCompleted(WizardState state)
	{
		// Save service selections to settings
		Settings.UseLocalFiles = state.UseLocalFiles;
		Settings.UseYouTube = state.UseYouTube;
		Settings.UseKarafun = state.UseKarafun;
		Settings.KarafunMode = state.KarafunMode;
		Settings.DisplayScreenMonitor = state.SelectedMonitor;
		Settings.SaveToDisk(FileWrapper);
		
		// Handle queue restoration choice
		HandleQueueRestoration(state);
		
		// Configure search tab based on wizard selections
		SearchTab.ConfigureAvailableServices(
			state.UseLocalFiles,
			state.UseYouTube,
			state.UseKarafun
		);
		
		// Sync SetupTab with display settings
		SetupTab.SetDisplayScreenMonitorUIValue(state.SelectedMonitor);
		
		SetProcess(true);
	}
	
	private void HandleQueueRestoration(WizardState state)
	{
		switch (state.QueueRestoreChoice)
		{
			case QueueRestoreOption.StartFresh:
				// Clear the queue that was already loaded and delete the save file
				_queueManagement.ClearQueue();
				RefreshQueueTree();
				break;
			case QueueRestoreOption.YesExceptFirst:
				// Remove the first item (presumably what was playing when the app closed)
				_queueManagement.RemoveFirstItem();
				RefreshQueueTree();
				break;
			case QueueRestoreOption.YesAll:
			case QueueRestoreOption.NotSet:
				// Queue will be loaded as-is by QueueManagementService
				break;
		}
	}
	
	private void OnWizardCancelled()
	{
		Quit();
	}

	private void SetupServices()
	{
		// Create and initialize services
		_sessionPreparation = new SessionPreparationService();
		_queueManagement = new QueueManagementService();
		_backgroundMusic = new BackgroundMusicService();
		_playbackCoordination = new PlaybackCoordinationService();
		_sessionUI = new SessionUIService();

		// Add them to the scene tree so they can use Godot APIs
		AddChild(_sessionPreparation);
		AddChild(_queueManagement);
		AddChild(_backgroundMusic);
		AddChild(_playbackCoordination);
		AddChild(_sessionUI);

		// Initialize them with their dependencies
		BrowserProvider.Initialize();
		BrowserProvider.SetSettings(Settings);
		KarafunRemoteProvider.Initialize();
		_sessionPreparation.Initialize(DisplayScreen, BrowserProvider, YtDlpProvider, KarafunRemoteProvider);
		_queueManagement.Initialize(FileWrapper, YtDlpProvider);
		_backgroundMusic.Initialize(Settings, FileWrapper, DisplayScreen);
		_playbackCoordination.Initialize(Settings, DisplayScreen, BrowserProvider, KarafunRemoteProvider, _backgroundMusic);
		_sessionUI.Initialize(_sessionPreparation);
	}

	private void SetupUI()
	{
		SetupQueueTree();
		SetupMainQueueControls();
		BindDisplayScreenControls();
		BindSearchScreenControls();
		SetupStartTab();
		SetupMonitorIdentification();
	}

	private void BindEvents()
	{
		// Session preparation events
		_sessionPreparation.SessionStatusUpdated += OnSessionStatusUpdated;
		
		// Queue management events
		_queueManagement.ItemAdded += OnQueueItemAdded;
		_queueManagement.ItemRemoved += OnQueueItemRemoved;
		_queueManagement.NowPlayingChanged += OnNowPlayingChanged;
		_queueManagement.PausedStateChanged += OnQueuePausedStateChanged;
		_queueManagement.QueueLoaded += OnQueueLoaded;
		_queueManagement.QueueReordered += RefreshQueueTree;
		
		// Refresh queue tree now if items were already loaded before events were bound
		if (_queueManagement.GetQueueItems().Any() || _queueManagement.NowPlaying != null)
		{
			GD.Print("Queue has items from disk, refreshing tree after event binding...");
			RefreshQueueTree();
		}
		
		// Search tab events
		SearchTab.ItemAddedToQueue += _queueManagement.AddToQueue;
		
		// Playback coordination events
		_playbackCoordination.PlaybackDurationChanged += UpdatePlaybackDuration;
		_playbackCoordination.PlaybackProgressChanged += (progressMs) => CallDeferred(nameof(UpdatePlaybackProgress), progressMs);
		_playbackCoordination.PlaybackFinished += OnPlaybackFinished;
		_playbackCoordination.ProgressSliderUpdateRequested += (stateText, maxSeconds, valueSeconds, enableEditing) => 
			Callable.From(() => SetProgressSlider(stateText, maxSeconds, valueSeconds, enableEditing)).CallDeferred();
		
		// Session preparation dialog events
		PrepareSessionOKButton.Pressed += OnPrepareSessionOKPressed;
		LaunchForLoginsButton.Pressed += OnLaunchForLoginsPressed;
		PrepareQuitButton.Pressed += Quit;
		ServiceConfirmationDialog.Confirmed += OnServiceConfirmationAccepted;
		
		// Karafun web player controls in PrepareSessionDialog
		LaunchKarafunWebPlayerButton.Pressed += OnLaunchKarafunWebPlayerPressed;
		ConnectKarafunRemoteButton.Pressed += OnConnectKarafunRemotePressed;
		KarafunRoomCodeEdit.TextChanged += OnKarafunRoomCodeChanged;
		
		// Monitor controls in PrepareSessionDialog
		MonitorSpinBox.ValueChanged += OnPrepareDialogMonitorChanged;
		IdentifyMonitorsButton.Pressed += OnIdentifyMonitorsPressed;
	}

	private void OnSessionStatusUpdated(PrepareSessionModel model)
	{
		// Initialize monitor controls
		InitializePrepareDialogMonitorControls();
		
		// Initialize Karafun controls
		InitializeKarafunControls();
		
		// Update Karafun remote status label
		KarafunRemoteStatusLabel.Text = model.KarafunRemoteMessage;
		
		_sessionUI.PopulateSessionTree(SessionPreparationTree, model, 
			PrepareSessionOKButton, LaunchForLoginsButton);
	}
	
	private void InitializeKarafunControls()
	{
		Callable.From(() => 
		{
			// Set up the Karafun room code edit with current settings
			KarafunRoomCodeEdit.Text = Settings.KarafunRoomCode ?? "";
		}).CallDeferred();
	}

	private void InitializePrepareDialogMonitorControls()
	{
		// Set up the monitor spinbox with current settings
		var screenCount = DisplayServer.GetScreenCount();
		MonitorSpinBox.MinValue = 0;
		MonitorSpinBox.MaxValue = Math.Max(0, screenCount - 1);
		MonitorSpinBox.Value = Settings.DisplayScreenMonitor;
		
		// Show single monitor warning if needed
		if (screenCount <= 1)
		{
			ShowMessageDialog("No Additional Displays Detected!", 
				"\nOnly one display monitor has been detected on this system.\n\n" +
				"This app is designed to be used in a \"kiosk\" style with multiple screens:\n\n" +
				"• Search and queue in the main window on one screen (with a keyboard and mouse),\n" +
				"• The other larger screen facing the performers to display the lyrics and next up.\n\n" +
				"It will be difficult to use the app with only one screen.\n" + 
                "(You will need to press ESC to dismiss the full-screen display window between songs.)\n");
		}
		
		UpdateMonitorWarning();
	}

	private void UpdateMonitorWarning()
	{
        Callable.From(() => {
            // Check if the selected monitor is the same as the main window
            var mainWindowScreen = DisplayServer.WindowGetCurrentScreen();
            var selectedMonitor = Settings.DisplayScreenMonitor;
            var totalScreens = DisplayServer.GetScreenCount();
            
            // Show warning if same monitor or invalid monitor
            var showWarning = (selectedMonitor == mainWindowScreen) || (selectedMonitor >= totalScreens);
            
            MonitorWarningLabel.Visible = showWarning;
            if (showWarning)
            {
                if (selectedMonitor >= totalScreens)
                {
                    MonitorWarningLabel.Text = "❌";
                    MonitorWarningLabel.TooltipText = "Invalid monitor selection";
                }
                else
                {
                    MonitorWarningLabel.Text = "⚠";
                    MonitorWarningLabel.TooltipText = "Display screen is set to same monitor as main window";
                }
            }
        }).CallDeferred();
	}

	private void OnPrepareDialogMonitorChanged(double value)
	{
		var monitorId = (int)value;
		Settings.DisplayScreenMonitor = monitorId;
		Settings.SaveToDisk(FileWrapper);
		DisplayScreen.SetMonitorId(Settings.DisplayScreenMonitor);
		
		// Sync with SetupTab
		SetupTab.SetDisplayScreenMonitorUIValue(Settings.DisplayScreenMonitor);
		
		UpdateMonitorWarning();
	}
	
	private async void OnLaunchKarafunWebPlayerPressed()
	{
		GD.Print("Launching Karafun web player from session preparation dialog");
		await BrowserProvider.LaunchKarafunWebPlayer(Settings);
	}
	
	private void OnKarafunRoomCodeChanged(string newCode)
	{
		Settings.KarafunRoomCode = newCode;
		Settings.SaveToDisk(FileWrapper);
	}
	
	private async void OnConnectKarafunRemotePressed()
	{
		if (string.IsNullOrWhiteSpace(Settings.KarafunRoomCode))
		{
			GD.PrintErr("Cannot connect: room code is empty");
			return;
		}
		
		GD.Print($"Connecting to Karafun remote with room code: {Settings.KarafunRoomCode}");
		await KarafunRemoteProvider.ConnectAsync(Settings.KarafunRoomCode);
	}

	private async void OnLaunchForLoginsPressed()
	{
		var process = await BrowserProvider.LaunchUncontrolledBrowser(Settings, "https://www.karafun.com/my/", "https://www.youtube.com/account");
		WaitingForBrowserDialog.Show();
		GD.Print($"Uncontrolled Browser Process ID: {process.Id}");
		
		process.Exited += (sender, args) =>
		{
			GD.Print("Browser process exited, resuming session preparation.");
			Callable.From(() =>
			{
				WaitingForBrowserDialog.Hide();
				_sessionPreparation.StartSessionPreparation(); // Refresh status
			}).CallDeferred();
		};
	}

	private void OnPrepareSessionOKPressed()
	{
		var model = _sessionPreparation.GetCurrentSessionModel();
		
		// Check if all services are ready
		if (_sessionUI.AreAllServicesReady(model))
		{
			// All services ready, proceed immediately
			ProceedWithAvailableServices();
		}
		else if (_sessionUI.AreAllServicesUnusable(model))
		{
			// This shouldn't happen since button should be disabled, but handle it anyway
			ShowMessageDialog("Cannot Proceed", "No services are available. Please wait for services to initialize or check for errors.");
		}
		else
		{
			// Some services are ready, show confirmation dialog
			_sessionUI.PopulateServiceConfirmationDialog(ServiceConfirmationMessage, model);
			ServiceConfirmationDialog.PopupCentered();
		}
	}

	private void OnServiceConfirmationAccepted()
	{
		ServiceConfirmationDialog.Hide();
		ProceedWithAvailableServices();
	}

	private void ProceedWithAvailableServices()
	{
		// Configure search tab based on which services are actually available
		var model = _sessionPreparation.GetCurrentSessionModel();
		SearchTab.ConfigureAvailableServices(
			_sessionPreparation.IsLocalFilesUsable(model),
			_sessionPreparation.IsYouTubeUsable(model),
			_sessionPreparation.IsKarafunUsable(model)
		);
		
		PrepareSessionDialog.Hide();
		SetProcess(true);
	}

	private void OnQueueItemAdded(QueueItem item)
	{
		AddQueueTreeRow(item);
	}

	private void OnQueueItemRemoved(QueueItem item)
	{
		RemoveQueueTreeRow(item);
	}

	private void OnQueueLoaded()
	{
		GD.Print("Queue loaded from disk, refreshing queue tree...");
		RefreshQueueTree();
	}

	private void OnNowPlayingChanged(QueueItem nowPlaying)
	{
		if (nowPlaying != null)
		{
			AppendToPlayHistory(nowPlaying);
		}
	}

	private void OnQueuePausedStateChanged(bool isPaused)
	{
		MainQueuePlayPauseButton.Text = isPaused ? "▶️" : "⏸️";
		DisplayScreen.ToggleQueuePaused(isPaused);
	}

	private void OnPlaybackFinished(QueueItem item)
	{
		Callable.From(() => 
		{
			_queueManagement.FinishedPlaying(item);
			RemoveQueueTreeRow(item);
			DisplayScreen.ClearDismissed();
			DisplayScreen.HideDisplayScreen();
		}).CallDeferred();
	}

	public void FilesDropped(string[] files)
	{
		var acceptedExtensions = new[] { ".zip", ".cdg", ".mp3", ".mp4" };
		var droppedFile = files.FirstOrDefault(f => acceptedExtensions.Contains(Path.GetExtension(f).ToLower()));
		if (droppedFile != null)
		{
			var (isValid, message) = LocalFileValidator.IsValid(droppedFile);
			if (!isValid)
			{
				ShowMessageDialog("Cannot import file", message);
				return;
			}

			// Switch to the search tab if not already there
			if (!SearchTab.Visible)
			{
				MainTabs.CurrentTab = 1; // TODO: don't hardcode this index
			}

			var externalQueueItem = GetBestGuessExternalQueueItem(droppedFile);
			SearchTab.ExternalFileShowAddDialog(externalQueueItem);
		}
	}

	public QueueItem GetBestGuessExternalQueueItem(string externalFilePath)
	{
		var returnItem = new QueueItem 
		{
			PerformanceLink = externalFilePath,
			CreatorName = "(drag-and-drop)",
			ItemType = Path.GetExtension(externalFilePath).ToLower() switch
			{
				".zip" => ItemType.LocalMp3GZip,
				".cdg" => ItemType.LocalMp3G,
				".mp3" => ItemType.LocalMp3G,
				".mp4" => ItemType.LocalMp4,
				_ => throw new NotImplementedException()
			}
		};

		// Parse filename for metadata
		var components = Path.GetFileNameWithoutExtension(externalFilePath).Split(" - ");
		switch (components.Length)
		{
			case 1:
				returnItem.SongName = components[0];
				break;
			case 2:
				returnItem.ArtistName = components[0];
				returnItem.SongName = components[1];
				break;
			case 3:
				returnItem.Identifier = components[0];
				returnItem.ArtistName = components[1];
				returnItem.SongName = components[2];
				break;
			case 4:
				returnItem.CreatorName = components[0];
				returnItem.Identifier = components[1];
				returnItem.ArtistName = components[2];
				returnItem.SongName = components[3];
				break;
			default:
				throw new NotImplementedException();
		}

		return returnItem;
	}

	public void ShowMessageDialog(string title, string message)
	{
        Callable.From(() => {
            MessageDialog.DialogText = message;
            MessageDialog.Title = title;
            MessageDialog.Show();
        }).CallDeferred();
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
			BrowserProvider.CloseControlledBrowser()
		};
		
		await Task.WhenAll(cleanupTasks);
		GetTree().Quit();
	}

	public async void OnProcess(double delta)
	{
		if (!_queueManagement.IsPaused && _queueManagement.NowPlaying == null)
		{
			if (_queueManagement.QueueCount > 0)
			{
				GD.Print($"Queue has {_queueManagement.QueueCount} items, playing next.");
				var nextItem = _queueManagement.GetNextInQueue();
				_ = Task.Run(async () => 
				{
					var cancellationToken = _queueManagement.GetPlaybackCancellationToken();
					await _playbackCoordination.PlayItemAsync(nextItem, cancellationToken);
				});
			}
			else if (!DisplayScreen.Visible && !DisplayScreen.IsDismissed)
			{
				GD.Print("Queue is empty, showing empty queue screen.");
				ShowEmptyQueueScreenAndBgMusic();
			}
		}
	}

	#region Queue UI Management

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

		_queueRoot = QueueTree.CreateItem();

		QueueTree.GuiInput += QueueTreeGuiInput;
		QueueTree.Reorder += QueueTreeReorder;
	}

	private void SetupMainQueueControls()
	{
		MainQueuePlayPauseButton = GetNode<Button>($"%{nameof(MainQueuePlayPauseButton)}");
		MainQueuePlayPauseButton.Pressed += () =>
		{
			if (_queueManagement.IsPaused)
			{
				_queueManagement.Resume();
				_ = _playbackCoordination.ResumeCurrentPlaybackAsync(_queueManagement.NowPlaying);
			}
			else
			{
				_queueManagement.Pause();
				_ = _playbackCoordination.PauseCurrentPlaybackAsync(_queueManagement.NowPlaying);
			}
		};

		MainQueueSkipButton = GetNode<Button>($"%{nameof(MainQueueSkipButton)}");
		MainQueueSkipButton.Pressed += async () =>
		{
			if (!_queueManagement.IsPaused)
			{
				var nowPlaying = _queueManagement.NowPlaying;
				
				// If Karafun is playing via remote control, send NextRequest
				if (nowPlaying?.ItemType == ItemType.KarafunWeb && KarafunRemoteProvider.IsConnected)
				{
					GD.Print("Skipping Karafun song via remote control");
					await KarafunRemoteProvider.SkipAsync();
				}
				
				_queueManagement.Skip();
				
				// Handle local playback cleanup
				if (nowPlaying?.ItemType is ItemType.LocalMp3G or ItemType.LocalMp3GZip or ItemType.LocalMp4
					|| (nowPlaying?.ItemType == ItemType.Youtube && !string.IsNullOrEmpty(nowPlaying.TemporaryDownloadPath)))
				{
					DisplayScreen.CancelIfPlaying();
					OnPlaybackFinished(nowPlaying);
				}
			}
		};
	}

	private void QueueTreeReorder(string draggedItemMetadata, string targetItemMetadata, int dropSection)
	{
		// Find items in queue by metadata
		var draggedItem = FindQueueItemByMetadata(draggedItemMetadata);
		var targetItem = FindQueueItemByMetadata(targetItemMetadata);
		
		if (draggedItem != null && targetItem != null)
		{
			_queueManagement.ReorderQueue(draggedItem, targetItem, dropSection);
			RefreshQueueTree();
		}
	}

	private QueueItem FindQueueItemByMetadata(string metadata)
	{
		// Check if it's the now playing item
		if (_queueManagement.NowPlaying != null && 
		    _queueManagement.NowPlaying.PerformanceLink == metadata)
		{
			return _queueManagement.NowPlaying;
		}
		
		// Search in the queue
		return _queueManagement.GetQueueItems()
			.FirstOrDefault(item => item.PerformanceLink == metadata);
	}

	private void RefreshQueueTree()
	{
		QueueTree.Clear();
		_queueRoot = QueueTree.CreateItem();
		
		// Add current playing item if any
		if (_queueManagement.NowPlaying != null)
		{
			AddQueueTreeRow(_queueManagement.NowPlaying);
		}
		
		// Add all queued items from the queue service
		foreach (var item in _queueManagement.GetQueueItems())
		{
			AddQueueTreeRow(item);
		}
	}

	public void QueueTreeGuiInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Delete)
		{
			var selectedItem = QueueTree.GetSelected();
			if (selectedItem != null)
			{
				var performanceLink = selectedItem.GetMetadata(0).ToString();
				var singer = selectedItem.GetText(0);
				var itemToRemove = FindQueueItemByMetadata(performanceLink);
				if (itemToRemove != null)
				{
					_queueManagement.RemoveFromQueue(itemToRemove);
				}
			}
		}
	}

	private void AddQueueTreeRow(QueueItem item)
	{
		if (item == null) return;

		if (_queueRoot == null)
		{
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
		if (_queueRoot == null || item == null) return;

		var items = _queueRoot.GetChildren();
		var treeItem = items.FirstOrDefault(i => 
			i.GetMetadata(0).ToString() == item.PerformanceLink && 
			i.GetText(0) == item.SingerName);
			
		if (treeItem != null)
		{
			_queueRoot.RemoveChild(treeItem);
		}
	}

	#endregion

	#region Display Screen Management

	public void BindDisplayScreenControls()
	{
		DisplayScreen.SetMonitorId(Settings.DisplayScreenMonitor);

		DisplayScreen.LocalPlaybackFinished += (wasPlaying) =>
		{
			var nowPlaying = _queueManagement.NowPlaying;
			if (wasPlaying == nowPlaying?.PerformanceLink || wasPlaying == nowPlaying?.TemporaryDownloadPath)
			{
				OnPlaybackFinished(nowPlaying);
			}
		};

		MainWindowProgressSlider.ValueChanged += (value) => 
		{
			_playbackCoordination.SeekCurrentPlayback(_queueManagement.NowPlaying, (long)value);
		};
	}

	public void UpdatePlaybackDuration(long durationMs)
	{
		if (durationMs <= 0) 
		{
			return;
		}

		GD.Print($"Playback duration changed: {durationMs}");
		Callable.From(() => {
			DurationLabel.Text = TimeSpan.FromMilliseconds(durationMs).ToString(@"mm\:ss");
			MainWindowProgressSlider.MaxValue = durationMs;
			MainWindowProgressSlider.Editable = true;
		}).CallDeferred();
	}

	public void UpdatePlaybackProgress(long progressMs)
	{
		if (progressMs <= 0) 
		{
			return;
		}
		Callable.From(() => {
			CurrentTimeLabel.Text = TimeSpan.FromMilliseconds(progressMs).ToString(@"mm\:ss");
			MainWindowProgressSlider.SetValueNoSignal(progressMs);
		}).CallDeferred();
	}

	private void SetProgressSlider(string stateText = null, int maxSeconds = 0, int valueSeconds = 0, bool enableEditing = false)
	{
		ProgressSliderLabel.Text = stateText;
		MainWindowProgressSlider.SetValueNoSignal(valueSeconds);
		MainWindowProgressSlider.MaxValue = maxSeconds;
		MainWindowProgressSlider.Editable = enableEditing;
		CurrentTimeLabel.Text = TimeSpan.FromSeconds(valueSeconds).ToString(@"mm\:ss");
		DurationLabel.Text = TimeSpan.FromSeconds(maxSeconds).ToString(@"mm\:ss");
	}

	public void ShowEmptyQueueScreenAndBgMusic()
	{
		DisplayScreen.ShowEmptyQueueScreen();
		SetProgressSlider("(The queue is empty)");
		if (Settings.BgMusicEnabled)
		{
			_backgroundMusic.FadeIn();
		}
	}

	#endregion

	#region Search Screen Management

	private void BindSearchScreenControls()
	{
		// Search tab events are already bound in BindEvents()
	}

	#endregion

	#region Setup Tab Management

	private void SetupStartTab()
	{
		SetupTab.CountdownLengthChanged += (value) =>
		{
			Settings.CountdownLengthSeconds = (int)value;
			Settings.SaveToDisk(FileWrapper);
		};
		
		SetupTab.DisplayScreenMonitorChanged += (value) =>
		{
			Settings.DisplayScreenMonitor = (int)value;
			Settings.SaveToDisk(FileWrapper);
			DisplayScreen.SetMonitorId(Settings.DisplayScreenMonitor);
			DisplayScreen.ShowDisplayScreen();
			
			// Sync with PrepareDialog monitor spinbox if dialog is visible
			if (PrepareSessionDialog.Visible)
			{
				MonitorSpinBox.Value = Settings.DisplayScreenMonitor;
				Callable.From(() => UpdateMonitorWarning()).CallDeferred();
			}
		};
		
		SetupTab.DisplayScreenDismissed += () => DisplayScreen.Dismiss();
		
		SetupTab.BgMusicItemRemoved += _backgroundMusic.RemoveMusicFile;
		SetupTab.BgMusicItemsAdded += _backgroundMusic.AddMusicFiles;
		SetupTab.BgMusicToggle += _backgroundMusic.SetEnabled;
		SetupTab.BgMusicVolumeChanged += _backgroundMusic.SetVolumePercent;
		
		// Karafun remote control
		SetupTab.KarafunRoomCodeChanged += (roomCode) =>
		{
			Settings.KarafunRoomCode = roomCode;
			Settings.SaveToDisk(FileWrapper);
		};
		
		SetupTab.KarafunConnectRequested += async (roomCode) =>
		{
			if (KarafunRemoteProvider.IsConnected)
			{
				await KarafunRemoteProvider.DisconnectAsync();
			}
			else if (!string.IsNullOrWhiteSpace(roomCode))
			{
				await KarafunRemoteProvider.ConnectAsync(roomCode);
			}
		};
		
		SetupTab.KarafunLaunchWebPlayerRequested += async () =>
		{
			// Launch the Karafun web player in a browser
			await BrowserProvider.LaunchUncontrolledBrowser(Settings, "https://www.karafun.com/web/discover/");
		};
		
		// Listen for Karafun remote connection status changes
		KarafunRemoteProvider.ConnectionStatusChanged += (status) =>
		{
			Callable.From(() => SetupTab.SetKarafunConnectionStatusUIValue(status)).CallDeferred();
		};

		SetupTab.SetBgMusicItemsUIValues(Settings.BgMusicFiles);
		SetupTab.SetBgMusicEnabledUIValue(Settings.BgMusicEnabled);
		SetupTab.SetBgMusicVolumePercentUIValue(Settings.BgMusicVolumePercent);
		SetupTab.SetDisplayScreenMonitorUIValue(Settings.DisplayScreenMonitor);
		SetupTab.SetDisplayScreenMonitorMaxValue(DisplayServer.GetScreenCount() - 1);
		SetupTab.SetCountdownLengthSecondsUIValue(Settings.CountdownLengthSeconds);
		SetupTab.SetKarafunRoomCodeUIValue(Settings.KarafunRoomCode);
		SetupTab.SetKarafunConnectionStatusUIValue(KarafunRemoteProvider.ConnectionStatus);
	}

	#endregion

	#region Monitor Identification

	private void SetupMonitorIdentification()
	{
		var overlayScene = GD.Load<PackedScene>("res://Controls/MonitorIdentificationOverlay.tscn");
		MonitorIdManager.Initialize(this, overlayScene);
	}

	private void OnIdentifyMonitorsPressed()
	{
		MonitorIdManager?.ShowAllMonitors();
	}

	#endregion

	#region Logging

	private void SetupHistoryLogFile()
	{
		var appStoragePath = Path.Combine(Utils.GetAppStoragePath(), "history");
		Directory.CreateDirectory(appStoragePath);
		sessionPlayHistoryFileName = Path.Combine(appStoragePath, $"history_{DateTime.Now:yyyy-MM-dd_HHmm}.log");
		using (FileWrapper.Create(sessionPlayHistoryFileName)) { }
	}

	private void AppendToPlayHistory(QueueItem queueItem)
	{
		var nowPlaying = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{queueItem.SingerName}|{queueItem.SongName}|{queueItem.ArtistName}|{queueItem.CreatorName}|{queueItem.PerformanceLink}";
		FileWrapper.AppendAllText(sessionPlayHistoryFileName, nowPlaying + "\n");
	}

	#endregion
}
