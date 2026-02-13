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
    event Action<SessionPrepWizardState> WizardCompleted;
    event Action WizardCancelled;
    
    void StartWizard(List<QueueItem> savedQueueItems);
}

[Meta(typeof(IAutoNode))]
public partial class SessionPrepWizard : Window, ISessionPrepWizard
{
    public override void _Notification(int what) => this.Notify(what);
    
    public event Action<SessionPrepWizardState> WizardCompleted;
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

    [Node] private IStep1RestoreQueue Step1RestoreQueue { get; set; }
    [Node] private IStep2SetDisplay Step2SetDisplay { get; set; }
    [Node] private IStep3SelectServices Step3SelectServices { get; set; }
    [Node] private IStep4PrepareSession Step4PrepareSession { get; set; }
    [Node] private IStep5LaunchKarafun Step5LaunchKarafun { get; set; }
    [Node] private IStep6KarafunRemoteControl Step6KarafunRemoteControl { get; set; }
    [Node] private IStep7PositionKarafunDisplay Step7PositionKarafunDisplay { get; set; }
    
    // Global
    [Node] private IButton QuitButton { get; set; }
    
    #endregion
    
    private SessionPrepWizardState _state;
    private SessionPrepWizardStep _currentStep;
    
    private enum SessionPrepWizardStep
    {
        Step1RestoreQueue,
        Step2_SetDisplay,
        Step3_SelectServices,
        Step4_PrepareSession,
        Step5_LaunchKarafun,
        Step6_KarafunRemoteControl,
        Step7_PositionKarafunDisplay
    }
    
    public void OnReady()
    {
        this.Provide();
        
        _state = new SessionPrepWizardState();
        
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        // Step 1 Restore Queue
        Step1RestoreQueue.RestoreChoiceSelected += OnQueueRestoreChoice;
        
        // Step 2 Set Display
        Step2SetDisplay.BackPressed += () => NavigateBack();
        Step2SetDisplay.NextPressed += () => NavigateNext();
        
        // Step 3 Select Services
        Step3SelectServices.BackPressed += () => NavigateBack();
        Step3SelectServices.NextPressed += () => NavigateNext();
        
        // Step 4 Prepare Session
        Step4PrepareSession.BackPressed += () => NavigateBack();
        Step4PrepareSession.NextPressed += () => NavigateNext();
        
        // Step 5 Launch Karafun
        Step5LaunchKarafun.BackPressed += () => NavigateBack();
        Step5LaunchKarafun.NextPressed += () => NavigateNext();
        
        // Step 6 Karafun Remote Control
        Step6KarafunRemoteControl.BackPressed += () => NavigateBack();
        Step6KarafunRemoteControl.NextPressed += () => NavigateNext();
        
        // Step 7 Position Karafun Display
        Step7PositionKarafunDisplay.BackPressed += () => NavigateBack();
        Step7PositionKarafunDisplay.FinishPressed += OnFinish;
        
        // Global
        QuitButton.Pressed += OnQuit;
        CloseRequested += OnQuit;
    }
    
    public void StartWizard(List<QueueItem> savedQueueItems)
    {
        // Initialize state from settings
        _state = new SessionPrepWizardState
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
        if (SessionPrepWizardLogic.ShouldShowRestoreQueueStep(_state))
        {
            ShowStep(SessionPrepWizardStep.Step1RestoreQueue);
        }
        else
        {
            ShowStep(SessionPrepWizardStep.Step2_SetDisplay);
        }
        
        Show();
    }
    
    private void ShowStep(SessionPrepWizardStep step)
    {
        _currentStep = step;
        
        // Hide all step containers
        Step1RestoreQueue.Visible = false;
        Step2SetDisplay.Visible = false;
        Step3SelectServices.Visible = false;
        Step4PrepareSession.Visible = false;
        Step5LaunchKarafun.Visible = false;
        Step6KarafunRemoteControl.Visible = false;
        Step7PositionKarafunDisplay.Visible = false;
        
        switch (step)
        {
            case SessionPrepWizardStep.Step1RestoreQueue:
                Step1RestoreQueue.Setup(_state);
                Step1RestoreQueue.Visible = true;
                Title = "Restore Queue?";
                break;
            case SessionPrepWizardStep.Step2_SetDisplay:
                Step2SetDisplay.Setup(_state, DisplayScreen, MonitorIdManager, Settings);
                Step2SetDisplay.Visible = true;
                Title = "Setup Step: Set Display Screen";
                break;
            case SessionPrepWizardStep.Step3_SelectServices:
                Step3SelectServices.Setup(_state, Settings);
                Step3SelectServices.Visible = true;
                Title = "Setup Step: Select Services";
                break;
            case SessionPrepWizardStep.Step4_PrepareSession:
                SetupStep4PrepareSessionAsync();
                Step4PrepareSession.Visible = true;
                Title = "Setup Step: Prepare Session";
                break;
            case SessionPrepWizardStep.Step5_LaunchKarafun:
                Step5LaunchKarafun.Setup(_state, BrowserProvider, Settings);
                Step5LaunchKarafun.Visible = true;
                Title = "Setup Step: Launch Karafun";
                break;
            case SessionPrepWizardStep.Step6_KarafunRemoteControl:
                Step6KarafunRemoteControl.Setup(_state, KarafunRemoteProvider, Settings);
                Step6KarafunRemoteControl.Visible = true;
                Title = "Setup Step: Karafun Remote Control";
                break;
            case SessionPrepWizardStep.Step7_PositionKarafunDisplay:
                Step7PositionKarafunDisplay.Setup(_state);
                Step7PositionKarafunDisplay.Visible = true;
                Title = "Setup Step: Position the Karafun Display";
                break;
        }
    }
    
