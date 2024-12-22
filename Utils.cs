using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Godot;
using HtmlAgilityPack;
using PuppeteerSharp;

public class Utils
{
    public static string GetAppStoragePath()
    {
        return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "KOKTKaraokeParty");
    }
    public static async Task<string> EnsureBrowser()
    {
        GD.Print("Ensuring browser...");
        var fetcher = Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
        {
            Browser = SupportedBrowser.Chromium
        });
        var installedBrowsers = fetcher.GetInstalledBrowsers();
        GD.Print($"Installed browsers: {string.Join(", ", installedBrowsers.Select(a => $"{a.Browser} {a.BuildId}"))}");
        var chromiumRevision = installedBrowsers.FirstOrDefault(a => a.Browser == SupportedBrowser.Chromium)?.BuildId;

        if (chromiumRevision == null)
        {
            GD.Print("Downloading Chromium...");
            var revisionInfo = await fetcher.DownloadAsync();
            chromiumRevision = revisionInfo.BuildId;
        }

        var path = fetcher.GetExecutablePath(chromiumRevision);
        GD.Print($"Browser is ready ({chromiumRevision} at {path}).");
        return path;
    }

    public static string EnsureAbsoluteUrl(string maybeRelativeUrl, string previousAbsoluteUrl)
    {
        return new Uri(new Uri(previousAbsoluteUrl), maybeRelativeUrl).ToString();
    }

    public static async Task<HtmlDocument> LoadHtmlResponse(string url)
    {
        using System.Net.Http.HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode(); // Ensure we got a valid response
        string htmlContent = await response.Content.ReadAsStringAsync();

        HtmlDocument htmlDoc = new();
        htmlDoc.LoadHtml(htmlContent);
        
        return htmlDoc;
    }
}