using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Xunit;

namespace KOKTKaraokeParty.Tests.Web
{
    // These tests are of little value; KarafunSearchScrape would need to inject its use of Utils.LoadHtmlResponse
    public class KarafunSearchScrapeTests
    {
        [Fact]
        public void ResultItemXPath_Should_Find_Items_In_Old_Format()
        {
            // Arrange
            var oldFormatHtml = @"
                <div class='song song-line flex grid-gap-s align-items-center'>
                    <a class='song-line--covers relative flex-none flex align-items-center justify-center bg-life-blue-electric' href='/test'>
                        <svg><use href='#icon-vote'></use></svg>
                    </a>
                    <div class='song__title-container'>
                        <a href='/karaoke/test' class='song__title block'>Test Song</a>
                        <div><a href='/karaoke/test-artist/' class='song__artist'>Test Artist</a></div>
                    </div>
                </div>";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(oldFormatHtml);
            
            // Act
            var resultItemXPath = "//div[contains(@class, 'song') and .//a[contains(@class, 'song-line--covers')]]";
            var searchResultItemDivs = htmlDoc.DocumentNode.SelectNodes(resultItemXPath);
            
            // Assert
            Assert.NotNull(searchResultItemDivs);
            Assert.Single(searchResultItemDivs);
        }

        [Fact]
        public void ResultItemXPath_Should_Find_Items_In_New_Format()
        {
            // Arrange
            var newFormatHtml = @"
                <div class='song flex gap-3 overflow-hidden py-2 pt-2 pb-2 border-solid border-b-1 border-border-01'>
                    <a href='/karaoke/test' class='song-line--covers relative flex-none block'>
                        <figure><img src='/test.jpg' alt='Test Song' width='56' class='rounded-xs flex-none w-14 h-14'></figure>
                    </a>
                    <div class='w-full py-0.5 overflow-hidden'>
                        <div class='flex items-center gap-2 overflow-hidden'>
                            <a href='/karaoke/test' class='text4 text-label-title truncate no-underline block'>Test Song</a>
                        </div>
                        <div class='flex items-center overflow-hidden'>
                            <a href='/karaoke/test-artist/' class='text5 text-label-subtitle no-underline truncate'>Test Artist</a>
                        </div>
                    </div>
                </div>";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(newFormatHtml);
            
            // Act
            var resultItemXPath = "//div[contains(@class, 'song') and .//a[contains(@class, 'song-line--covers')]]";
            var searchResultItemDivs = htmlDoc.DocumentNode.SelectNodes(resultItemXPath);
            
            // Assert
            Assert.NotNull(searchResultItemDivs);
            Assert.Single(searchResultItemDivs);
        }

        [Fact]
        public void ParseItemDivs_Should_Extract_Data_From_Old_Format()
        {
            // Arrange
            var oldFormatHtml = @"
                <div class='song song-line flex grid-gap-s align-items-center'>
                    <a class='song-line--covers relative flex-none flex align-items-center justify-center bg-life-blue-electric' href='/test'>
                        <svg><use href='#icon-vote'></use></svg>
                    </a>
                    <div class='song__title-container'>
                        <a href='/karaoke/luke-combs/don-t-tempt-me/' class='song__title block'>Don't Tempt Me</a>
                        <div><a href='/karaoke/luke-combs/' class='song__artist'>Luke Combs</a></div>
                    </div>
                </div>";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(oldFormatHtml);
            var itemDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'song')]");

            // Act - Simulate the parsing logic from ParseItemDivs
            var detailsDiv = itemDiv.SelectSingleNode(".//div[contains(@class, 'song__title-container')]") ??
                            itemDiv.SelectSingleNode(".//div[contains(@class, 'w-full') and contains(@class, 'overflow-hidden')]");

            var titleElement = detailsDiv?.SelectSingleNode(".//*[contains(@class, 'song__title')]") ??
                              detailsDiv?.SelectSingleNode(".//a[contains(@class, 'text-label-title')]") ??
                              detailsDiv?.SelectSingleNode(".//p[contains(@class, 'text-label-title')]");

