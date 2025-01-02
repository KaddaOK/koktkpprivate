using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
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

    void SetBgMusicItemsUIValues(List<string> bgMusicFiles);
    void SetBgMusicEnabledUIValue(bool enabled);
    void SetBgMusicVolumePercentUIValue(double volumePercent);
    void SetDisplayScreenMonitorUIValue(int monitor);
    void SetDisplayScreenMonitorMaxValue(int maxMonitor);
    void SetCountdownLengthSecondsUIValue(int countdownLengthSeconds);
}

[Meta(typeof(IAutoNode))]
public partial class SetupTab : MarginContainer, ISetupTab
{
    public override void _Notification(int what) => this.Notify(what);

    #region Dependencies
    [Dependency] public IPuppeteerPlayer PuppeteerPlayer => this.DependOn<IPuppeteerPlayer>();
    #endregion
    
    #region Nodes
    [Node] private Button LaunchUnautomatedButton { get; set; } = default!;
    [Node] private Button LaunchAutomatedButton { get; set; } = default!;
    [Node] private SpinBox MonitorSpinbox { get; set; } = default!;
    [Node] private SpinBox WaitSpinbox { get; set; } = default!;
    [Node] private Button ApplyMonitorButton { get; set; } = default!;
    [Node] private Button HideDisplayScreenButton { get; set; } = default!;
    [Node] private ItemList BgMusicItemList { get; set; } = default!;
    [Node] private FileDialog BgMusicAddFileDialog { get; set; } = default!;
    [Node] private CheckBox BgMusicEnabledCheckBox { get; set; } = default!;
    [Node] private SpinBox BgMusicVolumeSpinBox { get; set; } = default!;
    [Node] private Button BgMusicAddButton { get; set; } = default!;
    #endregion

    #region Signals

    [Signal] public delegate void CountdownLengthChangedEventHandler(int newCountdownLength);
    [Signal] public delegate void DisplayScreenMonitorChangedEventHandler(int newDisplayScreenMonitor);
    [Signal] public delegate void DisplayScreenDismissedEventHandler();
    [Signal] public delegate void BgMusicItemRemovedEventHandler(string pathToRemove);
    [Signal] public delegate void BgMusicItemsAddedEventHandler(string[] pathsToAdd);
    [Signal] public delegate void BgMusicToggleEventHandler(bool enabled);
    [Signal] public delegate void BgMusicVolumeChangedEventHandler(double newVolume);

    #endregion

    public void OnReady()
    {
        GD.Print("SetupTab OnReady did happen.");
        LaunchUnautomatedButton.Pressed += () => PuppeteerPlayer.LaunchUnautomatedBrowser("https://www.karafun.com/my/", "https://www.youtube.com/account");
        LaunchAutomatedButton.Pressed += () => PuppeteerPlayer.LaunchAutomatedBrowser();
        ApplyMonitorButton.Pressed += () => EmitSignal(SignalName.DisplayScreenMonitorChanged, (int)MonitorSpinbox.Value);
        HideDisplayScreenButton.Pressed += () => EmitSignal(SignalName.DisplayScreenDismissed);
        WaitSpinbox.ValueChanged += (value) => EmitSignal(SignalName.CountdownLengthChanged, (int)value);
        BgMusicItemList.GuiInput += BgMusicItemListGuiInput;
        BgMusicEnabledCheckBox.Toggled += (enabled) => EmitSignal(SignalName.BgMusicToggle, enabled);
        BgMusicAddFileDialog.FileSelected += (file) => OnBgMusicFilesSelected([file]);
        BgMusicAddFileDialog.FilesSelected += OnBgMusicFilesSelected;
        BgMusicAddButton.Pressed += () => BgMusicAddFileDialog.Visible = true;
        BgMusicVolumeSpinBox.ValueChanged += (double value) => EmitSignal(SignalName.BgMusicVolumeChanged, value);
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
    }

    public void SetDisplayScreenMonitorMaxValue(int maxMonitor)
    {
        MonitorSpinbox.MaxValue = maxMonitor;
    }

    public void SetCountdownLengthSecondsUIValue(int countdownLengthSeconds)
    {
        WaitSpinbox.Value = countdownLengthSeconds;
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
}
