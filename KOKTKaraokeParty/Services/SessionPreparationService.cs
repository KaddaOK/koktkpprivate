using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;
using System;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Services;

public enum VLCStatus
{
    NotStarted,
    Initializing,
    Ready,
    FatalError
}

public class PrepareSessionModel
{
    public BrowserAvailabilityStatus BrowserStatus { get; set; }
    public string BrowserIdentity { get; set; }
    public string BrowserMessage { get; set; }
    public YouTubeStatus YouTubeStatus { get; set; }
    public string YouTubeIdentity { get; set; }
    public string YouTubeMessage { get; set; }

    public KarafunStatus KarafunStatus { get; set; }
    public string KarafunIdentity { get; set; }
    public string KarafunMessage { get; set; }

    public VLCStatus VLCStatus { get; set; }
    public string VLCMessage { get; set; }
    
    public YtDlpStatus YtDlpStatus { get; set; }
    public string YtDlpIdentity { get; set; }
    public string YtDlpMessage { get; set; }
}

[Meta(typeof(IAutoNode))]
public partial class SessionPreparationService : Node
{
    public event Action<PrepareSessionModel> SessionStatusUpdated;
    
    private PrepareSessionModel _sessionModel;
    private IDisplayScreen _displayScreen;
    private IBrowserProviderNode _browserProvider;
    private IYtDlpProviderNode _ytDlpProvider;

    public void Initialize(IDisplayScreen displayScreen, IBrowserProviderNode browserProvider, IYtDlpProviderNode ytDlpProvider)
    {
        _displayScreen = displayScreen;
        _browserProvider = browserProvider;
        _ytDlpProvider = ytDlpProvider;
        _sessionModel = new PrepareSessionModel();
        SetupEventHandlers();
    }

    public void OnReady()
    {
        this.Provide();
    }

    private void SetupEventHandlers()
    {
        _browserProvider.BrowserAvailabilityStatusChecked += (status) =>
        {
            _sessionModel.BrowserStatus = status.StatusResult;
            _sessionModel.BrowserIdentity = status.Identity;
            _sessionModel.BrowserMessage = status.Message;
            SessionStatusUpdated?.Invoke(_sessionModel);
        };
        
        _browserProvider.YouTubeStatusChecked += (status) =>
        {
            _sessionModel.YouTubeStatus = status.StatusResult;
            _sessionModel.YouTubeIdentity = status.Identity;
            _sessionModel.YouTubeMessage = status.Message;
            SessionStatusUpdated?.Invoke(_sessionModel);
        };
        
        _browserProvider.KarafunStatusChecked += (status) =>
        {
            _sessionModel.KarafunStatus = status.StatusResult;
            _sessionModel.KarafunIdentity = status.Identity;
            _sessionModel.KarafunMessage = status.Message;
            SessionStatusUpdated?.Invoke(_sessionModel);
        };
        
        _ytDlpProvider.YtDlpStatusChecked += (status) =>
        {
            _sessionModel.YtDlpStatus = status.StatusResult;
            _sessionModel.YtDlpIdentity = status.Identity;
            _sessionModel.YtDlpMessage = status.Message;
            SessionStatusUpdated?.Invoke(_sessionModel);
        };
    }

    public void StartSessionPreparation()
    {
        _browserProvider.CheckStatus();
        _ = Task.Run(PrepareYtDlpSession);
        _ = Task.Run(PrepareVlcSession);
    }

    private async Task PrepareVlcSession()
    {
        _sessionModel.VLCStatus = VLCStatus.Initializing;
        SessionStatusUpdated?.Invoke(_sessionModel);
        await ToSignal(GetTree(), "process_frame");
        try
        {
            await _displayScreen.InitializeVlc();
            _sessionModel.VLCStatus = VLCStatus.Ready;
        }
        catch (Exception ex)
        {
            _sessionModel.VLCStatus = VLCStatus.FatalError;
            _sessionModel.VLCMessage = $"Error loading VLC libraries: {ex.Message}";
        }
        SessionStatusUpdated?.Invoke(_sessionModel);
    }
    
    private async Task PrepareYtDlpSession()
    {
        _sessionModel.YtDlpStatus = YtDlpStatus.Checking;
        SessionStatusUpdated?.Invoke(_sessionModel);
        await ToSignal(GetTree(), "process_frame");
        try
        {
            await _ytDlpProvider.CheckStatus();
        }
        catch (Exception ex)
        {
            _sessionModel.YtDlpStatus = YtDlpStatus.FatalError;
            _sessionModel.YtDlpMessage = $"Error checking yt-dlp status: {ex.Message}";
            SessionStatusUpdated?.Invoke(_sessionModel);
        }
    }

