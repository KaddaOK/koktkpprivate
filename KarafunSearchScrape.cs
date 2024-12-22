using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;

public class KarafunSearchScrape
{
    public static async IAsyncEnumerable<KarafunSearchScrapeResponse> Search(string query)
    {
        string url = "https://karafun.com/search/?query=" + HttpUtility.UrlEncode(query);
        await foreach (var searchResponse in GetSearchResults(url))
        {
            yield return searchResponse;
        }
    }

    public static async Task<string> GetDirectPerformanceLinkForSong(string songInfoLink)
    {
        var htmlDoc = await Utils.LoadHtmlResponse(songInfoLink);
        return GetDirectLinkForSong(htmlDoc, songInfoLink);
    }

    private static async IAsyncEnumerable<KarafunSearchScrapeResponse> GetSearchResults(string searchUrl)
    {
        var htmlDoc = await Utils.LoadHtmlResponse(searchUrl);

        var searchResultItemDivs = htmlDoc.DocumentNode.SelectNodes(ResultItemXPath);
        if (searchResultItemDivs == null)
        {
            yield break;
        }

        var initialList = ParseItemDivs(searchResultItemDivs, searchUrl);

        // Determine if there may be more results (generic search only returns 20 results)
        bool mayHaveMore = searchResultItemDivs.Count == 20;

        // Yield initial results
        yield return new KarafunSearchScrapeResponse(initialList) 
        { 
            MayHaveMore = mayHaveMore 
        };

        // Process Artist items
        var allResults = new List<KarafunSearchScrapeResultItem>(initialList);
        foreach (var artistItem in initialList.Where(item => item.ResultType == KarafunSearchScrapeResultItemType.Artist).ToList())
        {
            // Fetch additional results from the artist page
            await foreach (var artistPageResult in GetArtistPageResults(artistItem.ArtistLink))
            {
                yield return artistPageResult;
            }
        }
    }

    private static async IAsyncEnumerable<KarafunSearchScrapeResponse> GetArtistPageResults(string artistUrl)
    {
        // get the first page of results
        var htmlDoc = await Utils.LoadHtmlResponse(artistUrl);
        var artistResultItemDivs = htmlDoc.DocumentNode.SelectNodes(ResultItemXPath);
        if (artistResultItemDivs == null)
        {
            yield break;
        }

        var results = ParseItemDivs(artistResultItemDivs, artistUrl);

        // Yield initial artist page results
        yield return new KarafunSearchScrapeResponse(results) { PartOfArtistSet = artistUrl };

        // add results from subsequent pagination
        var nextUrl = GetNextPageUrlOrNull(htmlDoc, artistUrl);
        while (nextUrl != null)
        {
            var nextDoc = await Utils.LoadHtmlResponse(nextUrl);
            var nextResultItemDivs = nextDoc.DocumentNode.SelectNodes(ResultItemXPath);
            if (nextResultItemDivs != null)
            {
                var nextResults = ParseItemDivs(nextResultItemDivs, nextUrl);
                results.AddRange(nextResults);

                // Yield paginated results
                yield return new KarafunSearchScrapeResponse(nextResults) { PartOfArtistSet = artistUrl };
            }
            nextUrl = GetNextPageUrlOrNull(nextDoc, nextUrl);
        }
    }

    private static List<KarafunSearchScrapeResultItem> ParseItemDivs(HtmlNodeCollection itemDivs, string originalAbsoluteUrl)
    {
        var results = new List<KarafunSearchScrapeResultItem>();

        foreach (var itemDiv in itemDivs)
        {
            // Skip "Suggestion" items
            if (itemDiv.HasClass("song--suggest")) continue;

            var resultItem = new KarafunSearchScrapeResultItem();

            // Determine ResultType
            if (itemDiv.HasClass("song--forbidden"))
            {
                //resultItem.ResultType = KarafunSearchScrapeResultItemType.UnlicensedSong;
                continue; // Skip unlicensed songs
            }
            else
            {
                var artistElement = itemDiv.SelectSingleNode(".//div[contains(@class, 'song__title-container')]//*[contains(@class, 'song__artist')]");
                if (artistElement != null && artistElement.Name == "p")
                {
                    resultItem.ResultType = KarafunSearchScrapeResultItemType.Artist;
                }
                else
                {
                    resultItem.ResultType = KarafunSearchScrapeResultItemType.AvailableSong;
                }
            }

            // Extract details from detailsdiv
            var detailsDiv = itemDiv.SelectSingleNode(".//div[contains(@class, 'song__title-container')]");
            if (detailsDiv != null)
            {
                // Extract ArtistName and ArtistLink
                var artistElement = detailsDiv.SelectSingleNode(".//*[contains(@class, 'song__artist')]");
                if (artistElement != null)
                {
                    if (artistElement.Name == "p")
                    {
                        resultItem.ArtistName = artistElement.InnerText.Trim();
                    }
                    else if (artistElement.Name == "a")
                    {
                        resultItem.ArtistName = artistElement.InnerText.Trim();
                        resultItem.ArtistLink = Utils.EnsureAbsoluteUrl(artistElement.GetAttributeValue("href", string.Empty), originalAbsoluteUrl);
                    }
                }

                // Extract SongName and SongLink
                var titleElement = detailsDiv.SelectSingleNode(".//*[contains(@class, 'song__title')]");
                if (titleElement != null && titleElement.Name == "a")
                {
                    if (resultItem.ResultType == KarafunSearchScrapeResultItemType.Artist)
                    {
                        resultItem.ArtistName = titleElement.InnerText.Trim();
                        resultItem.ArtistLink = Utils.EnsureAbsoluteUrl(titleElement.GetAttributeValue("href", string.Empty), originalAbsoluteUrl);
                    }
                    else
                    {
                        resultItem.SongName = titleElement.InnerText.Trim();
                        resultItem.SongInfoLink = Utils.EnsureAbsoluteUrl(titleElement.GetAttributeValue("href", string.Empty), originalAbsoluteUrl);
                    }
                }
            }

            results.Add(resultItem);
        }
        
        return results;
    }

