using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HtmlAgilityPack;
using Newtonsoft.Json;
using PuppeteerSharp;

public class Utils
{
    public static string GetAppStoragePath()
    {
        return ProjectSettings.GlobalizePath("user://");
    }

    public static string GetSavedQueueFilePath()
    {
        return Path.Combine(GetAppStoragePath(), "queue.json");
    }

    /// <summary>
    /// Reads saved queue items from disk without modifying any state.
    /// Returns an empty list if no saved queue exists or if there's an error.
    /// </summary>
    public static List<QueueItem> GetSavedQueueItemsFromDisk()
    {
        try
        {
            var filePath = GetSavedQueueFilePath();
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var items = JsonConvert.DeserializeObject<QueueItem[]>(json);
                return items?.ToList() ?? new List<QueueItem>();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to read saved queue for preview: {ex.Message}");
        }
        return new List<QueueItem>();
    }

    /// <summary>
    /// Deletes the saved queue file if it exists.
    /// </summary>
    public static void DeleteSavedQueueFile()
    {
        try
        {
            var filePath = GetSavedQueueFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                GD.Print("Deleted saved queue file.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to delete saved queue file: {ex.Message}");
        }
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