using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty;
using KOKTKaraokeParty.Services;
using System.Collections.Generic;
using System.Linq;

public interface ISetupTab: IMarginContainer
{
    event SetupTab.CountdownLengthChangedEventHandler CountdownLengthChanged;
    event SetupTab.DisplayScreenMonitorChangedEventHandler DisplayScreenMonitorChanged;
    event SetupTab.DisplayScreenDismissedEventHandler DisplayScreenDismissed;
    event SetupTab.BgMusicItemRemovedEventHandler BgMusicItemRemoved;
    event SetupTab.BgMusicItemsAddedEventHandler BgMusicItemsAdded;
    event SetupTab.BgMusicToggleEventHandler BgMusicToggle;
    event SetupTab.BgMusicVolumeChangedEventHandler BgMusicVolumeChanged;
    event SetupTab.KarafunRoomCodeChangedEventHandler KarafunRoomCodeChanged;
    event SetupTab.KarafunConnectRequestedEventHandler KarafunConnectRequested;
    event SetupTab.KarafunLaunchWebPlayerRequestedEventHandler KarafunLaunchWebPlayerRequested;

    void SetBgMusicItemsUIValues(List<string> bgMusicFiles);
    void SetBgMusicEnabledUIValue(bool enabled);
    void SetBgMusicVolumePercentUIValue(double volumePercent);
    void SetDisplayScreenMonitorUIValue(int monitor);
    void SetDisplayScreenMonitorMaxValue(int maxMonitor);
    void SetCountdownLengthSecondsUIValue(int countdownLengthSeconds);
    void SetKarafunRoomCodeUIValue(string roomCode);
    void SetKarafunConnectionStatusUIValue(KarafunRemoteConnectionStatus status);
}

[Meta(typeof(IAutoNode))]
public partial class SetupTab : MarginContainer, ISetupTab
{
    public override void _Notification(int what) => this.Notify(what);

    #region Dependencies
    [Dependency] public IBrowserProviderNode BrowserProvider => this.DependOn<IBrowserProviderNode>();
    [Dependency] public IMonitorIdentificationManager MonitorIdManager => this.DependOn<IMonitorIdentificationManager>();
    [Dependency] public IYtDlpProviderNode YtDlpProvider => this.DependOn<IYtDlpProviderNode>();
    #endregion

    #region Nodes
    [Node] private Button LaunchUnautomatedButton { get; set; } = default!;
    [Node] private SpinBox MonitorSpinbox { get; set; } = default!;
    [Node] private SpinBox WaitSpinbox { get; set; } = default!;
    [Node] private Button ApplyMonitorButton { get; set; } = default!;
    [Node] private Button HideDisplayScreenButton { get; set; } = default!;
    [Node] private ItemList BgMusicItemList { get; set; } = default!;
    [Node] private FileDialog BgMusicAddFileDialog { get; set; } = default!;
    [Node] private CheckBox BgMusicEnabledCheckBox { get; set; } = default!;
    [Node] private SpinBox BgMusicVolumeSpinBox { get; set; } = default!;
    [Node] private Button BgMusicAddButton { get; set; } = default!;
    [Node] private Button IdentifyMonitorsButton { get; set; } = default!;
    [Node] private Label MonitorWarningLabel { get; set; } = default!;
    [Node] private Button UpdateYtDlpButton { get; set; } = default!;
    [Node] private AcceptDialog UpdateYtDlpDialog { get; set; } = default!;
    [Node] private Label YtDlpStatusLabel { get; set; } = default!;
    [Node] private Label DenoStatusLabel { get; set; } = default!;
    [Node] private LineEdit KarafunRoomCodeLineEdit { get; set; } = default!;
    [Node] private Button KarafunConnectButton { get; set; } = default!;
    [Node] private Label KarafunStatusLabel { get; set; } = default!;
    [Node] private Button LaunchKarafunWebButton { get; set; } = default!;
    #endregion

    #region Signals

    [Signal] public delegate void CountdownLengthChangedEventHandler(int newCountdownLength);
    [Signal] public delegate void DisplayScreenMonitorChangedEventHandler(int newDisplayScreenMonitor);
    [Signal] public delegate void DisplayScreenDismissedEventHandler();
    [Signal] public delegate void BgMusicItemRemovedEventHandler(string pathToRemove);
    [Signal] public delegate void BgMusicItemsAddedEventHandler(string[] pathsToAdd);
    [Signal] public delegate void BgMusicToggleEventHandler(bool enabled);
    [Signal] public delegate void BgMusicVolumeChangedEventHandler(double newVolume);
    [Signal] public delegate void KarafunRoomCodeChangedEventHandler(string roomCode);
    [Signal] public delegate void KarafunConnectRequestedEventHandler(string roomCode);
    [Signal] public delegate void KarafunLaunchWebPlayerRequestedEventHandler();

