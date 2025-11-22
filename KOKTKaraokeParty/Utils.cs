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