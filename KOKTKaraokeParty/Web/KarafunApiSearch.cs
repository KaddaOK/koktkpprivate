using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Godot;

/// <summary>
/// Searches Karafun using their internal API endpoint (requires a valid room code).
/// This is the same API that their remote control web client uses.
/// Returns up to 50 results per page, with pagination support for more.
/// </summary>
public static class KarafunApiSearch
{
    private const int PageSize = 50;
    private const string KarafunBaseUrl = "https://www.karafun.com";
    
    private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
    
    /// <summary>
    /// Search for songs using the Karafun API.
    /// Streams results in batches of 50 as they are retrieved.
    /// </summary>
    /// <param name="roomCode">A valid 6-digit Karafun room code</param>
    /// <param name="query">The search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of search results</returns>
    public static async IAsyncEnumerable<KarafunApiSearchResult> Search(
        string roomCode, 
        string query, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
        {
            GD.PrintErr($"Invalid room code for Karafun API search: {roomCode}");
            yield break;
        }
        
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }
        
        int offset = 0;
        int totalRetrieved = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await FetchPageAsync(roomCode, query, offset, cancellationToken);
            
            if (response == null || response.Songs == null || response.Songs.Count == 0)
            {
                break;
            }
            
            totalRetrieved += response.Songs.Count;
            GD.Print($"Karafun API: Retrieved {response.Songs.Count} songs (offset {offset}, total so far: {totalRetrieved})");
            
            yield return response;
            
            // If we got fewer than PageSize results, we've reached the end
            if (response.Songs.Count < PageSize)
            {
                break;
            }
            
            offset += PageSize;
        }
    }
    
    private static async Task<KarafunApiSearchResult> FetchPageAsync(
        string roomCode, 
        string query, 
        int offset, 
        CancellationToken cancellationToken)
    {
        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"{KarafunBaseUrl}/{roomCode}/?type=song_list&filter=sc_{encodedQuery}&offset={offset}";
            
            GD.Print($"Karafun API search: {url}");
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<KarafunApiSearchResult>(json);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Karafun API search failed: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Response from the Karafun API search endpoint
/// </summary>
public class KarafunApiSearchResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("songs")]
    public List<KarafunApiSong> Songs { get; set; }
}

/// <summary>
/// A song from the Karafun API
/// </summary>
public class KarafunApiSong
{
    [JsonPropertyName("songId")]
    public int SongId { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("artist")]
    public string Artist { get; set; }
    
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    
    [JsonPropertyName("key")]
    public string Key { get; set; }
    
    [JsonPropertyName("year")]
    public int? Year { get; set; }
    
    [JsonPropertyName("img")]
    public string ImageUrl { get; set; }
    
    [JsonPropertyName("isExplicit")]
    public bool IsExplicit { get; set; }
}
