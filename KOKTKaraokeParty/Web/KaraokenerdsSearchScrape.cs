using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

public class KaraokenerdsSearchScrape
{
    public static async Task<List<KNSearchResultItem>> Search(string query)
    {
        string url = $"https://karaokenerds.com/Search?query={HttpUtility.UrlEncode(query)}&webFilter=OnlyWeb";
        return await GetSearchResults(url);
    }

private static async Task<List<KNSearchResultItem>> GetSearchResults(string searchUrl)
    {
        var htmlDoc = await Utils.LoadHtmlResponse(searchUrl);
        var results = new List<KNSearchResultItem>();

        // Select all rows in the table body
        var rows = htmlDoc.DocumentNode.SelectNodes("//table/tbody/tr");
        if (rows == null)
        {
            return results;
        }

        KNSearchResultItem currentItem = null;

        foreach (var row in rows)
        {
            if (row.HasClass("group"))
            {
                var songName = row.SelectSingleNode("td[1]/a")?.InnerText.Trim();
                var artistName = row.SelectSingleNode("td[2]/a")?.InnerText.Trim();

                currentItem = new KNSearchResultItem
                {
                    SongName = songName,
                    ArtistName = artistName
                };
            }
            else if (row.HasClass("details") && currentItem != null)
            {
                var listItems = row.SelectNodes("td/ul/li");
                if (listItems != null)
                {
                    foreach (var listItem in listItems)
                    {
                        var creatorBrandName = listItem.SelectSingleNode("a[1]")?.InnerText.Trim();
                        var youtubeLinkNode = listItem.SelectSingleNode("div/a[contains(@href, 'youtube.com')]");

                        if (youtubeLinkNode != null)
                        {
                            var youtubeLink = youtubeLinkNode.GetAttributeValue("href", string.Empty);

                            var resultItem = new KNSearchResultItem
                            {
                                SongName = HttpUtility.HtmlDecode(currentItem.SongName),
                                ArtistName = HttpUtility.HtmlDecode(currentItem.ArtistName),
                                CreatorBrandName = HttpUtility.HtmlDecode(creatorBrandName),
                                YoutubeLink = youtubeLink
                            };

                            results.Add(resultItem);
                        }
                    }
                }
            }
        }

        return results;
    }
}

public class KNSearchResultItem
{
	public string SongName { get; set; }
	public string ArtistName { get; set; }
	public string CreatorBrandName { get; set; }
    public string YoutubeLink { get; set;}
}

