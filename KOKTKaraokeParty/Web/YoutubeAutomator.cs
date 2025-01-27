using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PuppeteerSharp;

public interface IYoutubeAutomator
{
    Task PlayYoutubeUrl(IPage page, string url, CancellationToken cancellationToken);
    Task ToggleYoutubePlayback(IPage youtubePage);
}
public class YoutubeAutomator : IYoutubeAutomator
{
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

    public async Task PlayYoutubeUrl(IPage page, string url, CancellationToken cancellationToken)
    {
        GD.Print("Navigating to the video page...");
        await page.GoToAsync(
            $"{url}", // why? idk; sometimes it fails "failed to deserialize params.url" for no reason
        WaitUntilNavigation.Networkidle2);

        // Wait for the play/pause button to be present
        GD.Print("Waiting for the play button...");
        await page.WaitForSelectorAsync(".ytp-play-button");

        // Wait for the fullscreen button to be present
        GD.Print("Waiting for the fullscreen button...");
        await page.WaitForSelectorAsync(".ytp-fullscreen-button");

        // Check if the video is fullscreen
        var fullscreenButton = await page.QuerySelectorAsync(".ytp-fullscreen-button");
        var fullscreenButtonTitle = await GetAttributeValue(page, ".ytp-fullscreen-button", "data-title-no-tooltip");
        if (fullscreenButtonTitle == "Full screen" && !cancellationToken.IsCancellationRequested)
        {
            GD.Print("Fullscreen button needs to be clicked. Clicking...");
            await fullscreenButton.ClickAsync();
        }

        // Check if the video is playing
        var playButton = await page.QuerySelectorAsync(".ytp-play-button");
        var playButtonTitle = await GetAttributeValue(page, ".ytp-play-button", "data-title-no-tooltip");
        //GD.Print("Play button title: " + playButtonTitle);
        if (playButtonTitle == "Play" && !cancellationToken.IsCancellationRequested)
        {
            GD.Print("Play button needs to be clicked. Clicking...");
            await playButton.ClickAsync();
        }

        // Disable autoplay for the next video
        var autoplayButton = await page.QuerySelectorAsync(".ytp-autonav-toggle-button");
        var autoplayChecked = await GetAttributeValue(page, ".ytp-autonav-toggle-button", "aria-checked");
        if (autoplayChecked == "true")
        {
            GD.Print("Autoplay toggle needs to be clicked. Clicking...");
            await autoplayButton.ClickAsync();
        }

        // let it play
        while (!cancellationToken.IsCancellationRequested)
        {
            // grab the play button title again
            playButtonTitle = await GetAttributeValue(page, ".ytp-play-button", "data-title-no-tooltip");
            if (playButtonTitle == "Replay")
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

    private string GetAttributeFunction(string attribute)
    {
        return $@"(selector) => {{
			const el = document.querySelector(selector);
			return el ? el.getAttribute('{attribute}') : null;
		}}";
    }
    private async Task<string> GetAttributeValue(IPage page, string selector, string attribute, bool log = false)
    {
        var result = await page.EvaluateFunctionAsync<string>(GetAttributeFunction(attribute), selector);
        if (log)
        {
            GD.Print($"{selector} {attribute}=\"{result}\"");
        }
        return result;
    }
}