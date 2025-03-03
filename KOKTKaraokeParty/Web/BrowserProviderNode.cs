using System.Linq;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using PuppeteerSharp;

public interface IBrowserProviderNode : INode
{
    Task CheckStatus(bool checkYouTube = true, bool checkKarafun = true);
    event BrowserAvailabilityStatusEventHandler BrowserAvailabilityStatusChecked;
    event YouTubeStatusEventHandler YouTubeStatusChecked;
    event KarafunStatusEventHandler KarafunStatusChecked;
}

public enum BrowserAvailabilityStatus
{
    Checking,
    Downloading,
    Ready,
    Busy,
    FatalError
}

public delegate void BrowserAvailabilityStatusEventHandler(StatusCheckResult<BrowserAvailabilityStatus> status);
public delegate void YouTubeStatusEventHandler(StatusCheckResult<YouTubeStatus> status);
public delegate void KarafunStatusEventHandler(StatusCheckResult<KarafunStatus> status);

[Meta(typeof(IAutoNode))]
public partial class BrowserProviderNode : Node, IBrowserProviderNode
{
    public override void _Notification(int what) => this.Notify(what);

    private IBrowser _headlessBrowser;
    private IBrowser _headedBrowser;
    private IBrowserFetcher _browserFetcher;

    public event BrowserAvailabilityStatusEventHandler BrowserAvailabilityStatusChecked;
    public event YouTubeStatusEventHandler YouTubeStatusChecked;
    public event KarafunStatusEventHandler KarafunStatusChecked;

    #region Initialized Dependencies

    private IYoutubeAutomator _youtubeAutomator { get; set; }
    private IKarafunAutomator _karafunAutomator { get; set; }

    public void SetupForTesting(IYoutubeAutomator youtubeAutomator, IKarafunAutomator karafunAutomator, IBrowserFetcher browserFetcher)
    {
        _youtubeAutomator = youtubeAutomator;
        _karafunAutomator = karafunAutomator;
        _browserFetcher = browserFetcher;
    }

