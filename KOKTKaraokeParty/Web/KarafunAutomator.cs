using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public interface IKarafunAutomator
{
    Task PlayKarafunUrl(IPage page, string url, CancellationToken cancellationToken);
    Task PauseKarafun(IPage page);
    Task ResumeKarafun(IPage page);
    Task<StatusCheckResult<KarafunStatus>> CheckStatus(IPage page);
    Task Seek(IPage page, long positionMs);
    event PlaybackProgressEventHandler PlaybackProgress;
    event PlaybackDurationChangedEventHandler PlaybackDurationChanged;
}

public enum KarafunStatus
{
    NotStarted,
    Checking,
    NotLoggedIn,
    Active,
    Inactive,
    Unknown,
    FatalError
}

public class KarafunAutomator : WebAutomatorBase<KarafunStatus>, IKarafunAutomator
{
    public string CurrentPath { get; private set; }
    public long? CurrentPositionMs { get; private set; }
    public long? ItemDurationMs { get; private set; }

    public async Task PauseKarafun(IPage page)
    {
        if (page != null)
        {
            var pauseButton = await page.QuerySelectorAsync(".player__audio__pause");
            if (pauseButton != null)
            {
                await pauseButton.ClickAsync();
            }
        }
    }
    
    public async Task ResumeKarafun(IPage page)
    {
        if (page != null)
        {
            var playButton = await page.QuerySelectorAsync(".player__audio__play");
            if (playButton != null)
            {
                await playButton.ClickAsync();
            }
        }
    }

public async Task Seek(IPage page, long positionMs)
    {
        if (CurrentPositionMs != null && ItemDurationMs != null)
        {
            if (positionMs < 0)
            {
                positionMs = 0;
            }
            else if (positionMs > ItemDurationMs)
            {
                return;
            }

            if (page != null)
            {
                var seekBar = await page.QuerySelectorAsync(".player__audio__bar");
                if (seekBar != null)
                {
                    var seekBarRect = await seekBar.BoundingBoxAsync();
                    var seekBarWidth = seekBarRect.Width;
                    var seekBarX = seekBarRect.X;
                    var seekBarY = seekBarRect.Y;
                    var seekBarCenterX = seekBarX + seekBarWidth / 2;
                    var seekBarCenterY = seekBarY + seekBarRect.Height / 2;
                    var seekBarClickX = seekBarX + (seekBarWidth * positionMs / ItemDurationMs.Value);
                    var seekBarClickY = seekBarCenterY;
                    await page.Mouse.ClickAsync(seekBarClickX, seekBarClickY);
                }
            }
        }
    }

    public event PlaybackProgressEventHandler PlaybackProgress;
    public event PlaybackDurationChangedEventHandler PlaybackDurationChanged;

