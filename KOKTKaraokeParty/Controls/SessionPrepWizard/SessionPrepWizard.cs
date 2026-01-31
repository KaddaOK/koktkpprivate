using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface ISessionPrepWizard : IWindow
{
    event Action<WizardState> WizardCompleted;
    event Action WizardCancelled;
    
    void StartWizard(List<QueueItem> savedQueueItems);
}

[Meta(typeof(IAutoNode))]
public partial class SessionPrepWizard : Window, ISessionPrepWizard
{
    public override void _Notification(int what) => this.Notify(what);
    
    public event Action<WizardState> WizardCompleted;
    public event Action WizardCancelled;
    
    #region Dependencies
    
    [Dependency] private Settings Settings => this.DependOn<Settings>();
    [Dependency] private IBrowserProviderNode BrowserProvider => this.DependOn<IBrowserProviderNode>();
    [Dependency] private IYtDlpProviderNode YtDlpProvider => this.DependOn<IYtDlpProviderNode>();
    [Dependency] private IKarafunRemoteProviderNode KarafunRemoteProvider => this.DependOn<IKarafunRemoteProviderNode>();
    [Dependency] private IDisplayScreen DisplayScreen => this.DependOn<IDisplayScreen>();
    [Dependency] private IMonitorIdentificationManager MonitorIdManager => this.DependOn<IMonitorIdentificationManager>();
    
    #endregion
    
    #region Nodes
    
    [Node] private IControl StepAContainer { get; set; }
    [Node] private IControl StepBContainer { get; set; }
    [Node] private IControl StepCContainer { get; set; }
    [Node] private IControl StepDContainer { get; set; }
    [Node] private IControl StepEContainer { get; set; }
    [Node] private IControl StepFContainer { get; set; }
    [Node] private IControl StepGContainer { get; set; }
    
    // Step A nodes
    [Node] private ITree RestoreQueueTree { get; set; }
    [Node] private IButton StartFreshButton { get; set; }
    [Node] private IButton YesExceptFirstButton { get; set; }
    [Node] private IButton YesAllButton { get; set; }
    
    // Step B nodes
    [Node] private ISpinBox MonitorSpinBox { get; set; }
    [Node] private IButton IdentifyMonitorsButton { get; set; }
    [Node] private ILabel MonitorWarningLabel { get; set; }
    [Node] private IButton StepBBackButton { get; set; }
    [Node] private IButton StepBNextButton { get; set; }
    
    // Step C nodes
    [Node] private ICheckBox UseLocalFilesCheckBox { get; set; }
    [Node] private ILabel LocalFilesInfoLabel { get; set; }
    [Node] private ICheckBox UseYouTubeCheckBox { get; set; }
    [Node] private ICheckBox UseKarafunCheckBox { get; set; }
    [Node] private IButton ControlledBrowserRadio { get; set; }
    [Node] private IButton InstalledAppRadio { get; set; }
    [Node] private IControl KarafunModeContainer { get; set; }
    [Node] private IButton StepCBackButton { get; set; }
    [Node] private IButton StepCNextButton { get; set; }
    
    // Step D nodes
    [Node] private ITree PrepareSessionTree { get; set; }
    [Node] private ILabel StepDStatusLabel { get; set; }
    [Node] private IButton StepDBackButton { get; set; }
    [Node] private IButton StepDNextButton { get; set; }
    
    // Step E nodes
    [Node] private IButton LaunchKarafunWebPlayerButton { get; set; }
    [Node] private ILabel StepEInstructionsLabel { get; set; }
    [Node] private IButton StepEBackButton { get; set; }
    [Node] private IButton StepENextButton { get; set; }
    
    // Step F nodes
    [Node] private ILineEdit RoomCodeEdit { get; set; }
    [Node] private IButton ConnectButton { get; set; }
    [Node] private ILabel ConnectionStatusLabel { get; set; }
    [Node] private IButton StepFBackButton { get; set; }
    [Node] private IButton StepFNextButton { get; set; }
    
    // Step G nodes
    [Node] private ILabel StepGInstructionsLabel { get; set; }
    [Node] private IButton StepGBackButton { get; set; }
    [Node] private IButton StepGFinishButton { get; set; }
    
