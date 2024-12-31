using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SetupTab : MarginContainer
{
	
	#region Nodes
	private Button LaunchUnautomatedButton { get; set; } = default!;
	private Button LaunchAutomatedButton { get; set; } = default!;
	private SpinBox MonitorSpinbox { get; set; } = default!;
	private SpinBox WaitSpinbox { get; set; } = default!;
	private Button ApplyMonitorButton { get; set; } = default!;
	private Button HideDisplayScreenButton { get; set; } = default!;
	private ItemList BgMusicItemList { get; set; } = default!;
	private FileDialog BgMusicAddFileDialog { get; set; } = default!;
	private CheckBox BgMusicEnabledCheckBox { get; set; } = default!;
	private SpinBox BgMusicVolumeSpinBox { get; set; } = default!;
	private Button BgMusicAddButton { get; set; } = default!;
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
	public override void _Ready()
	{
		LaunchUnautomatedButton = GetNode<Button>($"%{nameof(LaunchUnautomatedButton)}");
		LaunchAutomatedButton = GetNode<Button>($"%{nameof(LaunchAutomatedButton)}");
		MonitorSpinbox = GetNode<SpinBox>($"%{nameof(MonitorSpinbox)}");
		WaitSpinbox = GetNode<SpinBox>($"%{nameof(WaitSpinbox)}");
		ApplyMonitorButton = GetNode<Button>($"%{nameof(ApplyMonitorButton)}");
		HideDisplayScreenButton = GetNode<Button>($"%{nameof(HideDisplayScreenButton)}");

		BgMusicItemList = GetNode<ItemList>($"%{nameof(BgMusicItemList)}");
		BgMusicEnabledCheckBox = GetNode<CheckBox>($"%{nameof(BgMusicEnabledCheckBox)}");
		BgMusicVolumeSpinBox = GetNode<SpinBox>($"%{nameof(BgMusicVolumeSpinBox)}");
		BgMusicAddFileDialog = GetNode<FileDialog>($"%{nameof(BgMusicAddFileDialog)}");
		BgMusicAddButton = GetNode<Button>($"%{nameof(BgMusicAddButton)}");

		OnReady();
	}

	private void OnReady()
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