    public async Task PlayKarafunUrl(IPage page, string url, CancellationToken cancellationToken)
    {
        CurrentPath = url;
        CurrentPositionMs = 0;
        ItemDurationMs = 0;

        GD.Print("Navigating to the track page...");
        await page.GoToAsync(
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

            await page.GoToAsync(loginUrl, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Referer = url
            });

            // Wait for the user to log in and be redirected back to the original URL
            await page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }, Timeout = 0 });

            GD.Print("User logged in. Navigating back to the original URL...");
            await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);
        }

        GD.Print("Waiting for the fullscreen button...");
        var fullscreenButtonSelector = ".player__audio__fullscreen";
        await page.WaitForSelectorAsync(fullscreenButtonSelector);

        // Check if the fullscreen button needs to be clicked
        var isEnlargeIcon = await page.EvaluateFunctionAsync<bool>(@"(selector) => {
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
            await page.ClickAsync(fullscreenButtonSelector);
        }
        else
        {
            GD.Print("Already in fullscreen mode.");
        }

        // Wait for the player time element to be present and get its content
        GD.Print("Waiting for the player time element...");
        var playerTimeSelector = ".player__audio__time";
        await page.WaitForSelectorAsync(playerTimeSelector, new WaitForSelectorOptions { Visible = true, Timeout = 0 });

        var playerTime = await GetElementTextContent(page, playerTimeSelector);
        GD.Print($"Initial player__audio__time text: {playerTime}");

        // Wait for the play button to be clickable
        GD.Print("Waiting for the play button...");
        var playButtonSelector = ".player__audio__play";
        await page.WaitForSelectorAsync(playButtonSelector, new WaitForSelectorOptions { Visible = true });

        bool playbackStarted = false;
        while (!playbackStarted && !cancellationToken.IsCancellationRequested)
        {
            await page.ClickAsync(playButtonSelector);
            GD.Print("Trying to start playing...");

            // Wait for a sec
            await Task.Delay(1000);

            // Check if the play button is still present
            var playButton = await page.QuerySelectorAsync(playButtonSelector);
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
            var currentTime = await GetElementTextContent(page, playerTimeSelector);

            if (currentTime != null && currentTime != previousTime)
            {
                previousTime = currentTime;
                if (TryParseMinutesSecondsTimeSpan(currentTime, out TimeSpan negativeTimeSpanRemaining))
                {
                    // note that this is a count DOWN
                    if (ItemDurationMs == 0)
                    {
                        ItemDurationMs = (long)negativeTimeSpanRemaining.TotalMilliseconds;
                        PlaybackDurationChanged?.Invoke(ItemDurationMs.Value);
                    }
                    CurrentPositionMs = (long?)(ItemDurationMs - negativeTimeSpanRemaining.TotalMilliseconds);
                    PlaybackProgress?.Invoke((long)CurrentPositionMs);
                }
                else
                {
                    GD.PrintErr($"Failed to parse player__audio__time text: '{currentTime}'");
                }
            }

            // Break the loop if the player time text becomes empty
            if (currentTime == null || currentTime.Trim() == string.Empty)
            {
                GD.Print("Playback has stopped.");
                break;
            }

            await Task.Delay(1000, cancellationToken) // Small delay (1 second)
                    .ContinueWith(task => { }); // (stfu TaskCanceledException, that's expected)
        }

        if (cancellationToken.IsCancellationRequested)
        {
            GD.Print("Playback was cancelled.");
            await page.GoToAsync("about:blank");
        }
        else
        {
            GD.Print("Playback finished.");
        }
    }

    public override async Task<StatusCheckResult<KarafunStatus>> CheckStatus(IPage page)
    {
        string accountName = null;
        await page.GoToAsync("https://www.karafun.com/my/", WaitUntilNavigation.Networkidle0);

        // if they're logged in, there should be a main header section for their account name 
        var accountTitleWhenLoggedInSelector = ".main__header--account";
        var accountTitleWhenLoggedIn = await page.QuerySelectorAsync(accountTitleWhenLoggedInSelector);
        if (accountTitleWhenLoggedIn != null)
        {
            var accountNameText = await GetInnerTextContent(page, accountTitleWhenLoggedInSelector);
            if (!string.IsNullOrWhiteSpace(accountNameText))
            {
                if (accountNameText.StartsWith("Account"))
                {
                    accountNameText = accountNameText.Substring(7);
                }
                accountName = accountNameText.Replace("\n", "").Trim();
            }
        }

        // if they're not logged in, it would have redirected them to a login screen with the title "Log in to KaraFun"
        var pageTitleWhenNotLoggedInSelector = ".title-section";
        var pageTitleWhenNotLoggedIn = await page.QuerySelectorAsync(pageTitleWhenNotLoggedInSelector);
        if (pageTitleWhenNotLoggedIn != null)
        {
            var titleText = await GetInnerTextContent(page, pageTitleWhenNotLoggedInSelector);
            if (titleText == "Log in to KaraFun")
            {
                return new StatusCheckResult<KarafunStatus>(KarafunStatus.NotLoggedIn, accountName, "Karafun cannot function without an active subscription.");
            }
            else
            {
                return new StatusCheckResult<KarafunStatus>(KarafunStatus.Unknown, accountName, $"Page title found was '{titleText}'");
            }
        }
        
        // if they're logged in, the first subsection should be the text of their account status
        var firstSectionTitleSelector = ".title-section-tiny";
        var firstSectionTitle = await page.QuerySelectorAsync(firstSectionTitleSelector);
        if (firstSectionTitle != null)
        {
            var accountStatusText = await GetParentInnerTextContent(page, firstSectionTitleSelector);
            if (!string.IsNullOrWhiteSpace(accountStatusText))
            {
                if (accountStatusText.Contains("inactive", StringComparison.InvariantCultureIgnoreCase)
                    || accountStatusText.Contains("ended", StringComparison.InvariantCultureIgnoreCase))
                {
                    return new StatusCheckResult<KarafunStatus>(KarafunStatus.Inactive, accountName, accountStatusText);
                }
                else if (accountStatusText.Contains("active", StringComparison.InvariantCultureIgnoreCase))
                {
                    return new StatusCheckResult<KarafunStatus>(KarafunStatus.Active, accountName, accountStatusText);
                }
                else 
                {
                    return new StatusCheckResult<KarafunStatus>(KarafunStatus.Unknown, accountName, $"Account status was not parsed correctly: '{accountStatusText}'");
                }
            }
        }

        return new StatusCheckResult<KarafunStatus>(KarafunStatus.Unknown, accountName, "No sections were recognized.");
    }

}