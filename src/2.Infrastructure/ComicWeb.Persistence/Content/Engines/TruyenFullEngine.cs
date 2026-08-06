using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Dtos;
using HtmlAgilityPack;

namespace ComicWeb.Persistence.Content.Engines
{
    public sealed class TruyenFullEngine : IScraperEngine
    {
        private static readonly Regex ZeroWidthCharsRegex = new Regex(@"[\u200B-\u200D\uFEFF\u200E\u200F]", RegexOptions.Compiled);
        private static readonly Regex ChapterNumberRegex = new Regex(@"Chương\s+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool CanHandle(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("truyenfull.", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var html = await client.GetStringAsync(storyUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Title
            var titleNode = doc.DocumentNode.SelectSingleNode("//h3[@class='title' and @itemprop='name']")
                ?? doc.DocumentNode.SelectSingleNode("//h3[@class='title']")
                ?? doc.DocumentNode.SelectSingleNode("//h1");
            var title = titleNode != null ? CleanText(titleNode.InnerText) : "Truyện TruyenFull";

            // Description
            var descNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'desc-text') and @itemprop='description']")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'desc-text')]");
            var description = descNode != null ? CleanText(descNode.InnerText) : string.Empty;

            // Cover Image URL
            var imgNode = doc.DocumentNode.SelectSingleNode("//div[@class='info-holder']//div[@class='book']/img")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'books')]//img");
            var coverImageUrl = imgNode?.GetAttributeValue("src", string.Empty) ?? string.Empty;

            // Author Name
            var authorNode = doc.DocumentNode.SelectSingleNode("//a[@itemprop='author']")
                ?? doc.DocumentNode.SelectSingleNode("//div[@class='info']//div[contains(., 'Tác giả')]/a")
                ?? doc.DocumentNode.SelectSingleNode("//div[@class='info']//a");
            var authorName = authorNode != null ? CleanText(authorNode.InnerText) : "Đang cập nhật";

            // Genres
            var genres = new List<string>();
            var genreNodes = doc.DocumentNode.SelectNodes("//a[@itemprop='genre']")
                ?? doc.DocumentNode.SelectNodes("//div[@class='info']//div[contains(., 'Thể loại')]/a");
            if (genreNodes != null)
            {
                foreach (var node in genreNodes)
                {
                    genres.Add(CleanText(node.InnerText));
                }
            }

            // Chapters List (First page chapters)
            var chapters = new List<ScrapedChapterLinkDto>();
            var chapterLinks = doc.DocumentNode.SelectNodes("//ul[@class='list-chapter']//a");
            if (chapterLinks != null)
            {
                foreach (var link in chapterLinks)
                {
                    var href = link.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrEmpty(href)) continue;

                    var chapterText = CleanText(link.InnerText);
                    int chapterNum = 1;
                    var numMatch = ChapterNumberRegex.Match(chapterText);
                    if (numMatch.Success)
                    {
                        int.TryParse(numMatch.Groups[1].Value, out chapterNum);
                    }

                    chapters.Add(new ScrapedChapterLinkDto(chapterNum, chapterText, href));
                }
            }

            return new ScrapedStoryMetadataDto(title, description, coverImageUrl, authorName, genres, chapters);
        }

        public async Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var html = await client.GetStringAsync(chapterUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='chapter-c' and contains(@class, 'chapter-c')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'chapter-c')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[@id='chapter-content']");

            if (contentNode == null)
            {
                throw new Exception("Không thể tìm thấy thẻ chứa nội dung chương truyện TruyenFull (chapter-c).");
            }

            // Clean up any internal script or ads tags if present
            var scriptNodes = contentNode.SelectNodes(".//script | .//ins | .//iframe");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            var cleanHtml = contentNode.InnerHtml;

            // Strip watermark/anti-copy zero-width spaces
            return ZeroWidthCharsRegex.Replace(cleanHtml, "").Trim();
        }

        private static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var text = WebUtility.HtmlDecode(input);
            return ZeroWidthCharsRegex.Replace(text, "").Trim();
        }
    }
}
