using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Dtos;
using HtmlAgilityPack;

namespace ComicWeb.Persistence.Content.Engines
{
    public sealed class NguonTruyenEngine : IScraperEngine
    {
        private static readonly Regex ZeroWidthCharsRegex = new Regex(@"[\u200B-\u200D\uFEFF\u200E\u200F]", RegexOptions.Compiled);
        private static readonly Regex ChapterNumberRegex = new Regex(@"Chapter\s+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool CanHandle(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("nguontruyen.com", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct)
        {
            var handler = new HttpClientHandler { UseCookies = true };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var html = await client.GetStringAsync(storyUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Title
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[@class='title-2']");
            var title = titleNode != null ? CleanText(titleNode.InnerText) : "Truyện chưa có tên";

            // Description
            var descNode = doc.DocumentNode.SelectSingleNode("//div[@class='detail-film-desc']");
            var description = descNode != null ? CleanText(descNode.InnerText) : string.Empty;

            // Cover Image URL
            var imgNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'img-movie')]/img");
            var coverImageUrl = imgNode?.GetAttributeValue("src", string.Empty) ?? string.Empty;
            if (!string.IsNullOrEmpty(coverImageUrl) && coverImageUrl.StartsWith("/"))
            {
                coverImageUrl = "https://nguontruyen.com" + coverImageUrl;
            }

            // Author Name
            var authorNode = doc.DocumentNode.SelectSingleNode("//div[@class='infor-movie']/p[contains(., 'Tác giả')]/a")
                ?? doc.DocumentNode.SelectSingleNode("//div[@class='infor-movie']/p[1]/a");
            var authorName = authorNode != null ? CleanText(authorNode.InnerText) : "Đang cập nhật";

            // Genres
            var genres = new List<string>();
            var genreNodes = doc.DocumentNode.SelectNodes("//div[@class='infor-movie']/p[contains(., 'Thể loại')]/a");
            if (genreNodes != null)
            {
                foreach (var node in genreNodes)
                {
                    genres.Add(CleanText(node.InnerText));
                }
            }

            // Chapters List
            var chapters = new List<ScrapedChapterLinkDto>();
            var chapterLinks = doc.DocumentNode.SelectNodes("//a[@class='chapterLink']");
            if (chapterLinks != null)
            {
                foreach (var link in chapterLinks)
                {
                    var href = link.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrEmpty(href)) continue;

                    if (href.StartsWith("/"))
                    {
                        href = "https://nguontruyen.com" + href;
                    }

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

            // nguontruyen.com lists chapters newest first, so we reverse it to import chronologically
            chapters.Reverse();

            return new ScrapedStoryMetadataDto(title, description, coverImageUrl, authorName, genres, chapters);
        }

        public async Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct)
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer
            };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Step 1: GET the shell page to initialize cookies and retrieve variables
            var shellHtml = await client.GetStringAsync(chapterUrl, ct);

            // Parse fid
            var fidMatch = Regex.Match(shellHtml, @"'fid'\s*:\s*(\d+)");
            if (!fidMatch.Success)
            {
                fidMatch = Regex.Match(shellHtml, @"fid\s*:\s*(\d+)");
            }
            var fid = fidMatch.Success ? fidMatch.Groups[1].Value : string.Empty;

            // Parse time
            var timeMatch = Regex.Match(shellHtml, @"'time'\s*:\s*(\d+)");
            if (!timeMatch.Success)
            {
                timeMatch = Regex.Match(shellHtml, @"time\s*:\s*(\d+)");
            }
            var time = timeMatch.Success ? timeMatch.Groups[1].Value : string.Empty;

            // Parse key
            var keyMatch = Regex.Match(shellHtml, @"key\s*:\s*'([^']+)'");
            var key = keyMatch.Success ? keyMatch.Groups[1].Value : string.Empty;

            if (string.IsNullOrEmpty(fid) || string.IsNullOrEmpty(time) || string.IsNullOrEmpty(key))
            {
                throw new Exception("Không thể tìm thấy các tham số bảo mật (fid, time, key) trên trang nguồn nguontruyen.");
            }

            // Step 2: Ensure session cookies are duplicated for the grab subdomain in CookieContainer
            var baseUri = new Uri("https://nguontruyen.com");
            var targetUri = new Uri("https://grab.nguontruyen.com");
            var cookies = cookieContainer.GetCookies(baseUri);
            foreach (Cookie cookie in cookies)
            {
                var newCookie = new Cookie(cookie.Name, cookie.Value, "/", "grab.nguontruyen.com");
                cookieContainer.Add(targetUri, newCookie);
            }

            // Step 3: Call the AJAX POST endpoint to load the actual text content
            var ajaxUrl = chapterUrl.Replace("/doc-truyen/", "/doc-truyen-ajax/");
            // Inject the grab subdomain
            var uriMatch = Regex.Match(ajaxUrl, @"^(https?://)([^/]+)(.*)$");
            if (uriMatch.Success)
            {
                ajaxUrl = $"{uriMatch.Groups[1].Value}grab.{uriMatch.Groups[2].Value}{uriMatch.Groups[3].Value}";
            }

            var postData = new Dictionary<string, string>
            {
                { "fid", fid },
                { "time", time },
                { "key", key }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, ajaxUrl)
            {
                Content = new FormUrlEncodedContent(postData)
            };
            request.Headers.Add("Referer", chapterUrl);
            request.Headers.Add("Origin", "https://nguontruyen.com");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Dest", "empty");

            var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var jsonResult = await response.Content.ReadFromJsonAsync<AjaxChapterResponse>(cancellationToken: ct);
            if (jsonResult == null || jsonResult.Code != 200 || string.IsNullOrEmpty(jsonResult.Html))
            {
                throw new Exception("Máy chủ truyện nguồn trả về nội dung rỗng. Chương truyện này có thể bị khóa hoặc yêu cầu trả phí.");
            }

            // Step 4: Parse and sanitize the HTML returned in the JSON field
            var doc = new HtmlDocument();
            doc.LoadHtml(jsonResult.Html);

            var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'read-item-text')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[@id='readingContent']")
                ?? doc.DocumentNode;

            // Remove hidden tracker elements
            var hiddenNodes = contentNode.SelectNodes(".//*[contains(@style, 'display: none') or contains(@style, 'visibility: hidden')]");
            if (hiddenNodes != null)
            {
                foreach (var node in hiddenNodes)
                {
                    node.Remove();
                }
            }

            // Remove top ads placeholder
            var adsNodes = contentNode.SelectNodes(".//*[@id='ads-chapter-top']");
            if (adsNodes != null)
            {
                foreach (var node in adsNodes)
                {
                    node.Remove();
                }
            }

            // Remove TTS wrapper
            var ttsNodes = contentNode.SelectNodes(".//*[contains(@class, 'tts')]");
            if (ttsNodes != null)
            {
                foreach (var node in ttsNodes)
                {
                    node.Remove();
                }
            }

            var cleanHtml = contentNode.InnerHtml;

            // Clean zero-width space characters and return
            return ZeroWidthCharsRegex.Replace(cleanHtml, "").Trim();
        }

        private static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var text = WebUtility.HtmlDecode(input);
            return ZeroWidthCharsRegex.Replace(text, "").Trim();
        }

        private sealed class AjaxChapterResponse
        {
            public int Code { get; set; }
            public string Html { get; set; } = string.Empty;
        }
    }
}