    #endregion

    public void OnReady()
    {
        LaunchUnautomatedButton.Pressed += () => BrowserProvider.LaunchUncontrolledBrowser("https://www.karafun.com/my/", "https://www.youtube.com/account");
        ApplyMonitorButton.Pressed += () => EmitSignal(SignalName.DisplayScreenMonitorChanged, (int)MonitorSpinbox.Value);
        HideDisplayScreenButton.Pressed += () => EmitSignal(SignalName.DisplayScreenDismissed);
        WaitSpinbox.ValueChanged += (value) => EmitSignal(SignalName.CountdownLengthChanged, (int)value);
        BgMusicItemList.GuiInput += BgMusicItemListGuiInput;
        BgMusicEnabledCheckBox.Toggled += (enabled) => EmitSignal(SignalName.BgMusicToggle, enabled);
        BgMusicAddFileDialog.FileSelected += (file) => OnBgMusicFilesSelected([file]);
        BgMusicAddFileDialog.FilesSelected += OnBgMusicFilesSelected;
        BgMusicAddButton.Pressed += () => BgMusicAddFileDialog.Visible = true;
        BgMusicVolumeSpinBox.ValueChanged += (double value) => EmitSignal(SignalName.BgMusicVolumeChanged, value);
        IdentifyMonitorsButton.Pressed += () => MonitorIdManager.ShowAllMonitors();
        MonitorSpinbox.ValueChanged += (double value) => UpdateMonitorWarning((int)value);
        UpdateYtDlpButton.Pressed += ShowUpdateDialog;
        
        // Karafun remote control
        KarafunRoomCodeLineEdit.TextChanged += (text) => EmitSignal(SignalName.KarafunRoomCodeChanged, text);
        KarafunConnectButton.Pressed += () => EmitSignal(SignalName.KarafunConnectRequested, KarafunRoomCodeLineEdit.Text);
        LaunchKarafunWebButton.Pressed += () => EmitSignal(SignalName.KarafunLaunchWebPlayerRequested);
    }

