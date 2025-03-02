using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public interface IYoutubeAutomator
{
    Task PlayYoutubeUrl(IPage page, string url, CancellationToken cancellationToken);
    Task ToggleYoutubePlayback(IPage youtubePage);
    Task Seek(IPage youtubePage, long positionMs);

    event PlaybackProgressEventHandler PlaybackProgress;
    event PlaybackDurationChangedEventHandler PlaybackDurationChanged;
}

public enum YouTubeStatus
{
    NotLoggedIn,
    Premium,
    NotPremium,
    Unknown
}

public class YoutubeAutomator : WebAutomatorBase<YouTubeStatus>, IYoutubeAutomator
{
    public string CurrentPath { get; private set; }
    public long? CurrentPositionMs { get; private set; }
    public long? ItemDurationMs { get; private set; }

    public event PlaybackProgressEventHandler PlaybackProgress;
    public event PlaybackDurationChangedEventHandler PlaybackDurationChanged;

    public async Task ToggleYoutubePlayback(IPage youtubePage)
    {
        if (youtubePage != null)
        {
            var playPauseButton = await youtubePage.QuerySelectorAsync(".ytp-play-button");
            if (playPauseButton != null)
            {
                await playPauseButton.ClickAsync();
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
                await SetElementFieldValue(page, ".video-stream", "currentTime", positionMs / 1000.0);
            }
        }
    }