    private static string ResultItemXPath = "//div[contains(@class, 'song') and contains(@class, 'song-line')]";


    private static string GetDirectLinkForSong(HtmlDocument htmlDoc, string baseUrl)
    {
        // Find the <a> tag with an href that starts with "/web/?song="
        var songAnchor = htmlDoc.DocumentNode.SelectSingleNode("//a[starts-with(@href, '/web/?song=')]");
        if (songAnchor == null)
        {
            return null; // Return null if no matching anchor is found
        }

        // Extract the href attribute
        var relativeHref = songAnchor.GetAttributeValue("href", null);
        if (string.IsNullOrEmpty(relativeHref))
        {
            return null; // Return null if the href is missing or empty
        }

        // Construct the full URL using the base URL
        return Utils.EnsureAbsoluteUrl(relativeHref, baseUrl);
    }

    private static string GetNextPageUrlOrNull(HtmlDocument htmlDoc, string originalAbsoluteUrl)
    {
        // Look for the pagination div
        var paginationDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pagination')]");
        if (paginationDiv == null) return null;

        // Look for the next anchor within the pagination div
        var nextAnchor = paginationDiv.SelectSingleNode(".//a[contains(@class, 'next')]");
        if (nextAnchor == null) return null;

        // Return the href of the next anchor, if found
        var newUrl = nextAnchor.GetAttributeValue("href", null);
        
        // it's probably relative and we need an absolute URL in order to make a new request
        return Utils.EnsureAbsoluteUrl(newUrl, originalAbsoluteUrl);
    }

}

public enum KarafunSearchScrapeResultItemType
{
    /// <summary>
    /// A song that can actually be sung on Karafun.
    /// </summary>
	AvailableSong,

    /// <summary>
    /// Karafun tells you outright when they've lost the license to a song. These 
    /// should be sorted to the bottom and are unable to be queued.
    /// </summary>
	UnlicensedSong,

    /// <summary>
    /// When Karafun feels an entire artist is relevant to the search results, you
    /// get one of these.  Caller should not show these, but rather replace them 
    /// with subsequent yields that are paginations of the artist's page in the 
    /// Karafun catalog.
    /// </summary>
	Artist,

    /// <summary>
    /// Karafun also tells you when they don't have a song but know that people 
    /// have requested it.  I don't show these anywhere so I'm not sure why I 
    /// included it in this enum
    /// </summary>
	Suggestion
}
public class KarafunSearchScrapeResultItem
{
	public string SongName { get; set; }

    /// <summary>
    /// This is not the performance launch link, it's to an info page that has the 
    /// performance launch link on it.  Call GetDirectLinkForSong to get the actual
    /// performance launch link.
    /// </summary>
	public string SongInfoLink { get; set; }
	public string ArtistName { get; set; }
	public string ArtistLink { get; set; }
	public KarafunSearchScrapeResultItemType ResultType { get; set; }
}

/// <summary>
/// Represents a page of results of a search query.
/// </summary>
public class KarafunSearchScrapeResponse
{
    /// <summary>
    /// Indicates whether there were 20 search results on the initial page, which 
    /// is the maximum that Karafun will return for a general query.  (One or more 
    /// of these 20 may have been an "Artist" item, in which case subsequent yields
    /// of results will be for the artist page, which is how you ever get more than 
    /// 20 results.)
    /// </summary>
    public bool MayHaveMore { get; set; }

    /// <summary>
    /// If this is not null, it indicates that the results are from the artist page 
    /// identified by the URL in this property.  The artist item in the original 
    /// results should not be displayed, but used as a placeholder for where in 
    /// the original 20 results Karafun thought the artist as a whole's relevance 
    /// belonged.  Result sets yielded should therefore be inserted in at that 
    /// particular point, with the recognition that there may be more coming.
    /// </summary>
	public string PartOfArtistSet { get; set;}
	public List<KarafunSearchScrapeResultItem> Results { get; set; }
	public KarafunSearchScrapeResponse() 
	{
		Results = new List<KarafunSearchScrapeResultItem>();
	}
	public KarafunSearchScrapeResponse(List<KarafunSearchScrapeResultItem> results)
	{
		Results = results;
	}
}