    private void BgMusicItemListGuiInput(InputEvent @event)
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
                    EmitSignal(SignalName.BgMusicItemRemoved, pathToRemove);
                }
            }
        }
    }

    public void SetBgMusicItemsUIValues(List<string> bgMusicFiles)
    {
        BgMusicItemList.Clear();
        foreach (var bgMusicFile in bgMusicFiles)
        {
            BgMusicItemList.AddItem(bgMusicFile);
        }
    }

    public void SetBgMusicEnabledUIValue(bool enabled)
    {
        BgMusicEnabledCheckBox.SetPressedNoSignal(enabled);
    }

    public void SetBgMusicVolumePercentUIValue(double volumePercent)
    {
        BgMusicVolumeSpinBox.Value = volumePercent;
    }

    public void SetDisplayScreenMonitorUIValue(int monitor)
    {
        MonitorSpinbox.Value = monitor;
        UpdateMonitorWarning(monitor);
    }

    public void SetDisplayScreenMonitorMaxValue(int maxMonitor)
    {
        MonitorSpinbox.MaxValue = maxMonitor;
    }

    public void SetCountdownLengthSecondsUIValue(int countdownLengthSeconds)
    {
        WaitSpinbox.Value = countdownLengthSeconds;
    }

    public void SetKarafunRoomCodeUIValue(string roomCode)
    {
        KarafunRoomCodeLineEdit.Text = roomCode ?? "";
    }

    public void SetKarafunConnectionStatusUIValue(KarafunRemoteConnectionStatus status)
    {
        Callable.From(() => {
            var (icon, tooltip, buttonEnabled) = status switch
            {
                KarafunRemoteConnectionStatus.Disconnected => ("⬛", "Not connected", true),
                KarafunRemoteConnectionStatus.FetchingSettings => ("⏳", "Fetching settings...", false),
                KarafunRemoteConnectionStatus.Connecting => ("⏳", "Connecting...", false),
                KarafunRemoteConnectionStatus.Connected => ("✔", "Connected to Karafun remote control", false),
                KarafunRemoteConnectionStatus.Reconnecting => ("⏳", "Reconnecting...", false),
                KarafunRemoteConnectionStatus.Error => ("❌", "Connection failed", true),
                _ => ("⚠", "Unknown status", true)
            };
            
            KarafunStatusLabel.Text = icon;
            KarafunStatusLabel.TooltipText = tooltip;
            KarafunConnectButton.Disabled = !buttonEnabled;
            KarafunConnectButton.Text = status == KarafunRemoteConnectionStatus.Connected ? "Disconnect" : "Connect";
        }).CallDeferred();
    }

	private void UpdateMonitorWarning(int selectedMonitor)
	{
        Callable.From(() => {
            // Check if the selected monitor is the same as the main window
            var mainWindowScreen = DisplayServer.WindowGetCurrentScreen();
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

    private void OnBgMusicFilesSelected(string[] files)
    {
        BgMusicAddFileDialog.Visible = false;
        foreach (var file in files)
        {
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

        EmitSignal(SignalName.BgMusicItemsAdded, files);
    }

    private async void ShowUpdateDialog()
    {
        // Show dialog immediately
        UpdateYtDlpDialog.Visible = true;

        // Disable OK button until updates complete
        var okButton = UpdateYtDlpDialog.GetOkButton();
        if (okButton != null)
        {
            okButton.Disabled = true;
        }

        // Get current versions
        var currentYtDlpVersion = await YtDlpProvider.GetYtDlpVersion();
        var currentDenoVersion = await YtDlpProvider.GetDenoVersion();

        // Start both updates simultaneously
        var ytDlpUpdateTask = UpdateYtDlp(currentYtDlpVersion);
        var denoUpdateTask = UpdateDeno(currentDenoVersion);

        await System.Threading.Tasks.Task.WhenAll(ytDlpUpdateTask, denoUpdateTask);

        // Re-enable OK button when both complete
        if (okButton != null)
        {
            okButton.Disabled = false;
        }
    }

    private async System.Threading.Tasks.Task UpdateYtDlp(string currentVersion)
    {
        try
        {
            YtDlpStatusLabel.Text = $"⏳ yt-dlp: Currently v{currentVersion}; checking...";

            var latestVersion = await YtDlpProvider.GetLatestYtDlpVersionFromGitHub();
            
            if (latestVersion != null && latestVersion != currentVersion)
            {
                YtDlpStatusLabel.Text = $"⏳ yt-dlp: Currently v{currentVersion}; downloading v{latestVersion}...";
                await YtDlpProvider.ForceUpdateYtDlp();
                var newVersion = await YtDlpProvider.GetYtDlpVersion();
                YtDlpStatusLabel.Text = $"✔ yt-dlp: Updated from v{currentVersion} to v{newVersion}";
            }
            else if (latestVersion == currentVersion)
            {
                YtDlpStatusLabel.Text = $"❎ yt-dlp: Already up to date (v{currentVersion})";
            }
            else
            {
                // Couldn't determine latest version, update anyway
                YtDlpStatusLabel.Text = $"⏳ yt-dlp: Currently v{currentVersion}; downloading latest...";
                await YtDlpProvider.ForceUpdateYtDlp();
                var newVersion = await YtDlpProvider.GetYtDlpVersion();
                if (newVersion != currentVersion)
                {
                    YtDlpStatusLabel.Text = $"✔ yt-dlp: Updated from v{currentVersion} to v{newVersion}";
                }
                else
                {
                    YtDlpStatusLabel.Text = $"❎ yt-dlp: v{newVersion} (no change)";
                }
            }
        }
        catch (System.Exception ex)
        {
            YtDlpStatusLabel.Text = $"yt-dlp: Update failed - {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task UpdateDeno(string currentVersion)
    {
        try
        {
            DenoStatusLabel.Text = $"⏳ deno: Currently v{currentVersion}; checking...";

            var latestVersion = await YtDlpProvider.GetLatestDenoVersionFromGitHub();
            
            if (latestVersion != null && latestVersion != currentVersion)
            {
                DenoStatusLabel.Text = $"⏳ deno: Currently v{currentVersion}; downloading v{latestVersion}...";
                await YtDlpProvider.ForceUpdateDeno();
                var newVersion = await YtDlpProvider.GetDenoVersion();
                DenoStatusLabel.Text = $"✔ deno: Updated from v{currentVersion} to v{newVersion}";
            }
            else if (latestVersion == currentVersion)
            {
                DenoStatusLabel.Text = $"❎ deno: Already up to date (v{currentVersion})";
            }
            else
            {
                // Couldn't determine latest version, update anyway
                DenoStatusLabel.Text = $"⏳ deno: Currently v{currentVersion}; downloading latest...";
                await YtDlpProvider.ForceUpdateDeno();
                var newVersion = await YtDlpProvider.GetDenoVersion();
                if (newVersion != currentVersion)
                {
                    DenoStatusLabel.Text = $"✔ deno: Updated from v{currentVersion} to v{newVersion}";
                }
                else
                {
                    DenoStatusLabel.Text = $"❎ deno: v{newVersion} (no change)";
                }
            }
        }
        catch (System.Exception ex)
        {
            DenoStatusLabel.Text = $"deno: Update failed - {ex.Message}";
        }
    }

    private void PerformUpdate()
    {
        // This is now unused - updates happen automatically in ShowUpdateDialog
        // Keeping for backward compatibility in case it's wired up
    }
}