    public (string icon, string description) GetBrowserStatusLine(BrowserAvailabilityStatus status)
    {
        return status switch
        {
            BrowserAvailabilityStatus.NotStarted => ("⬛", "Not started"),
            BrowserAvailabilityStatus.Checking => ("⏳", "Checking..."),
            BrowserAvailabilityStatus.Downloading => ("⏳", "Downloading..."),
            BrowserAvailabilityStatus.Ready => ("✔", "Ready"),
            BrowserAvailabilityStatus.Busy => ("❌", "Busy"),
            BrowserAvailabilityStatus.FatalError => ("❌", "Error"),
            _ => ("⚠", "Unknown")
        };
    }

    public (string icon, string description) GetYouTubeStatusLine(YouTubeStatus status)
    {
        return status switch
        {
            YouTubeStatus.NotStarted => ("⬛", "Not started"),
            YouTubeStatus.Checking => ("⏳", "Checking..."),
            YouTubeStatus.NotLoggedIn => ("❌", "Not logged in"),
            YouTubeStatus.Premium => ("✔", "Logged into Premium Account"),
            YouTubeStatus.NotPremium => ("⚠", "Account is not Premium"),
            YouTubeStatus.Unknown => ("⚠", "Unknown"),
            YouTubeStatus.FatalError => ("❌", "Error"),
            _ => ("⚠", "Unknown")
        };
    }

    public (string icon, string description) GetKarafunStatusLine(KarafunStatus status)
    {
        return status switch
        {
            KarafunStatus.NotStarted => ("⬛", "Not started"),
            KarafunStatus.Checking => ("⏳", "Checking..."),
            KarafunStatus.NotLoggedIn => ("❌", "Not logged in"),
            KarafunStatus.Active => ("✔", "Logged into Active Subscription"),
            KarafunStatus.Inactive => ("❌", "Subscription is Inactive"),
            KarafunStatus.Unknown => ("⚠", "Unknown"),
            KarafunStatus.FatalError => ("❌", "Error"),
            _ => ("⚠", "Unknown")
        };
    }

    public (string icon, string description) GetYtDlpStatusLine(YtDlpStatus status)
    {
        return status switch
        {
            YtDlpStatus.NotStarted => ("⬛", "Not started"),
            YtDlpStatus.Checking => ("⏳", "Checking..."),
            YtDlpStatus.Downloading => ("⏳", "Downloading..."),
            YtDlpStatus.Ready => ("✔", "Ready"),
            YtDlpStatus.FatalError => ("❌", "Error"),
            _ => ("⚠", "Unknown")
        };
    }

    public string GetStreamedContentIcon(PrepareSessionModel model)
    {
        if (model.BrowserStatus == BrowserAvailabilityStatus.NotStarted && 
            model.YouTubeStatus == YouTubeStatus.NotStarted &&
            model.KarafunStatus == KarafunStatus.NotStarted)
        {
            return "⬛";
        }

        if (model.BrowserStatus == BrowserAvailabilityStatus.NotStarted ||
            model.BrowserStatus == BrowserAvailabilityStatus.Checking ||
            model.BrowserStatus == BrowserAvailabilityStatus.Downloading ||
            model.YouTubeStatus == YouTubeStatus.NotStarted ||
            model.YouTubeStatus == YouTubeStatus.Checking ||
            model.KarafunStatus == KarafunStatus.NotStarted ||
            model.KarafunStatus == KarafunStatus.Checking)
        {
            return "⏳";
        }

        if (model.BrowserStatus == BrowserAvailabilityStatus.Ready &&
            model.YouTubeStatus == YouTubeStatus.Premium &&
            model.KarafunStatus == KarafunStatus.Active)
        {
            return "✔";
        }
        if (model.BrowserStatus == BrowserAvailabilityStatus.FatalError || 
            model.YouTubeStatus == YouTubeStatus.FatalError || 
            model.KarafunStatus == KarafunStatus.FatalError)
        {
            return "❌";
        }
        return "⚠";
    }

    public PrepareSessionModel GetCurrentSessionModel() => _sessionModel;
}