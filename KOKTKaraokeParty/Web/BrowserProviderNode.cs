using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

    Task LaunchControlledBrowser();
    Task<Process> LaunchUncontrolledBrowser(params string[] sites);
    Task CloseControlledBrowser();
    Task PlayYoutubeUrl(string url, CancellationToken cancellationToken);
    Task PlayKarafunUrl(string url, CancellationToken cancellationToken);
    Task PauseKarafun();
    Task ResumeKarafun();
    Task SeekKarafun(long positionMs);
    Task ToggleYoutubePlayback();
    Task SeekYouTube(long positionMs);

    event BrowserProviderNode.PlaybackProgressEventHandler PlaybackProgress;
    event BrowserProviderNode.PlaybackDurationChangedEventHandler PlaybackDurationChanged;
}

public enum BrowserAvailabilityStatus
{
    NotStarted,
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
    private static SupportedBrowser _browserType = SupportedBrowser.Chromium; // TODO: make configurable

    public override void _Notification(int what) => this.Notify(what);

    private IBrowser _headlessBrowser;
    private IBrowser _headedBrowser;
    //private int _uncontrolledBrowserPid = -1;
    private Process _uncontrolledBrowserProcess;
    private IPage _page; // TODO: rename
    private IBrowserFetcher _browserFetcher;

