using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep4PrepareSession : IVBoxContainer
{
    event Action BackPressed;
    event Action NextPressed;
    Task SetupAsync(SessionPrepWizardState state, IDisplayScreen displayScreen, IYtDlpProviderNode ytDlpProvider, IBrowserProviderNode browserProvider);
}

[Meta(typeof(IAutoNode))]
public partial class Step4PrepareSession : VBoxContainer, IStep4PrepareSession
{
	public override void _Notification(int what) => this.Notify(what);
	
	public event Action BackPressed;
	public event Action NextPressed;
	
	#region Nodes
	
	[Node] private ITree PrepareSessionTree { get; set; }
	[Node] private ILabel Step4PrepareSessionStatusLabel { get; set; }
	[Node] private IButton Step4PrepareSessionBackButton { get; set; }
	[Node] private IButton Step4PrepareSessionNextButton { get; set; }
	
	#endregion
	
	private SessionPrepWizardState _state;
	
	public void OnReady()
	{
		Step4PrepareSessionBackButton.Pressed += () => BackPressed?.Invoke();
		Step4PrepareSessionNextButton.Pressed += () => NextPressed?.Invoke();
	}
	
	public async Task SetupAsync(SessionPrepWizardState state, IDisplayScreen displayScreen, IYtDlpProviderNode ytDlpProvider, IBrowserProviderNode browserProvider)
	{
		_state = state;
		
		Step4PrepareSessionNextButton.Disabled = true;
		PrepareSessionTree.Clear();
		
		PrepareSessionTree.Columns = 2;
		PrepareSessionTree.SetColumnTitle(0, "Service");
		PrepareSessionTree.SetColumnTitle(1, "Status");
		PrepareSessionTree.SetColumnTitlesVisible(true);
		
		var root = PrepareSessionTree.CreateItem();
		
		// Start required checks
		var tasks = new List<Task>();
		
		if (SessionPrepWizardLogic.NeedsVlcPrepare(_state))
		{
			var vlcItem = PrepareSessionTree.CreateItem(root);
			vlcItem.SetText(0, "VLC Libraries");
			vlcItem.SetText(1, "⏳ Initializing...");
			tasks.Add(PrepareVlcAsync(vlcItem, displayScreen));
		}
		
		if (SessionPrepWizardLogic.NeedsYtDlpPrepare(_state))
		{
			var ytdlpItem = PrepareSessionTree.CreateItem(root);
			ytdlpItem.SetText(0, "yt-dlp");
			ytdlpItem.SetText(1, "⏳ Checking...");
			tasks.Add(PrepareYtDlpAsync(ytdlpItem, ytDlpProvider));
		}
		
		if (SessionPrepWizardLogic.NeedsBrowserCheck(_state))
		{
			var browserItem = PrepareSessionTree.CreateItem(root);
			browserItem.SetText(0, "Browser");
			browserItem.SetText(1, "⏳ Checking...");
			tasks.Add(PrepareBrowserAsync(browserItem, browserProvider));
		}
		
		await Task.WhenAll(tasks);
		
		// Enable next button and show appropriate message
		Step4PrepareSessionNextButton.Disabled = false;
		if (SessionPrepWizardLogic.CanAutoAdvanceFromStep4PrepareSession(_state))
		{
			Step4PrepareSessionStatusLabel.Text = "All checks passed! Click Next to continue.";
		}
		else
		{
			Step4PrepareSessionStatusLabel.Text = "Some checks need attention. Click Next to continue anyway, or Back to change services.";
		}
	}
	
	private async Task PrepareVlcAsync(TreeItem treeItem, IDisplayScreen displayScreen)
	{
		try
		{
			await displayScreen.InitializeVlc();
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
	
	private async Task PrepareYtDlpAsync(TreeItem treeItem, IYtDlpProviderNode ytDlpProvider)
	{
		try
		{
			await ytDlpProvider.CheckStatus();
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
	
	private async Task PrepareBrowserAsync(TreeItem treeItem, IBrowserProviderNode browserProvider)
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
			
			browserProvider.BrowserAvailabilityStatusChecked += OnStatusChecked;
			// We only care about browser availability here, so just wait for the event
			await browserProvider.CheckStatus(checkYouTube: false, checkKarafun: false);
			
			await tcs.Task;
			browserProvider.BrowserAvailabilityStatusChecked -= OnStatusChecked;
		}
		catch (Exception ex)
		{
			_state.BrowserReady = false;
			_state.BrowserMessage = ex.Message;
			Callable.From(() => treeItem.SetText(1, $"❌ {ex.Message}")).CallDeferred();
		}
	}
}