    public async Task PlayYoutubeUrl(IPage page, string url, CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        GD.Print("Navigating to the video page...");
        await page.GoToAsync(
            $"{url}", // why? idk; sometimes it fails "failed to deserialize params.url" for no reason
        WaitUntilNavigation.DOMContentLoaded);
        GD.Print($"GoToAsync DOMContentLoaded after {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Restart();

        // Wait for the video-stream to be present
        GD.Print("Waiting for the video stream...");
        await page.WaitForSelectorAsync(".video-stream");
        GD.Print($"Video stream appeared after another {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Restart();

        // Wait for the play/pause button to be present
        GD.Print("Waiting for the play button...");
        await page.WaitForSelectorAsync(".ytp-play-button");
        GD.Print($"Play button appeared after another {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Restart();

        // Wait for the fullscreen button to be present
        GD.Print("Waiting for the fullscreen button...");
        await page.WaitForSelectorAsync(".ytp-fullscreen-button");
        GD.Print($"Fullscreen button appeared after another {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Restart();

        // Check if the video is playing
        var playButton = await page.QuerySelectorAsync(".ytp-play-button");
        var playButtonTitle = await GetElementAttributeValue(page, ".ytp-play-button", "data-title-no-tooltip");
        //GD.Print("Play button title: " + playButtonTitle);
        if (playButtonTitle == "Play" && !cancellationToken.IsCancellationRequested)
        {
            GD.Print("Play button needs to be clicked. Clicking...");
            await playButton.ClickAsync();
        }

        // Check if the video is fullscreen
        var fullscreenButton = await page.QuerySelectorAsync(".ytp-fullscreen-button");
        var fullscreenButtonTitle = await GetElementAttributeValue(page, ".ytp-fullscreen-button", "data-title-no-tooltip");
        if (fullscreenButtonTitle == "Full screen" && !cancellationToken.IsCancellationRequested)
        {
            GD.Print("Fullscreen button needs to be clicked. Clicking...");
            await fullscreenButton.ClickAsync();
        }

        // Disable autoplay for the next video
        var autoplayChecked = await GetElementAttributeValue(page, ".ytp-autonav-toggle-button", "aria-checked");
        if (autoplayChecked == "true")
        {
            GD.Print("Autoplay toggle needs to be clicked. Clicking...");
            var autoplayButton = await page.QuerySelectorAsync(".ytp-button:has(.ytp-autonav-toggle-button)");
            if (autoplayButton == null)
            {
                GD.Print("Autoplay button not found even though we JUST found it to be aria-checked=true, that makes no sense...");
            }
            else
            {
                await autoplayButton.ClickAsync();
            }
        }

        // let it play
        ItemDurationMs = null;
        CurrentPositionMs = null;
        string previousTimeDisplay = null;
        double? previousTimeSeconds = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            // get the duration if we don't already have it 
            if (ItemDurationMs == null)
            {
                var duration = await GetElementFieldValue<double?>(page, ".video-stream", "duration"); 
                if (duration != null)
                {
                    ItemDurationMs = (long)(duration * 1000);
                    GD.Print($"Invoking PlaybackDurationChanged with {ItemDurationMs.Value}");
                    PlaybackDurationChanged?.Invoke(ItemDurationMs.Value);
                }
                else
                {
                    GD.Print("Couldn't get duration, trying backup method:");
                    var durationDisplay = await GetElementTextContent(page, ".ytp-time-duration");
                    if (durationDisplay != null && TryParseMinutesSecondsTimeSpan(durationDisplay, out TimeSpan durationTimespan))
                    {
                        ItemDurationMs = (long)durationTimespan.TotalMilliseconds;
                        GD.Print($"Invoking PlaybackDurationChanged with {ItemDurationMs.Value}");
                        PlaybackDurationChanged?.Invoke(ItemDurationMs.Value);
                    }
                }
            }

            var currentTimeSeconds = await GetElementFieldValue<double?>(page, ".video-stream", "currentTime"); 
            if (currentTimeSeconds != null)
            {
                if (currentTimeSeconds != previousTimeSeconds)
                {
                    previousTimeSeconds = currentTimeSeconds;
                    CurrentPositionMs = (long)(currentTimeSeconds * 1000);
                    GD.Print($"Invoking PlaybackProgress with {CurrentPositionMs.Value}");
                    PlaybackProgress?.Invoke(CurrentPositionMs.Value);
                }
            }
            else 
            {
                // you can try to get the current time, but it only updates if it's visible :(
                var currentTimeDisplay = await GetElementTextContent(page, ".ytp-time-current");
                GD.Print("Current time: " + currentTimeDisplay);
                if (currentTimeDisplay != null && currentTimeDisplay != previousTimeDisplay)
                {
                    previousTimeDisplay = currentTimeDisplay;
                    if (TryParseMinutesSecondsTimeSpan(currentTimeDisplay, out TimeSpan currentTimespan))
                    {
                        CurrentPositionMs = (long)currentTimespan.TotalMilliseconds;
                        GD.Print($"Invoking PlaybackProgress with {CurrentPositionMs.Value}");
                        PlaybackProgress?.Invoke(CurrentPositionMs.Value);
                    }
                }
            }

            // grab the play button title again
            playButtonTitle = await GetElementAttributeValue(page, ".ytp-play-button", "data-title-no-tooltip");
            if (playButtonTitle == "Replay")
            {
                GD.Print("Play button text shows that playback is finished.");
                break;
            }
            if (CurrentPositionMs >= ItemDurationMs)
            {
                GD.Print("CurrentPositionMs shows that playback is finished.");
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
        CurrentPositionMs = null;
        ItemDurationMs = null;
    }

    public override async Task<StatusCheckResult<YouTubeStatus>> CheckStatus(IPage page)
    {
        string accountName = null;
        await page.GoToAsync("https://www.youtube.com/premium_benefits", WaitUntilNavigation.Networkidle0);

        // if they're logged in, there should be an avatar button
        var avatarButton = await page.QuerySelectorAsync("#avatar-btn");
        
        if (avatarButton == null)
        {
            return new StatusCheckResult<YouTubeStatus>(YouTubeStatus.NotLoggedIn, null, null);
        }
        
        // have to click on it to get the account name items to materialize
        await avatarButton.ClickAsync();
        await page.WaitForNetworkIdleAsync();
        
        // there are two possibilities: an email address, or an account handle
        var emailElement = await page.QuerySelectorAsync("#email");
        if (emailElement != null)
        {
            accountName = await GetInnerTextContent(page, "#email");
        }
        if (string.IsNullOrWhiteSpace(accountName))
        {
            var channelHandleElement = await page.QuerySelectorAsync("#channel-handle");
            if (channelHandleElement != null)
            {
                accountName = await GetInnerTextContent(page, "#channel-handle");
            }
        }

        var premiumBadgeSelector = ".badge[aria-label='Premium']";
        var premiumBadge = await page.QuerySelectorAsync(premiumBadgeSelector);
        var getPremiumButtonSelector = "a[aria-label='Get Premium']";
        var getPremiumButton = await page.QuerySelectorAsync(getPremiumButtonSelector);
        if (premiumBadge != null)
        {
            if (getPremiumButton != null)
            {
                return new StatusCheckResult<YouTubeStatus>(YouTubeStatus.Unknown, accountName, "Detected both premium and non-premium indicators");
            }
            return new StatusCheckResult<YouTubeStatus>(YouTubeStatus.Premium, accountName, null);
        }
        
        if (getPremiumButton != null)
        {
            return new StatusCheckResult<YouTubeStatus>(YouTubeStatus.NotPremium, accountName, null);
        }
        
        return new StatusCheckResult<YouTubeStatus>(YouTubeStatus.Unknown, accountName, "User seemed to be logged in but neither premium nor non-premium indications were detected");
    }

}