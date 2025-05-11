using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public interface IPuppeteerPlayer
{
    Task LaunchAutomatedBrowser();
    Task LaunchUnautomatedBrowser(params string[] sites);
    Task CloseAutomatedBrowser();
    Task PlayYoutubeUrl(string url, CancellationToken cancellationToken);
    Task PlayKarafunUrl(string url, CancellationToken cancellationToken);
    Task PauseKarafun();
    Task ResumeKarafun();
    Task SeekKarafun(long positionMs);
    Task ToggleYoutubePlayback();
    Task SeekYouTube(long positionMs);

    event PlaybackProgressEventHandler PlaybackProgress;
    event PlaybackDurationChangedEventHandler PlaybackDurationChanged;
}

public class PuppeteerPlayer : IPuppeteerPlayer
{
    public event PlaybackProgressEventHandler PlaybackProgress;
    public event PlaybackDurationChangedEventHandler PlaybackDurationChanged;
    
    private IBrowser _browser;
    private IPage _page;

    private IYoutubeAutomator _youtubeAutomator;
    private IKarafunAutomator _karafunAutomator;
    public PuppeteerPlayer(IYoutubeAutomator youtubeAutomator, IKarafunAutomator karafunAutomator)
    {
        _youtubeAutomator = youtubeAutomator;
        _karafunAutomator = karafunAutomator;
        _karafunAutomator.PlaybackProgress += (progressMs) => PlaybackProgress?.Invoke(progressMs);
        _karafunAutomator.PlaybackDurationChanged += (durationMs) => PlaybackDurationChanged?.Invoke(durationMs);
        _youtubeAutomator.PlaybackProgress += (progressMs) => PlaybackProgress?.Invoke(progressMs);
        _youtubeAutomator.PlaybackDurationChanged += (durationMs) => PlaybackDurationChanged?.Invoke(durationMs);
    }
    public PuppeteerPlayer() : this(new YoutubeAutomator(), new KarafunAutomator()) { }

    public async Task LaunchUnautomatedBrowser(params string[] sites)
    {
        var executablePath = await Utils.EnsureBrowser();
        GD.Print($"Browser executable: {executablePath}");
        var userDataDir = GetBrowserUserProfileDir();
        GD.Print($"User data directory: {userDataDir}");
        var paramsArray = new[] { $"--user-data-dir=\"{userDataDir}\"" }.Concat(sites).ToArray();
        GD.Print($"Launching browser with parameters: {string.Join("|", paramsArray)}");
        OS.CreateProcess(executablePath, paramsArray);
    }

    public async Task CloseAutomatedBrowser()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
    }

    public async Task LaunchAutomatedBrowser()
    {
        var executablePath = await Utils.EnsureBrowser();
        var userDataDir = GetBrowserUserProfileDir();
        GD.Print($"User data directory: {userDataDir}");
        Directory.CreateDirectory(userDataDir); // Ensure the directory exists

        // Launch the browser
        if (_browser == null)
        {
            try
            {
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false, // Show the browser
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
                    UserDataDir = userDataDir // Path to store user session
                });
            }
            catch (ProcessException ex)
            {
                GD.Print($"Failed to launch browser: {Utils.GetAllPossibleExceptionInfo(ex)}");
                throw;
            }
        }
        // get the initial page(s), and then
        var initialPages = await _browser.PagesAsync();
        // create a new page, and then
        _page = await _browser.NewPageAsync();
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

    public string GetBrowserUserProfileDir()
    {
        return Path.Combine(Utils.GetAppStoragePath(), "browser_user_profile");
    }

    public async Task PlayYoutubeUrl(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentNullException(nameof(url));
        }
        await LaunchAutomatedBrowser();
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

        await LaunchAutomatedBrowser();

        await _karafunAutomator.PlayKarafunUrl(_page, url, cancellationToken);
    }

    public async Task SeekKarafun(long positionMs) => await _karafunAutomator.Seek(_page, positionMs);
}
