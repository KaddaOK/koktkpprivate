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
    Task ToggleYoutubePlayback();
}

public class PuppeteerPlayer : IPuppeteerPlayer
{
    private IBrowser _browser;
    private IPage _page;

    private IYoutubeAutomator _youtubeAutomator;
    private IKarafunAutomator _karafunAutomator;
    public PuppeteerPlayer(IYoutubeAutomator youtubeAutomator, IKarafunAutomator karafunAutomator)
    {
        _youtubeAutomator = youtubeAutomator;
        _karafunAutomator = karafunAutomator;
    }
    public PuppeteerPlayer() : this(new YoutubeAutomator(), new KarafunAutomator()) { }

    public async Task LaunchUnautomatedBrowser(params string[] sites)
    {
        var executablePath = await Utils.EnsureBrowser();
        var userDataDir = GetBrowserUserProfileDir();
        OS.CreateProcess(executablePath, new[] { $"--user-data-dir=\"{userDataDir}\"" }.Concat(sites).ToArray());
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
						//"--start-fullscreen", // Fullscreen mode on launch
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
}