    #region Step 1: Restore Queue
    
    private void OnQueueRestoreChoice(QueueRestoreOption choice)
    {
        _state.QueueRestoreChoice = choice;
        
        // Determine required services from queue
        var required = SessionPrepWizardLogic.GetRequiredServicesFromQueue(_state.SavedQueueItems, choice);
        _state.LocalFilesRequiredByQueue = required.RequiresLocalFiles;
        _state.YouTubeRequiredByQueue = required.RequiresYouTube;
        _state.KarafunRequiredByQueue = required.RequiresKarafun;
        
        // Force-enable services required by queue
        if (required.RequiresLocalFiles) _state.UseLocalFiles = true;
        if (required.RequiresYouTube) _state.UseYouTube = true;
        if (required.RequiresKarafun) _state.UseKarafun = true;
        
        ShowStep(SessionPrepWizardStep.Step2_SetDisplay);
    }
    
    #endregion
    
    #region Step 4: Async Setup
    
    private async void SetupStep4PrepareSessionAsync()
    {
        await Step4PrepareSession.SetupAsync(_state, DisplayScreen, YtDlpProvider, BrowserProvider);
    }
    
    #endregion
    
    #region Navigation
    
    private void NavigateBack()
    {
        switch (_currentStep)
        {
            case SessionPrepWizardStep.Step2_SetDisplay:
                if (SessionPrepWizardLogic.ShouldShowRestoreQueueStep(_state))
                    ShowStep(SessionPrepWizardStep.Step1RestoreQueue);
                break;
            case SessionPrepWizardStep.Step3_SelectServices:
                ShowStep(SessionPrepWizardStep.Step2_SetDisplay);
                break;
            case SessionPrepWizardStep.Step4_PrepareSession:
                ShowStep(SessionPrepWizardStep.Step3_SelectServices);
                break;
            case SessionPrepWizardStep.Step5_LaunchKarafun:
                if (SessionPrepWizardLogic.NeedsAnyPreparation(_state))
                    ShowStep(SessionPrepWizardStep.Step4_PrepareSession);
                else
                    ShowStep(SessionPrepWizardStep.Step3_SelectServices);
                break;
            case SessionPrepWizardStep.Step6_KarafunRemoteControl:
                ShowStep(SessionPrepWizardStep.Step5_LaunchKarafun);
                break;
            case SessionPrepWizardStep.Step7_PositionKarafunDisplay:
                ShowStep(SessionPrepWizardStep.Step6_KarafunRemoteControl);
                break;
        }
    }
    
    private void NavigateNext()
    {
        switch (_currentStep)
        {
            case SessionPrepWizardStep.Step2_SetDisplay:
                ShowStep(SessionPrepWizardStep.Step3_SelectServices);
                break;
            case SessionPrepWizardStep.Step3_SelectServices:
                if (SessionPrepWizardLogic.NeedsAnyPreparation(_state))
                    ShowStep(SessionPrepWizardStep.Step4_PrepareSession);
                else if (SessionPrepWizardLogic.ShouldShowKarafunSteps(_state))
                    ShowStep(SessionPrepWizardStep.Step5_LaunchKarafun);
                else
                    FinishWizard();
                break;
            case SessionPrepWizardStep.Step4_PrepareSession:
                if (SessionPrepWizardLogic.ShouldShowKarafunSteps(_state))
                    ShowStep(SessionPrepWizardStep.Step5_LaunchKarafun);
                else
                    FinishWizard();
                break;
            case SessionPrepWizardStep.Step5_LaunchKarafun:
                ShowStep(SessionPrepWizardStep.Step6_KarafunRemoteControl);
                break;
            case SessionPrepWizardStep.Step6_KarafunRemoteControl:
                ShowStep(SessionPrepWizardStep.Step7_PositionKarafunDisplay);
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
