using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HtmlAgilityPack;
using PuppeteerSharp;

public class Utils
{
    public static string GetAppStoragePath()
    {
        return ProjectSettings.GlobalizePath("user://");
    }
    
#region TODO this is all stupid
    private static IBrowserFetcher GetBrowserFetcher()
    {
        var downloadPath = ProjectSettings.GlobalizePath("user://browser");
        return Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
        {
            Browser = SupportedBrowser.Chromium,
            Path = downloadPath
        });
    }

    public static async Task<string> CheckForBrowser()
    {
        return await CheckForBrowser(GetBrowserFetcher());
    }

    public static async Task<string> CheckForBrowser(IBrowserFetcher fetcher)
    {
        return await Task.Run(() => { 
            var fetcher = GetBrowserFetcher();
            var installedBrowsers = fetcher.GetInstalledBrowsers();
            var chromiumRevision = installedBrowsers.FirstOrDefault(a => a.Browser == SupportedBrowser.Chromium)?.BuildId;
            return chromiumRevision;
        });
    }

    public static async Task<string> EnsureBrowser()
    {
        GD.Print("Ensuring browser...");
        var fetcher = GetBrowserFetcher();
        var chromiumRevision = await CheckForBrowser(fetcher);

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

#endregion

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

    public static string GetAllPossibleExceptionInfo(Exception ex, StringBuilder builder = null)
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
}