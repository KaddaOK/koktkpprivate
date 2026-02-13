using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public interface IKarafunAutomator
{
    Task PlayKarafunUrl(IPage page, string url, CancellationToken cancellationToken);
    Task MonitorKarafunWebPlayer(IPage page, CancellationToken cancellationToken);
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

    /// <summary>
    /// Continuously monitor the Karafun web player for playback progress updates.
    /// This is designed to run in the background and only emit events when something is playing.
    /// </summary>
    public async Task MonitorKarafunWebPlayer(IPage page, CancellationToken cancellationToken)
    {
        GD.Print("Starting Karafun web player monitoring...");
        
        // Wait for the player time elements to be present
        var playerTimeSelector = ".select-none.tabular-nums";
        try
        {
            await page.WaitForSelectorAsync(playerTimeSelector, new WaitForSelectorOptions { Visible = true, Timeout = 30000 });
            GD.Print("Karafun player time elements found, monitoring started.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Could not find Karafun player time elements: {ex.Message}");
            return;
        }

        var previousElapsedTime = "";
        var wasPlaying = false;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var timeElements = await page.QuerySelectorAllAsync(playerTimeSelector);
                if (timeElements == null || timeElements.Length < 2)
                {
                    await Task.Delay(1000, cancellationToken).ContinueWith(task => { });
                    continue;
                }

                var time1Text = await GetInnerTextContent(page, timeElements[0]);
                var time2Text = await GetInnerTextContent(page, timeElements[1]);

                // Normalize Unicode minus sign (U+2212) to regular hyphen-minus
                if (time1Text != null)
                {
                    time1Text = time1Text.Replace('−', '-');
                }
                if (time2Text != null)
                {
                    time2Text = time2Text.Replace('−', '-');
                }

                // Check if something is playing (not both 0:00)
                var isPlaying = !((time1Text == "0:00" || time1Text == "-0:00") && 
                                  (time2Text == "0:00" || time2Text == "-0:00"));

                if (isPlaying)
                {
                    if (!wasPlaying)
                    {
                        GD.Print("Karafun playback detected.");
                        wasPlaying = true;
                        ItemDurationMs = 0; // Reset duration for new song
                    }

                    string elapsedTimeText = null;
                    string totalOrRemainingTimeText = null;

                    // Determine which is elapsed and which is remaining/total
                    if (time1Text != null && time1Text.StartsWith("-"))
                    {
                        elapsedTimeText = time2Text;
                        totalOrRemainingTimeText = time1Text.Substring(1);
                    }
                    else if (time2Text != null && time2Text.StartsWith("-"))
                    {
                        elapsedTimeText = time1Text;
                        totalOrRemainingTimeText = time2Text.Substring(1);
                    }
                    else
                    {
                        var time1Parsed = TryParseMinutesSecondsTimeSpan(time1Text, out TimeSpan time1Span);
                        var time2Parsed = TryParseMinutesSecondsTimeSpan(time2Text, out TimeSpan time2Span);
                        if (time1Parsed && time2Parsed)
                        {
                            if (time1Span > time2Span)
                            {
                                totalOrRemainingTimeText = time1Text;
                                elapsedTimeText = time2Text;
                            }
                            else
                            {
                                totalOrRemainingTimeText = time2Text;
                                elapsedTimeText = time1Text;
                            }
                        }
                    }

                    if (elapsedTimeText != null && elapsedTimeText != previousElapsedTime)
                    {
                        previousElapsedTime = elapsedTimeText;

                        if (TryParseMinutesSecondsTimeSpan(elapsedTimeText, out TimeSpan elapsedSpan) &&
                            TryParseMinutesSecondsTimeSpan(totalOrRemainingTimeText, out TimeSpan remainingOrTotalSpan))
                        {
                            long calculatedDuration;
                            if (time1Text != null && time1Text.StartsWith("-") || time2Text != null && time2Text.StartsWith("-"))
                            {
                                calculatedDuration = (long)(elapsedSpan.TotalMilliseconds + remainingOrTotalSpan.TotalMilliseconds);
                            }
                            else
                            {
                                calculatedDuration = (long)remainingOrTotalSpan.TotalMilliseconds;
                            }

                            if (ItemDurationMs == 0 || ItemDurationMs != calculatedDuration)
                            {
                                ItemDurationMs = calculatedDuration;
                                PlaybackDurationChanged?.Invoke(ItemDurationMs.Value);
                            }

                            CurrentPositionMs = (long)elapsedSpan.TotalMilliseconds;
                            PlaybackProgress?.Invoke((long)CurrentPositionMs);
                        }
                    }
                }
                else if (wasPlaying)
                {
                    // Playback stopped
                    GD.Print("Karafun playback stopped (idle).");
                    wasPlaying = false;
                    previousElapsedTime = "";
                    ItemDurationMs = 0;
                    CurrentPositionMs = 0;
                }

                await Task.Delay(1000, cancellationToken).ContinueWith(task => { });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                GD.PrintErr($"Error monitoring Karafun web player: {ex.Message}");
                await Task.Delay(5000, cancellationToken).ContinueWith(task => { });
            }
        }
        
        GD.Print("Karafun web player monitoring stopped.");
    }

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

        // Check if the dual-screen display button needs to be clicked
        GD.Print("Checking for dual-screen display button...");
        var dualScreenButtons = await page.QuerySelectorAllAsync("button[title='Dual-Screen Display']");
        if (dualScreenButtons != null && dualScreenButtons.Length > 0)
        {
            var dualScreenButton = dualScreenButtons[0];
            var hasBrandPrimaryClass = await page.EvaluateFunctionAsync<bool>(@"(button) => {
                return button.classList.contains('border-brand-primary');
            }", dualScreenButton);

            if (!hasBrandPrimaryClass && !cancellationToken.IsCancellationRequested)
            {
                GD.Print("Dual-screen display button needs to be clicked. Clicking...");
                await dualScreenButton.ClickAsync();
            }
            else
            {
                GD.Print("Dual-screen display already active.");
            }
        }

        // Wait for the player time elements to be present
        GD.Print("Waiting for the player time elements...");
        var playerTimeSelector = ".select-none.tabular-nums";
        await page.WaitForSelectorAsync(playerTimeSelector, new WaitForSelectorOptions { Visible = true, Timeout = 0 });

        GD.Print("Initial player time elements found.");

        // Monitor the player time text for changes
        GD.Print("Monitoring playback time...");
        var previousElapsedTime = "";
        var playbackHasStarted = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var timeElements = await page.QuerySelectorAllAsync(playerTimeSelector);
            if (timeElements == null || timeElements.Length < 2)
            {
                GD.PrintErr("Could not find two time elements.");
                break;
            }

            var time1Text = await GetInnerTextContent(page, timeElements[0]);
            var time2Text = await GetInnerTextContent(page, timeElements[1]);

            // Normalize Unicode minus sign (U+2212) to regular hyphen-minus
            if (time1Text != null)
            {
                time1Text = time1Text.Replace('−', '-');
            }
            if (time2Text != null)
            {
                time2Text = time2Text.Replace('−', '-');
            }

            // TODO: this is just a debug print
            //GD.Print($"Player time texts: '{time1Text}', '{time2Text}'");

            // Check if playback has started (at least one value is not 0:00 or -0:00)
            if (!playbackHasStarted && 
                time1Text != "0:00" && time1Text != "-0:00" && 
                time2Text != "0:00" && time2Text != "-0:00")
            {
                playbackHasStarted = true;
                GD.Print("Playback has started.");
            }

            // Check if both times are 0:00 (playback ended) - but only after playback started
            if (playbackHasStarted && 
                (time1Text == "0:00" || time1Text == "-0:00") && 
                (time2Text == "0:00" || time2Text == "-0:00"))
            {
                GD.Print("Both time elements are 0:00. Playback has stopped.");
                break;
            }

            string elapsedTimeText = null;
            string totalOrRemainingTimeText = null;

            // Determine which is elapsed and which is remaining/total
            if (time1Text != null && time1Text.StartsWith("-"))
            {
                // time1 is remaining (negative), time2 is elapsed
                elapsedTimeText = time2Text;
                totalOrRemainingTimeText = time1Text.Substring(1); // Remove the minus sign
            }
            else if (time2Text != null && time2Text.StartsWith("-"))
            {
                // time2 is remaining (negative), time1 is elapsed
                elapsedTimeText = time1Text;
                totalOrRemainingTimeText = time2Text.Substring(1); // Remove the minus sign
            }
            else
            {
                // Neither is negative, so we need to compare them
                var time1Parsed = TryParseMinutesSecondsTimeSpan(time1Text, out TimeSpan time1Span);
                var time2Parsed = TryParseMinutesSecondsTimeSpan(time2Text, out TimeSpan time2Span);
                // TODO: this is just a debug print
                //GD.Print($"Parsed times for comparison: time1Parsed={time1Parsed}, time1Span={time1Span}, time2Parsed={time2Parsed}, time2Span={time2Span}");
                if (time1Parsed && time2Parsed)
                {
                    if (time1Span > time2Span)
                    {
                        // time1 is total, time2 is elapsed
                        totalOrRemainingTimeText = time1Text;
                        elapsedTimeText = time2Text;
                    }
                    else
                    {
                        // time2 is total, time1 is elapsed
                        totalOrRemainingTimeText = time2Text;
                        elapsedTimeText = time1Text;
                    }
                }
            }

            // TODO: this is just a debug print
            //GD.Print($"Parsed times: elapsed='{elapsedTimeText}', other='{totalOrRemainingTimeText}', time1='{time1Text}', time2='{time2Text}'");

            if (elapsedTimeText != null && elapsedTimeText != previousElapsedTime)
            {
                previousElapsedTime = elapsedTimeText;

                if (TryParseMinutesSecondsTimeSpan(elapsedTimeText, out TimeSpan elapsedSpan) &&
                    TryParseMinutesSecondsTimeSpan(totalOrRemainingTimeText, out TimeSpan remainingOrTotalSpan))
                {
                    // Calculate total duration
                    long calculatedDuration;
                    if (time1Text != null && time1Text.StartsWith("-") || time2Text != null && time2Text.StartsWith("-"))
                    {
                        // We have remaining time, so total = elapsed + remaining
                        calculatedDuration = (long)(elapsedSpan.TotalMilliseconds + remainingOrTotalSpan.TotalMilliseconds);
                    }
                    else
                    {
                        // We have total time directly
                        calculatedDuration = (long)remainingOrTotalSpan.TotalMilliseconds;
                    }

                    if (ItemDurationMs == 0 || ItemDurationMs != calculatedDuration)
                    {
                        ItemDurationMs = calculatedDuration;
                        PlaybackDurationChanged?.Invoke(ItemDurationMs.Value);
                    }

                    CurrentPositionMs = (long)elapsedSpan.TotalMilliseconds;
                    PlaybackProgress?.Invoke((long)CurrentPositionMs);
                }
                else
                {
                    GD.PrintErr($"Failed to parse player time texts: elapsed='{elapsedTimeText}', other='{totalOrRemainingTimeText}'");
                }
            }

            // TODO: this is just a debug print


            await Task.Delay(1000, cancellationToken) // Small delay (1 second)
                    .ContinueWith(task => { }); // (stfu TaskCanceledException, that's expected)
        }

        if (cancellationToken.IsCancellationRequested)
        {
            GD.Print("Playback was cancelled. Clicking 'Play Next Song' button...");
            var playNextButtons = await page.QuerySelectorAllAsync("button[title='Play Next Song']");
            if (playNextButtons != null && playNextButtons.Length > 0)
            {
                await playNextButtons[0].ClickAsync();
            }
            else
            {
                GD.PrintErr("Could not find 'Play Next Song' button.");
            }
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