    public event BrowserAvailabilityStatusEventHandler BrowserAvailabilityStatusChecked;
    public event YouTubeStatusEventHandler YouTubeStatusChecked;
    public event KarafunStatusEventHandler KarafunStatusChecked;
    public delegate void PlaybackProgressEventHandler(long progressMs);
    public delegate void PlaybackDurationChangedEventHandler(long durationMs);
    public event PlaybackProgressEventHandler PlaybackProgress;
    public event PlaybackDurationChangedEventHandler PlaybackDurationChanged;

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
        _youtubeAutomator.PlaybackProgress += (progressMs) => PlaybackProgress?.Invoke(progressMs);
        _youtubeAutomator.PlaybackDurationChanged += (durationMs) => PlaybackDurationChanged?.Invoke(durationMs);
        _karafunAutomator = new KarafunAutomator();
        _karafunAutomator.PlaybackProgress += (progressMs) => PlaybackProgress?.Invoke(progressMs);
        _karafunAutomator.PlaybackDurationChanged += (durationMs) => PlaybackDurationChanged?.Invoke(durationMs);
        _browserFetcher = Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
        {
            Browser = _browserType,
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
            var browserRevision = await CheckForBrowser(_browserFetcher);
            if (browserRevision == null)
            {
                GD.Print("Downloading browser...");
                BrowserAvailabilityStatusChecked?.Invoke(
                    new StatusCheckResult<BrowserAvailabilityStatus>(
                        BrowserAvailabilityStatus.Downloading,
                        null,
                        null));
                var revisionInfo = await _browserFetcher.DownloadAsync();
                browserRevision = revisionInfo?.BuildId;
            }
            path = _browserFetcher.GetExecutablePath(browserRevision);
            GD.Print($"Browser ready at {path}");
            BrowserAvailabilityStatusChecked?.Invoke(
                new StatusCheckResult<BrowserAvailabilityStatus>(
                    BrowserAvailabilityStatus.Ready,
                    $"{_browserType} {browserRevision}",
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

        var concurrentChecks = new List<Task>();
        if (checkYouTube)
        {
            concurrentChecks.Add(Task.Run(async () =>
            {
                var youTubeCheckPage = await browserToUse.NewPageAsync();
                Callable.From(() =>
                    YouTubeStatusChecked?.Invoke(new StatusCheckResult<YouTubeStatus>(YouTubeStatus.Checking, null, null))
                    ).CallDeferred();
                var youTubeStatusResult = await _youtubeAutomator.CheckStatus(youTubeCheckPage);
                Callable.From(() =>
                    YouTubeStatusChecked?.Invoke(youTubeStatusResult)
                    ).CallDeferred();
                await youTubeCheckPage.CloseAsync();
            }));
        }
        if (checkKarafun)
        {
            concurrentChecks.Add(Task.Run(async () =>
            {
                var karafunCheckPage = await browserToUse.NewPageAsync();
                Callable.From(() =>
                    KarafunStatusChecked?.Invoke(new StatusCheckResult<KarafunStatus>(KarafunStatus.Checking, null, null))
                    ).CallDeferred();
                var karafunStatusResult = await _karafunAutomator.CheckStatus(karafunCheckPage);
                Callable.From(() =>
                    KarafunStatusChecked?.Invoke(karafunStatusResult)
                    ).CallDeferred();
                await karafunCheckPage.CloseAsync();
            }));
        }

        await Task.WhenAll(concurrentChecks);

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
                Browser = _browserType,
                ExecutablePath = executablePath,
                Args = _browserType == SupportedBrowser.Firefox ? null 
                : new[] {
                    //"--start-fullscreen", // browser fullscreen on launch actually makes karafun stretch wrong :(
                    "--no-default-browser-check", // Skip the default browser check
                },
                IgnoredDefaultArgs = _browserType == SupportedBrowser.Firefox ? null 
                : new[] {
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

    public async Task<Process> LaunchUncontrolledBrowser(params string[] sites)
    {
        var executablePath = await EnsureBrowser();
        GD.Print($"Browser executable: {executablePath}");
        var userDataDir = GetBrowserUserProfileDir();
        GD.Print($"User data directory: {userDataDir}");
        var paramsArray = new[] { $"--user-data-dir=\"{userDataDir}\"" }.Concat(sites).ToArray();
        GD.Print($"Launching browser with parameters: {string.Join("|", paramsArray)}");
        //_uncontrolledBrowserPid = OS.CreateProcess(executablePath, paramsArray);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = string.Join(" ", paramsArray),
            UseShellExecute = false,   // Important for redirecting IO and for cross-platform use
            CreateNoWindow = true,     // Optional
        };

        _uncontrolledBrowserProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _uncontrolledBrowserProcess.Exited += (sender, e) =>
        {
            GD.Print("Browser process exited.");
            // TODO: emit an event here?
        };

        _uncontrolledBrowserProcess.Start();
        return _uncontrolledBrowserProcess;
    }

    public async Task LaunchControlledBrowser()
    {
        var executablePath = await EnsureBrowser();
        var userDataDir = GetBrowserUserProfileDir();
        GD.Print($"User data directory: {userDataDir}");
        Directory.CreateDirectory(userDataDir); // Ensure the directory exists

        // Launch the browser
        if (_headedBrowser == null)
        {
            _headedBrowser = await CreateBrowser(executablePath, false);
        }
        // get the initial page(s), and then
        var initialPages = await _headedBrowser.PagesAsync();
        // create a new page, and then
        _page = await _headedBrowser.NewPageAsync();
        // add custom CSS to make it less intrusive to wait for the fullscreen button
        await _page.SetBypassCSPAsync(true);
        await _page.EvaluateExpressionOnNewDocumentAsync(@"
            const windowTimeout = () => {
                const head = document.getElementsByTagName('head');
                if (head && head.length > 0) {
                    console.log('creating style element');
                    const style = document.createElement('style');
                    style.type = 'text/css';
                    style.innerHTML = 
                    `
                    /* Helps for Karafun; YouTube doesn't care */
                    body { background-color: black !important; }

                    /* Hides for Karafun that don't affect YouTube */
                    #app .left { display: none !important; }
                    #app .queue { display: none !important; }

                    /* Hides for YouTube that don't affect Karafun */
                    #secondary { display: none; }
                    #below { display: none; }
                    #ytd-player { width: 100%; height: 100% }
                    .html5-video-container { width: 100%; height: 100% }
                    #center { opacity: 0; }
                    .ytp-endscreen-content { opacity: 0; }
                    `;
                    head[0].appendChild(style);
                } else {
                    console.log('no head element found; trying again in 10 ms');
                    window.setTimeout(windowTimeout, 10);
                }
            };
            window.setTimeout(windowTimeout, 0);
        ");

        // close the initial page(s) because, well, otherwise weird shenanigans happen
        foreach (var initialPage in initialPages)
        {
            await initialPage.CloseAsync();
        }
    }

    public async Task CloseControlledBrowser()
    {
        if (_headlessBrowser != null)
        {
            await _headlessBrowser.CloseAsync();
            _headlessBrowser = null;
        }
        if (_headedBrowser != null)
        {
            await _headedBrowser.CloseAsync();
            _headedBrowser = null;
        }
    }

    public string GetBrowserUserProfileDir()
    {
        return ProjectSettings.GlobalizePath("user://browser_user_profile");
    }

    private async Task<string> CheckForBrowser(IBrowserFetcher fetcher)
    {
        return await Task.Run(() =>
        {
            var installedBrowsers = fetcher.GetInstalledBrowsers();
            var browserRevision = installedBrowsers.FirstOrDefault(a => a.Browser == _browserType)?.BuildId;
            return browserRevision;
        });
    }


    #region TODO this is all stupid
    // TODO: is any of this used?  Seems like a lot of it is also on BrowserProviderNode...
    private static IBrowserFetcher GetBrowserFetcher()
    {
        var downloadPath = ProjectSettings.GlobalizePath("user://browser");
        return Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
        {
            Browser = _browserType,
            Path = downloadPath
        });
    }

    public async Task<string> EnsureBrowser() // only used from BrowserProviderNode
    {
        GD.Print("Ensuring browser...");
        var fetcher = GetBrowserFetcher();
        var browserRevision = await CheckForBrowser(fetcher);

        if (browserRevision == null)
        {
            GD.Print("Downloading browser...");
            var revisionInfo = await fetcher.DownloadAsync();
            browserRevision = revisionInfo.BuildId;
        }

        var path = fetcher.GetExecutablePath(browserRevision);
        GD.Print($"Browser is ready ({_browserType} {browserRevision} at {path}).");
        return path;
    }

#endregion
    
    public async Task PlayYoutubeUrl(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentNullException(nameof(url));
        }
        await LaunchControlledBrowser();
        await _youtubeAutomator.PlayYoutubeUrl(_page, url, cancellationToken);
    }

    public async Task SeekYouTube(long positionMs) => await _youtubeAutomator.Seek(_page, positionMs);

    public async Task ToggleYoutubePlayback() => await _youtubeAutomator.ToggleYoutubePlayback(_page);

    public async Task PauseKarafun() => await _karafunAutomator.PauseKarafun(_page);
    public async Task ResumeKarafun() => await _karafunAutomator.ResumeKarafun(_page);

    public async Task PlayKarafunUrl(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        await LaunchControlledBrowser();

        await _karafunAutomator.PlayKarafunUrl(_page, url, cancellationToken);
    }

    public async Task SeekKarafun(long positionMs) => await _karafunAutomator.Seek(_page, positionMs);
}