    // Global
    [Node] private IButton QuitButton { get; set; }
    
    #endregion
    
    private WizardState _state;
    private WizardStep _currentStep;
    private bool _karafunRemoteEventSubscribed = false;
    
    private enum WizardStep
    {
        A_RestoreQueue,
        B_SetDisplay,
        C_SelectServices,
        D_PrepareSession,
        E_LaunchKarafun,
        F_RemoteControl,
        G_PositionDisplay
    }
    
    public void OnReady()
    {
        this.Provide();
        
        _state = new WizardState();
        
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        // Step A
        StartFreshButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.StartFresh);
        YesExceptFirstButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.YesExceptFirst);
        YesAllButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.YesAll);
        
        // Step B
        MonitorSpinBox.ValueChanged += OnMonitorChanged;
        IdentifyMonitorsButton.Pressed += OnIdentifyMonitors;
        StepBBackButton.Pressed += () => NavigateBack();
        StepBNextButton.Pressed += () => NavigateNext();
        
        // Step C
        UseLocalFilesCheckBox.Toggled += OnServiceCheckboxToggled;
        UseYouTubeCheckBox.Toggled += OnServiceCheckboxToggled;
        UseKarafunCheckBox.Toggled += OnKarafunCheckboxToggled;
        ControlledBrowserRadio.Pressed += () => OnKarafunModeChanged(KarafunMode.ControlledBrowser);
        InstalledAppRadio.Pressed += () => OnKarafunModeChanged(KarafunMode.InstalledApp);
        StepCBackButton.Pressed += () => NavigateBack();
        StepCNextButton.Pressed += () => NavigateNext();
        
        // Step D
        StepDBackButton.Pressed += () => NavigateBack();
        StepDNextButton.Pressed += () => NavigateNext();
        
        // Step E
        LaunchKarafunWebPlayerButton.Pressed += OnLaunchKarafunWebPlayer;
        StepEBackButton.Pressed += () => NavigateBack();
        StepENextButton.Pressed += () => NavigateNext();
        
        // Step F
        ConnectButton.Pressed += OnConnectRemote;
        RoomCodeEdit.TextChanged += OnRoomCodeChanged;
        StepFBackButton.Pressed += () => NavigateBack();
        StepFNextButton.Pressed += () => NavigateNext();
        
        // Step G
        StepGBackButton.Pressed += () => NavigateBack();
        StepGFinishButton.Pressed += OnFinish;
        
        // Global
        QuitButton.Pressed += OnQuit;
        CloseRequested += OnQuit;
        
        // Note: KarafunRemoteProvider event subscription is deferred to SetupStepF
        // because the provider may not have called Provide() yet at this point
    }
    
    public void StartWizard(List<QueueItem> savedQueueItems)
    {
        // Initialize state from settings
        _state = new WizardState
        {
            SelectedMonitor = Settings.DisplayScreenMonitor,
            AvailableMonitorCount = DisplayServer.GetScreenCount(),
            UseLocalFiles = Settings.UseLocalFiles,
            UseYouTube = Settings.UseYouTube,
            UseKarafun = Settings.UseKarafun,
            KarafunMode = Settings.KarafunMode,
            KarafunRoomCode = Settings.KarafunRoomCode ?? ""
        };
        
        // Check saved queue
        if (savedQueueItems != null && savedQueueItems.Count > 0)
        {
            _state.HasSavedQueue = true;
            _state.SavedQueueItems = savedQueueItems.Select(q => new SavedQueueItemInfo
            {
                SingerName = q.SingerName,
                SongName = q.SongName,
                ArtistName = q.ArtistName,
                ItemType = q.ItemType
            }).ToList();
        }
        
        // Get local database stats
        try
        {
            using var dbContext = new KOKTDbContext();
            _state.LocalSongCount = dbContext.LocalSongFiles.Count();
            _state.LocalArtistCount = dbContext.LocalSongFiles.Select(f => f.ArtistName).Distinct().Count();
            _state.LocalPathCount = dbContext.LocalScanPaths.Count();
        }
        catch
        {
            _state.LocalSongCount = 0;
            _state.LocalArtistCount = 0;
            _state.LocalPathCount = 0;
        }
        
        // Determine starting step
        if (WizardLogic.ShouldShowRestoreQueueStep(_state))
        {
            ShowStep(WizardStep.A_RestoreQueue);
        }
        else
        {
            ShowStep(WizardStep.B_SetDisplay);
        }
        
        Show();
    }
    
    private void ShowStep(WizardStep step)
    {
        _currentStep = step;
        
        // Hide all step containers
        StepAContainer.Visible = false;
        StepBContainer.Visible = false;
        StepCContainer.Visible = false;
        StepDContainer.Visible = false;
        StepEContainer.Visible = false;
        StepFContainer.Visible = false;
        StepGContainer.Visible = false;
        
        switch (step)
        {
            case WizardStep.A_RestoreQueue:
                SetupStepA();
                StepAContainer.Visible = true;
                Title = "Restore Queue?";
                break;
            case WizardStep.B_SetDisplay:
                SetupStepB();
                StepBContainer.Visible = true;
                Title = "Set Display";
                break;
            case WizardStep.C_SelectServices:
                SetupStepC();
                StepCContainer.Visible = true;
                Title = "Select Services";
                break;
            case WizardStep.D_PrepareSession:
                SetupStepD();
                StepDContainer.Visible = true;
                Title = "Prepare Session";
                break;
            case WizardStep.E_LaunchKarafun:
                SetupStepE();
                StepEContainer.Visible = true;
                Title = "Launch Karafun";
                break;
            case WizardStep.F_RemoteControl:
                SetupStepF();
                StepFContainer.Visible = true;
                Title = "Karafun Remote Control";
                break;
            case WizardStep.G_PositionDisplay:
                SetupStepG();
                StepGContainer.Visible = true;
                Title = "Position Display";
                break;
        }
    }
    
    #region Step A: Restore Queue
    
    private void SetupStepA()
    {
        RestoreQueueTree.Clear();
        RestoreQueueTree.Columns = 3;
        RestoreQueueTree.SetColumnTitle(0, "Singer");
        RestoreQueueTree.SetColumnTitle(1, "Song");
        RestoreQueueTree.SetColumnTitle(2, "Artist");
        RestoreQueueTree.SetColumnTitlesVisible(true);
        
        var root = RestoreQueueTree.CreateItem();
        
        foreach (var item in _state.SavedQueueItems)
        {
            var treeItem = RestoreQueueTree.CreateItem(root);
            treeItem.SetText(0, item.SingerName ?? "");
            treeItem.SetText(1, item.SongName ?? "");
            treeItem.SetText(2, item.ArtistName ?? "");
        }
        
        // Disable "Yes, except first" if only one item
        YesExceptFirstButton.Disabled = _state.SavedQueueItems.Count <= 1;
    }
    
    private void OnQueueRestoreChoice(QueueRestoreOption choice)
    {
        _state.QueueRestoreChoice = choice;
        
        // Determine required services from queue
        var required = WizardLogic.GetRequiredServicesFromQueue(_state.SavedQueueItems, choice);
        _state.LocalFilesRequiredByQueue = required.RequiresLocalFiles;
        _state.YouTubeRequiredByQueue = required.RequiresYouTube;
        _state.KarafunRequiredByQueue = required.RequiresKarafun;
        
        // Force-enable services required by queue
        if (required.RequiresLocalFiles) _state.UseLocalFiles = true;
        if (required.RequiresYouTube) _state.UseYouTube = true;
        if (required.RequiresKarafun) _state.UseKarafun = true;
        
        ShowStep(WizardStep.B_SetDisplay);
    }
    
    #endregion
    
    #region Step B: Set Display
    
    private void SetupStepB()
    {
        MonitorSpinBox.MinValue = 0;
        MonitorSpinBox.MaxValue = Math.Max(0, _state.AvailableMonitorCount - 1);
        MonitorSpinBox.Value = _state.SelectedMonitor;
        
        // Hide back button if Step A wasn't shown (nothing to go back to)
        StepBBackButton.Visible = _state.HasSavedQueue;
        
        UpdateMonitorWarning();
    }
    
    private void OnMonitorChanged(double value)
    {
        _state.SelectedMonitor = (int)value;
        Settings.DisplayScreenMonitor = _state.SelectedMonitor;
        Settings.SaveToDisk(new FileWrapper());
        
        Callable.From(() => DisplayScreen.SetMonitorId(_state.SelectedMonitor)).CallDeferred();
        UpdateMonitorWarning();
    }
    
    private void OnIdentifyMonitors()
    {
        MonitorIdManager.ShowAllMonitors();
    }
    
    private void UpdateMonitorWarning()
    {
        var singleMonitor = _state.AvailableMonitorCount <= 1;
        MonitorWarningLabel.Visible = singleMonitor;
        MonitorWarningLabel.TooltipText = singleMonitor 
            ? "Warning: Only one monitor detected. The display screen will cover this window."
            : "";
    }
    
    #endregion
    
    #region Step C: Select Services
    
    private void SetupStepC()
    {
        // Set checkbox states
        UseLocalFilesCheckBox.ButtonPressed = _state.UseLocalFiles;
        UseYouTubeCheckBox.ButtonPressed = _state.UseYouTube;
        UseKarafunCheckBox.ButtonPressed = _state.UseKarafun;
        
        // Disable checkboxes for services required by restored queue
        UseLocalFilesCheckBox.Disabled = _state.LocalFilesRequiredByQueue;
        UseYouTubeCheckBox.Disabled = _state.YouTubeRequiredByQueue;
        UseKarafunCheckBox.Disabled = _state.KarafunRequiredByQueue;
        
        // Setup Karafun mode radio buttons
        ControlledBrowserRadio.ButtonPressed = _state.KarafunMode == KarafunMode.ControlledBrowser;
        InstalledAppRadio.ButtonPressed = _state.KarafunMode == KarafunMode.InstalledApp;
        KarafunModeContainer.Visible = _state.UseKarafun;
        
        // Update local files info label
        if (_state.LocalSongCount > 0)
        {
            var songWord = _state.LocalSongCount == 1 ? "song" : "songs";
            var artistWord = _state.LocalArtistCount == 1 ? "artist" : "artists";
            var pathWord = _state.LocalPathCount == 1 ? "path" : "paths";
            LocalFilesInfoLabel.Text = $"{_state.LocalSongCount} {songWord} by {_state.LocalArtistCount} {artistWord} across {_state.LocalPathCount} file {pathWord}";
        }
        else
        {
            LocalFilesInfoLabel.Text = "No files scanned yet. Do this on the Local Files tab once the app has started.";
        }
        
        UpdateStepCNextButton();
    }
    
    private void OnServiceCheckboxToggled(bool pressed)
    {
        _state.UseLocalFiles = UseLocalFilesCheckBox.ButtonPressed;
        _state.UseYouTube = UseYouTubeCheckBox.ButtonPressed;
        
        // Save to settings
        Settings.UseLocalFiles = _state.UseLocalFiles;
        Settings.UseYouTube = _state.UseYouTube;
        Settings.SaveToDisk(new FileWrapper());
        
        UpdateStepCNextButton();
    }
    
    private void OnKarafunCheckboxToggled(bool pressed)
    {
        _state.UseKarafun = UseKarafunCheckBox.ButtonPressed;
        KarafunModeContainer.Visible = _state.UseKarafun;
        
        Settings.UseKarafun = _state.UseKarafun;
        Settings.SaveToDisk(new FileWrapper());
        
        UpdateStepCNextButton();
    }
    
    private void OnKarafunModeChanged(KarafunMode mode)
    {
        _state.KarafunMode = mode;
        ControlledBrowserRadio.ButtonPressed = mode == KarafunMode.ControlledBrowser;
        InstalledAppRadio.ButtonPressed = mode == KarafunMode.InstalledApp;
        
        Settings.KarafunMode = mode;
        Settings.SaveToDisk(new FileWrapper());
    }
    
    private void UpdateStepCNextButton()
    {
        StepCNextButton.Disabled = !WizardLogic.IsStepCNextEnabled(_state);
    }
    
    #endregion
    
    #region Step D: Prepare Session
    
    private async void SetupStepD()
    {
        StepDNextButton.Disabled = true;
        PrepareSessionTree.Clear();
        
        PrepareSessionTree.Columns = 2;
        PrepareSessionTree.SetColumnTitle(0, "Service");
        PrepareSessionTree.SetColumnTitle(1, "Status");
        PrepareSessionTree.SetColumnTitlesVisible(true);
        
        var root = PrepareSessionTree.CreateItem();
        
        // Start required checks
        var tasks = new List<Task>();
        
        if (WizardLogic.NeedsVlcPrepare(_state))
        {
            var vlcItem = PrepareSessionTree.CreateItem(root);
            vlcItem.SetText(0, "VLC Libraries");
            vlcItem.SetText(1, "⏳ Initializing...");
            tasks.Add(PrepareVlcAsync(vlcItem));
        }
        
        if (WizardLogic.NeedsYtDlpPrepare(_state))
        {
            var ytdlpItem = PrepareSessionTree.CreateItem(root);
            ytdlpItem.SetText(0, "yt-dlp");
            ytdlpItem.SetText(1, "⏳ Checking...");
            tasks.Add(PrepareYtDlpAsync(ytdlpItem));
        }
        
        if (WizardLogic.NeedsBrowserCheck(_state))
        {
            var browserItem = PrepareSessionTree.CreateItem(root);
            browserItem.SetText(0, "Browser");
            browserItem.SetText(1, "⏳ Checking...");
            tasks.Add(PrepareBrowserAsync(browserItem));
        }
        
        await Task.WhenAll(tasks);
        
        // Enable next button and show appropriate message
        StepDNextButton.Disabled = false;
        if (WizardLogic.CanAutoAdvanceFromStepD(_state))
        {
            StepDStatusLabel.Text = "All checks passed! Click Next to continue.";
        }
        else
        {
            StepDStatusLabel.Text = "Some checks need attention. Click Next to continue anyway, or Back to change services.";
        }
    }
    
    private async Task PrepareVlcAsync(TreeItem treeItem)
    {
        try
        {
            await DisplayScreen.InitializeVlc();
            _state.VlcReady = true;
            _state.VlcMessage = "Ready";
            Callable.From(() => treeItem.SetText(1, "✔ Ready")).CallDeferred();
        }
        catch (Exception ex)
        {
            _state.VlcReady = false;
            _state.VlcMessage = ex.Message;
            Callable.From(() => treeItem.SetText(1, $"❌ {ex.Message}")).CallDeferred();
        }
    }
    
    private async Task PrepareYtDlpAsync(TreeItem treeItem)
    {
        try
        {
            await YtDlpProvider.CheckStatus();
            _state.YtDlpReady = true;
            _state.YtDlpMessage = "Ready";
            Callable.From(() => treeItem.SetText(1, "✔ Ready")).CallDeferred();
        }
        catch (Exception ex)
        {
            _state.YtDlpReady = false;
            _state.YtDlpMessage = ex.Message;
            Callable.From(() => treeItem.SetText(1, $"❌ {ex.Message}")).CallDeferred();
        }
    }
    
    private async Task PrepareBrowserAsync(TreeItem treeItem)
    {
        try
        {
            // Subscribe to browser status updates
            var tcs = new TaskCompletionSource<bool>();
            
            void OnStatusChecked(StatusCheckResult<BrowserAvailabilityStatus> result)
            {
                _state.BrowserReady = result.StatusResult == BrowserAvailabilityStatus.Ready;
                _state.BrowserMessage = result.Message;
                
                var statusText = result.StatusResult switch
                {
                    BrowserAvailabilityStatus.Ready => "✔ Ready",
                    BrowserAvailabilityStatus.Downloading => "⏳ Downloading...",
                    _ => $"❌ {result.Message}"
                };
                Callable.From(() => treeItem.SetText(1, statusText)).CallDeferred();
                
                if (result.StatusResult != BrowserAvailabilityStatus.Checking && 
                    result.StatusResult != BrowserAvailabilityStatus.Downloading)
                {
                    tcs.TrySetResult(_state.BrowserReady);
                }
            }
            
            BrowserProvider.BrowserAvailabilityStatusChecked += OnStatusChecked;
            // We only care about browser availability here, so just wait for the event
            await BrowserProvider.CheckStatus(checkYouTube: false, checkKarafun: false);
            
            await tcs.Task;
            BrowserProvider.BrowserAvailabilityStatusChecked -= OnStatusChecked;
        }
        catch (Exception ex)
        {
            _state.BrowserReady = false;
            _state.BrowserMessage = ex.Message;
            Callable.From(() => treeItem.SetText(1, $"❌ {ex.Message}")).CallDeferred();
        }
    }
    
    #endregion
    
    #region Step E: Launch Karafun
    
    private void SetupStepE()
    {
        if (_state.KarafunMode == KarafunMode.ControlledBrowser)
        {
            LaunchKarafunWebPlayerButton.Visible = true;
            StepEInstructionsLabel.Text = 
                "Click the button below to launch the Karafun web player in a controlled browser.\n\n" +
                "• You may need to log in if you haven't already\n" +
                "• If songs are already in the Karafun queue, you should skip them";
        }
        else
        {
            LaunchKarafunWebPlayerButton.Visible = false;
            StepEInstructionsLabel.Text = 
                "Please launch your installed Karafun application manually.\n\n" +
                "• Log in if you haven't already\n" +
                "• If songs are already in the Karafun queue, you should skip them\n\n" +
                "Click Next when you have the Karafun app running.";
        }
        
        UpdateStepENextButton();
    }
    
    private async void OnLaunchKarafunWebPlayer()
    {
        LaunchKarafunWebPlayerButton.Disabled = true;
        LaunchKarafunWebPlayerButton.Text = "Launching...";
        
        try
        {
            await BrowserProvider.LaunchKarafunWebPlayer(Settings);
            _state.KarafunWebPlayerLaunched = true;
            LaunchKarafunWebPlayerButton.Text = "✔ Launched";
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to launch Karafun web player: {ex.Message}");
            LaunchKarafunWebPlayerButton.Disabled = false;
            LaunchKarafunWebPlayerButton.Text = "Launch Karafun Web Player";
        }
        
        UpdateStepENextButton();
    }
    
    private void UpdateStepENextButton()
    {
        StepENextButton.Disabled = !WizardLogic.IsStepENextEnabled(_state);
    }
    
    #endregion
    
    #region Step F: Remote Control
    
    private void SetupStepF()
    {
        // Subscribe to remote provider events (deferred from OnReady)
        if (!_karafunRemoteEventSubscribed)
        {
            KarafunRemoteProvider.ConnectionStatusChanged += OnKarafunConnectionStatusChanged;
            _karafunRemoteEventSubscribed = true;
        }
        
        RoomCodeEdit.Text = _state.KarafunRoomCode;
        ConnectionStatusLabel.Text = _state.KarafunRemoteConnected 
            ? "✔ Connected" 
            : "Not connected";
        
        UpdateStepFNextButton();
    }
    
    private void OnRoomCodeChanged(string newText)
    {
        _state.KarafunRoomCode = newText;
        Settings.KarafunRoomCode = newText;
        Settings.SaveToDisk(new FileWrapper());
        
        ConnectButton.Disabled = !WizardLogic.IsValidRoomCodeFormat(newText);
    }
    
    private async void OnConnectRemote()
    {
        if (!WizardLogic.IsValidRoomCodeFormat(_state.KarafunRoomCode))
        {
            ConnectionStatusLabel.Text = "Invalid room code format (must be 6 digits)";
            return;
        }
        
        ConnectButton.Disabled = true;
        ConnectionStatusLabel.Text = "⏳ Connecting...";
        
        var success = await KarafunRemoteProvider.ConnectAsync(_state.KarafunRoomCode);
        
        if (success)
        {
            _state.KarafunRemoteConnected = true;
            ConnectionStatusLabel.Text = "✔ Connected";
        }
        else
        {
            ConnectionStatusLabel.Text = "❌ Connection failed";
            ConnectButton.Disabled = false;
        }
        
        UpdateStepFNextButton();
    }
    
    private void OnKarafunConnectionStatusChanged(KarafunRemoteConnectionStatus status)
    {
        Callable.From(() =>
        {
            _state.KarafunRemoteConnected = status == KarafunRemoteConnectionStatus.Connected;
            _state.KarafunRemoteMessage = status.ToString();
            
            if (_currentStep == WizardStep.F_RemoteControl)
            {
                ConnectionStatusLabel.Text = status switch
                {
                    KarafunRemoteConnectionStatus.Connected => "✔ Connected",
                    KarafunRemoteConnectionStatus.Connecting => "⏳ Connecting...",
                    KarafunRemoteConnectionStatus.FetchingSettings => "⏳ Fetching settings...",
                    KarafunRemoteConnectionStatus.Reconnecting => "⏳ Reconnecting...",
                    KarafunRemoteConnectionStatus.Error => "❌ Connection error",
                    _ => "Not connected"
                };
                
                UpdateStepFNextButton();
            }
        }).CallDeferred();
    }
    
    private void UpdateStepFNextButton()
    {
        StepFNextButton.Disabled = !WizardLogic.IsStepFNextEnabled(_state);
    }
    
    #endregion
    
    #region Step G: Position Display
    
    private void SetupStepG()
    {
        var instructions = "Now position your Karafun display:\n\n";
        instructions += "1. In Karafun, open the dual-screen display\n";
        instructions += "   (View menu → Dual Screen Display)\n\n";
        instructions += "2. Drag the display window to your desired screen\n\n";
        instructions += "3. Maximize the window\n\n";
        
        if (_state.KarafunMode == KarafunMode.ControlledBrowser)
        {
            instructions += "Tip: Press F11 in the browser for better fullscreen.\n\n";
        }
        
        instructions += "The KOKT display will appear on top of it when not playing Karafun songs.";
        
        StepGInstructionsLabel.Text = instructions;
    }
    
    #endregion
    
    #region Navigation
    
    private void NavigateBack()
    {
        switch (_currentStep)
        {
            case WizardStep.B_SetDisplay:
                if (WizardLogic.ShouldShowRestoreQueueStep(_state))
                    ShowStep(WizardStep.A_RestoreQueue);
                break;
            case WizardStep.C_SelectServices:
                ShowStep(WizardStep.B_SetDisplay);
                break;
            case WizardStep.D_PrepareSession:
                ShowStep(WizardStep.C_SelectServices);
                break;
            case WizardStep.E_LaunchKarafun:
                if (WizardLogic.NeedsAnyPreparation(_state))
                    ShowStep(WizardStep.D_PrepareSession);
                else
                    ShowStep(WizardStep.C_SelectServices);
                break;
            case WizardStep.F_RemoteControl:
                ShowStep(WizardStep.E_LaunchKarafun);
                break;
            case WizardStep.G_PositionDisplay:
                ShowStep(WizardStep.F_RemoteControl);
                break;
        }
    }
    
    private void NavigateNext()
    {
        switch (_currentStep)
        {
            case WizardStep.B_SetDisplay:
                ShowStep(WizardStep.C_SelectServices);
                break;
            case WizardStep.C_SelectServices:
                if (WizardLogic.NeedsAnyPreparation(_state))
                    ShowStep(WizardStep.D_PrepareSession);
                else if (WizardLogic.ShouldShowKarafunSteps(_state))
                    ShowStep(WizardStep.E_LaunchKarafun);
                else
                    FinishWizard();
                break;
            case WizardStep.D_PrepareSession:
                if (WizardLogic.ShouldShowKarafunSteps(_state))
                    ShowStep(WizardStep.E_LaunchKarafun);
                else
                    FinishWizard();
                break;
            case WizardStep.E_LaunchKarafun:
                ShowStep(WizardStep.F_RemoteControl);
                break;
            case WizardStep.F_RemoteControl:
                ShowStep(WizardStep.G_PositionDisplay);
                break;
        }
    }
    
    private void OnFinish()
    {
        FinishWizard();
    }
    
    private void FinishWizard()
    {
        Hide();
        WizardCompleted?.Invoke(_state);
    }
    
    private void OnQuit()
    {
        WizardCancelled?.Invoke();
    }
    
    #endregion
}