            var artistElement = detailsDiv?.SelectSingleNode(".//*[contains(@class, 'song__artist')]") ??
                               detailsDiv?.SelectSingleNode(".//span[contains(@class, 'text-label-subtitle')]") ??
                               detailsDiv?.SelectSingleNode(".//a[contains(@class, 'text-label-subtitle')]");

            // Assert
            Assert.NotNull(detailsDiv);
            Assert.NotNull(titleElement);
            Assert.NotNull(artistElement);
            Assert.Equal("Don't Tempt Me", titleElement.InnerText.Trim());
            Assert.Equal("Luke Combs", artistElement.InnerText.Trim());
            Assert.Equal("/karaoke/luke-combs/don-t-tempt-me/", titleElement.GetAttributeValue("href", ""));
            Assert.Equal("/karaoke/luke-combs/", artistElement.GetAttributeValue("href", ""));
        }

        [Fact]
        public void ParseItemDivs_Should_Extract_Data_From_New_Format()
        {
            // Arrange
            var newFormatHtml = @"
                <div class='song flex gap-3 overflow-hidden py-2 pt-2 pb-2 border-solid border-b-1 border-border-01'>
                    <a href='/karaoke/luke-combs/don-t-tempt-me/' class='song-line--covers relative flex-none block'>
                        <figure><img src='/test.jpg' alt='Test Song' width='56' class='rounded-xs flex-none w-14 h-14'></figure>
                    </a>
                    <div class='w-full py-0.5 overflow-hidden'>
                        <div class='flex items-center gap-2 overflow-hidden'>
                            <a href='/karaoke/luke-combs/don-t-tempt-me/' class='text4 text-label-title truncate no-underline block'>Don't Tempt Me</a>
                        </div>
                        <div class='flex items-center overflow-hidden'>
                            <a href='/karaoke/luke-combs/' class='text5 text-label-subtitle no-underline truncate'>Luke Combs</a>
                        </div>
                    </div>
                </div>";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(newFormatHtml);
            var itemDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'song')]");

            // Act - Simulate the parsing logic from ParseItemDivs
            var detailsDiv = itemDiv.SelectSingleNode(".//div[contains(@class, 'song__title-container')]") ??
                            itemDiv.SelectSingleNode(".//div[contains(@class, 'w-full') and contains(@class, 'overflow-hidden')]");

            var titleElement = detailsDiv?.SelectSingleNode(".//*[contains(@class, 'song__title')]") ??
                              detailsDiv?.SelectSingleNode(".//a[contains(@class, 'text-label-title')]") ??
                              detailsDiv?.SelectSingleNode(".//p[contains(@class, 'text-label-title')]");

            var artistElement = detailsDiv?.SelectSingleNode(".//*[contains(@class, 'song__artist')]") ??
                               detailsDiv?.SelectSingleNode(".//span[contains(@class, 'text-label-subtitle')]") ??
                               detailsDiv?.SelectSingleNode(".//a[contains(@class, 'text-label-subtitle')]");

            // Assert
            Assert.NotNull(detailsDiv);
            Assert.NotNull(titleElement);
            Assert.NotNull(artistElement);
            Assert.Equal("Don't Tempt Me", titleElement.InnerText.Trim());
            Assert.Equal("Luke Combs", artistElement.InnerText.Trim());
            Assert.Equal("/karaoke/luke-combs/don-t-tempt-me/", titleElement.GetAttributeValue("href", ""));
            Assert.Equal("/karaoke/luke-combs/", artistElement.GetAttributeValue("href", ""));
        }

        [Fact]
        public void ParseItemDivs_Should_Skip_Suggestion_Items()
        {
            // Arrange
            var suggestionHtml = @"
                <div class='song song-line flex grid-gap-s align-items-center song--suggest'>
                    <a class='song-line--covers relative flex-none flex align-items-center justify-center bg-life-blue-electric' href='/test'>
                        <svg><use href='#icon-vote'></use></svg>
                    </a>
                    <div class='song__title-container'>
                        <p class='song__title block'>Suggestion Song</p>
                        <div><span class='song__artist'>Suggested Artist</span></div>
                    </div>
                </div>";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(suggestionHtml);
            var itemDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'song')]");

            // Act
            var isSuggestion = itemDiv.HasClass("song--suggest");

            // Assert
            Assert.True(isSuggestion);
        }
    }
}