    public void Initialize()
    {
        _youtubeAutomator = new YoutubeAutomator();
        _karafunAutomator = new KarafunAutomator();
        _browserFetcher = Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
        {
            Browser = SupportedBrowser.Chromium,
            Path = ProjectSettings.GlobalizePath("user://browser")
        });
    }

    #endregion

    public async Task CheckStatus(bool checkYouTube = true, bool checkKarafun = true)
    {
        // you have to check the browser in order to check the status
        string path;
        try
        {
            GD.Print("Checking browser status...");
            BrowserAvailabilityStatusChecked?.Invoke(
                    new StatusCheckResult<BrowserAvailabilityStatus>(
                        BrowserAvailabilityStatus.Checking, 
                        null, 
                        null));
            var chromiumRevision = await CheckForBrowser(_browserFetcher);
            if (chromiumRevision == null)
            {
                GD.Print("Downloading browser...");
                BrowserAvailabilityStatusChecked?.Invoke(
                    new StatusCheckResult<BrowserAvailabilityStatus>(
                        BrowserAvailabilityStatus.Downloading, 
                        null, 
                        null));
                var revisionInfo = await _browserFetcher.DownloadAsync();
                chromiumRevision = revisionInfo?.BuildId;
            }
            path = _browserFetcher.GetExecutablePath(chromiumRevision);
            GD.Print($"Browser ready at {path}");
            BrowserAvailabilityStatusChecked?.Invoke(
                new StatusCheckResult<BrowserAvailabilityStatus>(
                    BrowserAvailabilityStatus.Ready, 
                    $"Chromium {chromiumRevision}", 
                    path));
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Browser status check failed: {ex.Message}");
            BrowserAvailabilityStatusChecked?.Invoke(
                new StatusCheckResult<BrowserAvailabilityStatus>(BrowserAvailabilityStatus.FatalError, null, ex.Message));
            if (checkYouTube)
            {
                YouTubeStatusChecked?.Invoke(new StatusCheckResult<YouTubeStatus>(
                    YouTubeStatus.FatalError, null, "Could not check YouTube status because browser access failed."));
            }
            if (checkKarafun)
            {
                KarafunStatusChecked?.Invoke(new StatusCheckResult<KarafunStatus>(
                    KarafunStatus.FatalError, null, "Could not check Karafun status because browser access failed."));
            }
            return;
        }

        IBrowser browserToUse = null;
        bool iOwnThisBrowser = false;
        if (_headedBrowser != null && !_headedBrowser.IsClosed)
        {
            // TODO: consider this case. because if they're dicking around with this while someone is singing it could be a real mess...
            browserToUse = _headedBrowser;
            GD.PrintErr("There is a visible browser already in use!");
            BrowserAvailabilityStatusChecked?.Invoke(
                new StatusCheckResult<BrowserAvailabilityStatus>(
                    BrowserAvailabilityStatus.Busy, 
                    _headedBrowser.Process?.Id.ToString(),
                    "There is a visible browser already in use."));
        }
        else if (_headlessBrowser != null && !_headlessBrowser.IsClosed)
        {
            browserToUse = _headlessBrowser;
            GD.PrintErr("A headless browser already exists, which is odd.");
            BrowserAvailabilityStatusChecked?.Invoke(
                new StatusCheckResult<BrowserAvailabilityStatus>(
                    BrowserAvailabilityStatus.Busy, 
                    _headedBrowser.Process?.Id.ToString(),
                    "A headless browser already exists, which is odd."));
        }

        if (browserToUse == null)
        {
            iOwnThisBrowser = true;
            _headlessBrowser = await CreateBrowser(path, true);
            browserToUse = _headlessBrowser;
        }

        // TODO: check these concurrently... it's just that if you do, then your 
        // invokes "can't call this function in this node" so you'd have to wrap 
        // them with CallDeferred but if you do THAT then they have to be FUCKING
        // GODOT VARIANTS AND I HATE THAT STUPID SHIT SO VERY MUCH GOD DAMN
        if (checkYouTube)
        {
            var youTubeCheckPage = await browserToUse.NewPageAsync();
            YouTubeStatusChecked?.Invoke(new StatusCheckResult<YouTubeStatus>(YouTubeStatus.Checking, null, null));
            var youTubeStatusResult = await _youtubeAutomator.CheckStatus(youTubeCheckPage);
            YouTubeStatusChecked?.Invoke(youTubeStatusResult);
            await youTubeCheckPage.CloseAsync();
        }
        if (checkKarafun)
        {
            var karafunCheckPage = await browserToUse.NewPageAsync();
            KarafunStatusChecked?.Invoke(new StatusCheckResult<KarafunStatus>(KarafunStatus.Checking, null, null));
            var karafunStatusResult = await _karafunAutomator.CheckStatus(karafunCheckPage);
            KarafunStatusChecked?.Invoke(karafunStatusResult);
            await karafunCheckPage.CloseAsync();
        }

        if (iOwnThisBrowser)
        {
            await browserToUse.CloseAsync();
            await browserToUse.DisposeAsync();
        }
    }

    private async Task<IBrowser> CreateBrowser(string executablePath, bool isHeadless = false)
    {
        try
        {
            return await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = isHeadless, // Show the browser
                DefaultViewport = null, // Fullscreen viewport
                Browser = SupportedBrowser.Chromium,
                ExecutablePath = executablePath,
                Args = new[] {
                    //"--start-fullscreen", // browser fullscreen on launch actually makes karafun stretch wrong :(
                    "--no-default-browser-check", // Skip the default browser check
                },
                IgnoredDefaultArgs = new[] {
                    "--enable-automation", // Disable the "Chrome is being controlled by automated test software" notification
                    "--enable-blink-features=IdleDetection" // May or may not get some message about that
                },
                UserDataDir = GetBrowserUserProfileDir() // Path to store user session
            });
        }
        catch (ProcessException ex)
        {
            GD.Print($"Failed to launch browser: {Utils.GetAllPossibleExceptionInfo(ex)}");
            throw;
        }
    }

    public string GetBrowserUserProfileDir()
    {
        return ProjectSettings.GlobalizePath("user://browser_user_profile");
    }

    private async Task<string> CheckForBrowser(IBrowserFetcher fetcher)
    {
        return await Task.Run(() => { 
            var installedBrowsers = fetcher.GetInstalledBrowsers();
            var chromiumRevision = installedBrowsers.FirstOrDefault(a => a.Browser == SupportedBrowser.Chromium)?.BuildId;
            return chromiumRevision;
        });
    }
}