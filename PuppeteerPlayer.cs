using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public class PuppeteerPlayer
{
	private static IBrowser _browser;
	private static IPage _page;

	private static string GetAllPossibleExceptionInfo(Exception ex, StringBuilder builder = null)
	{
		var sb = builder ?? new StringBuilder();
		if (ex == null)
		{
			return sb.ToString();
		}

		sb.AppendLine($"Type: {ex.GetType().Name} ");
		sb.AppendLine($"Message: {ex.Message} ");
		sb.AppendLine($"HResult: {ex.HResult} ");
		if (ex.Data != null)
		{
			foreach (var key in ex.Data.Keys)
			{
				sb.AppendLine($"Data - {key}: {ex.Data[key]} ");
			}
		}
		sb.AppendLine($"Source: {ex.Source} ");
		var baseException = ex.GetBaseException();
		if (baseException != null && baseException != ex)
		{
			sb.AppendLine($"Base Exception: {baseException.GetType().Name} {baseException.Message} ");
		}
		if (ex.InnerException != null)
		{
			sb.AppendLine("-->");
			GetAllPossibleExceptionInfo(ex.InnerException, sb);
		}

		return sb.ToString();
	}

	public static async Task LaunchUnautomatedBrowser(params string[] sites)
	{
		var executablePath = await Utils.EnsureBrowser();
		var userDataDir = GetBrowserUserProfileDir();
		OS.CreateProcess(executablePath, new[]{$"--user-data-dir=\"{userDataDir}\""}.Concat(sites).ToArray());
	}

	public static async Task CloseBrowser()
	{
		if (_browser != null)
		{
			await _browser.CloseAsync();
			_browser = null;
		}
	}

	public static async Task LaunchAutomatedBrowser()
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
				GD.Print($"Failed to launch browser: {GetAllPossibleExceptionInfo(ex)}");
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

	public static string GetBrowserUserProfileDir()
	{
		return Path.Combine(Utils.GetAppStoragePath(), "browser_user_profile");
	}

	private static string GetAttributeFunction(string selector, string attribute)
	{
		return $@"(selector) => {{
			const el = document.querySelector(selector);
			return el ? el.getAttribute('{attribute}') : null;
		}}";
	}
	private static async Task<string> GetAttributeValue(IPage page, string selector, string attribute, bool log = false)
	{
		var result = await page.EvaluateFunctionAsync<string>(GetAttributeFunction(selector, attribute), selector);
		if (log) {
			GD.Print($"{selector} {attribute}=\"{result}\"");
		}
		return result;
	}

	public static async Task PlayYoutubeUrl(string url, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentNullException(nameof(url));
		}
		await LaunchAutomatedBrowser();

		GD.Print("Navigating to the video page...");
		await _page.GoToAsync(
			$"{url}", // why? idk; sometimes it fails "failed to deserialize params.url" for no reason
		WaitUntilNavigation.Networkidle2);

		// Wait for the play/pause button to be present
		GD.Print("Waiting for the play button...");
		await _page.WaitForSelectorAsync(".ytp-play-button");

		// Wait for the fullscreen button to be present
		GD.Print("Waiting for the fullscreen button...");
		await _page.WaitForSelectorAsync(".ytp-fullscreen-button");

		// Check if the video is fullscreen
		var fullscreenButton = await _page.QuerySelectorAsync(".ytp-fullscreen-button");
		var fullscreenButtonTitle = await GetAttributeValue(_page, ".ytp-fullscreen-button", "data-title-no-tooltip");
		if (fullscreenButtonTitle == "Full screen" && !cancellationToken.IsCancellationRequested)
		{
			GD.Print("Fullscreen button needs to be clicked. Clicking...");
			await fullscreenButton.ClickAsync();
		}

		// Check if the video is playing
		var playButton = await _page.QuerySelectorAsync(".ytp-play-button");
		var playButtonTitle = await GetAttributeValue(_page, ".ytp-play-button", "data-title-no-tooltip");
		//GD.Print("Play button title: " + playButtonTitle);
		if (playButtonTitle == "Play" && !cancellationToken.IsCancellationRequested)
		{
			GD.Print("Play button needs to be clicked. Clicking...");
			await playButton.ClickAsync();
		}

		// Disable autoplay for the next video
		var autoplayButton = await _page.QuerySelectorAsync(".ytp-autonav-toggle-button");
		var autoplayChecked = await GetAttributeValue(_page, ".ytp-autonav-toggle-button", "aria-checked");
		if (autoplayChecked == "true")
		{
			GD.Print("Autoplay toggle needs to be clicked. Clicking...");
			await autoplayButton.ClickAsync();
		}

		// let it play
		while (!cancellationToken.IsCancellationRequested)
		{
			// grab the play button title again
			playButtonTitle = await GetAttributeValue(_page, ".ytp-play-button", "data-title-no-tooltip");
			if (playButtonTitle == "Replay")
			{
				GD.Print("Playback has stopped.");
				break;
			}

			await Task.Delay(1000, cancellationToken) // Small delay (1 second)
					.ContinueWith(task => {}); // (stfu TaskCanceledException, that's expected)
		}

		if (cancellationToken.IsCancellationRequested)
		{
			GD.Print("Playback was cancelled.");
			await _page.GoToAsync("about:blank");
		}
		else
		{
			GD.Print("Playback finished.");
		}
	}

    public static async Task PauseKarafun()
    {
        if (_page != null)
        {
            var pauseButton = await _page.QuerySelectorAsync(".player__audio__pause");
            if (pauseButton != null)
            {
                await pauseButton.ClickAsync();
            }
        }
    }
    public static async Task ResumeKarafun()
    {
        if (_page != null)
        {
            var playButton = await _page.QuerySelectorAsync(".player__audio__play");
            if (playButton != null)
            {
                await playButton.ClickAsync();
            }
        }
    }
    public static async Task ToggleYoutube()
    {
        if (_page != null)
        {
            var playPauseButton = await _page.QuerySelectorAsync(".ytp-play-button");
            if (playPauseButton != null)
            {
                await playPauseButton.ClickAsync();
            }
        }
    }
	public static async Task PlayKarafunUrl(string url, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentNullException(nameof(url));
		}

		await LaunchAutomatedBrowser();

		GD.Print("Navigating to the track page...");
		await _page.GoToAsync(
			$"{url}", // why? idk; sometimes it fails "failed to deserialize params.url" for no reason
		WaitUntilNavigation.Networkidle2);

		// Check for the presence of the player__subscribe element
		// TODO: unfortunately this doesn't always go away after logging into an active subscription and idk why... cache maybe?
		var subscribeSelector = ".player__subscribe";
		var isSubscribePresent = false;//await page.QuerySelectorAsync(subscribeSelector) != null;

		if (isSubscribePresent)
		{
			GD.Print("Subscribe element found. Redirecting to login page...");

			// Redirect to the login page with the original URL as a referrer
			var loginUrl = Utils.EnsureAbsoluteUrl($"/my/login.html", url);

			await _page.GoToAsync(loginUrl, new NavigationOptions
			{
				WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
				Referer = url
			});

			// Wait for the user to log in and be redirected back to the original URL
			await _page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }, Timeout = 0 });

			GD.Print("User logged in. Navigating back to the original URL...");
			await _page.GoToAsync(url, WaitUntilNavigation.Networkidle2);
		}

		GD.Print("Waiting for the fullscreen button...");
		var fullscreenButtonSelector = ".player__audio__fullscreen";
		await _page.WaitForSelectorAsync(fullscreenButtonSelector);

		// Check if the fullscreen button needs to be clicked
		var isEnlargeIcon = await _page.EvaluateFunctionAsync<bool>(@"(selector) => {
				const fullscreenButton = document.querySelector(selector);
				if (fullscreenButton) {
					const svg = fullscreenButton.querySelector('use');
					return svg && svg.getAttribute('xlink:href') === '#icon-enlarge';
				}
				return false;
			}", fullscreenButtonSelector);

		if (isEnlargeIcon && !cancellationToken.IsCancellationRequested)
		{
			GD.Print("Fullscreen button needs to be clicked. Clicking...");
			await _page.ClickAsync(fullscreenButtonSelector);
		}
		else
		{
			GD.Print("Already in fullscreen mode.");
		}

		// Wait for the player time element to be present and get its content
		GD.Print("Waiting for the player time element...");
		var playerTimeSelector = ".player__audio__time";
		await _page.WaitForSelectorAsync(playerTimeSelector, new WaitForSelectorOptions { Visible = true, Timeout = 0 });

		var playerTime = await _page.EvaluateFunctionAsync<string>(@"(selector) => {
				const el = document.querySelector(selector);
				return el ? el.textContent.trim() : null;
			}", playerTimeSelector);
		GD.Print($"Initial player__audio__time text: {playerTime}");

		// Wait for the play button to be clickable
		GD.Print("Waiting for the play button...");
		var playButtonSelector = ".player__audio__play";
		await _page.WaitForSelectorAsync(playButtonSelector, new WaitForSelectorOptions { Visible = true });

		bool playbackStarted = false;
		while (!playbackStarted && !cancellationToken.IsCancellationRequested)
		{
			await _page.ClickAsync(playButtonSelector);
			GD.Print("Trying to start playing...");

			// Wait for a sec
			await Task.Delay(1000);

			// Check if the play button is still present
			var playButton = await _page.QuerySelectorAsync(playButtonSelector);
			if (playButton == null)
			{
				playbackStarted = true;
				GD.Print("Play button is no longer present so playback probably started.");
			}
			else
			{
				//GD.Print("Play button is still present. Trying again...");
			}
		}

		// Monitor the player time text for changes
		GD.Print("Monitoring playback time...");
		var previousTime = playerTime;
		while (!cancellationToken.IsCancellationRequested)
		{
			var currentTime = await _page.EvaluateFunctionAsync<string>(@"(selector) => {
				const el = document.querySelector(selector);
				return el ? el.textContent.trim() : null;
			}", playerTimeSelector);

			if (currentTime != null && currentTime != previousTime)
			{
				//GD.Print($"player__audio__time text changed: '{currentTime}'");
				previousTime = currentTime;
			}

			// Break the loop if the player time text becomes empty
			if (currentTime == null || currentTime.Trim() == string.Empty)
			{
				GD.Print("Playback has stopped.");
				break;
			}

			await Task.Delay(1000, cancellationToken) // Small delay (1 second)
					.ContinueWith(task => {}); // (stfu TaskCanceledException, that's expected)
		}

		if (cancellationToken.IsCancellationRequested)
		{
			GD.Print("Playback was cancelled.");
			await _page.GoToAsync("about:blank");
		}
		else
		{
			GD.Print("Playback finished.");
		}
	}
}
