using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Dtos;
using HtmlAgilityPack;

namespace ComicWeb.Persistence.Content.Engines
{
    /// <summary>
    /// Dedicated scraper engine for giotruyen.online (Laravel-based comic site).
    ///
    /// Key characteristics of giotruyen.online:
    ///   - Story detail URL: /truyen/{slug}
    ///   - Chapter URL:      /{storySlug}/{chapterSlug}  (no /truyen/ prefix)
    ///   - Chapter list:     AJAX POST to /ajax/get-chapters with CSRF token + pagination
    ///   - CSRF token:       Embedded in window.SuuTruyen.csrfToken in the page HTML
    ///   - Content element:  div.chapter-detail or div with id "chapter-content"
    /// </summary>
    public sealed class GioTruyenEngine : IScraperEngine
    {
        private const string BaseUrl = "https://giotruyen.online";
        private const string AjaxChaptersUrl = "https://giotruyen.online/ajax/get-chapters";

        private static readonly Regex ZeroWidthRegex = new(
            @"[\u200B-\u200D\uFEFF\u200E\u200F]", RegexOptions.Compiled);

        private static readonly Regex CsrfTokenRegex = new(
            @"csrfToken\s*:\s*'([^']+)'", RegexOptions.Compiled);

        private static readonly Regex StorySlugRegex = new(
            @"giotruyen\.online/truyen/([^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ChapterNumRegex = new(
            @"(?:Chương|chương|Chuong|chuong|Chapter|chapter)\s*(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── IScraperEngine ─────────────────────────────────────────────────────

        public bool CanHandle(string url) =>
            !string.IsNullOrWhiteSpace(url) &&
            url.Contains("giotruyen.online", StringComparison.OrdinalIgnoreCase);

        public async Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct)
        {
            using var client = CreateHttpClient(storyUrl);

            // 1. Fetch the story detail page
            var html = await client.GetStringAsync(storyUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 2. Extract metadata from HTML
            var title  = ExtractTitle(doc);
            var author = ExtractAuthor(doc);
            var cover  = ExtractCoverImage(doc);
            var desc   = ExtractDescription(doc);
            var genres = ExtractGenres(doc);

            // 3. Extract story slug and CSRF token
            var storySlug = ExtractStorySlug(storyUrl, doc);
            var csrfToken = ExtractCsrfToken(html);

            // 4. Load ALL chapters via AJAX pagination
            var chapters = await FetchAllChaptersAsync(client, storySlug, csrfToken, ct);

            return new ScrapedStoryMetadataDto(title, desc, cover, author, genres, chapters);
        }

        public async Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct)
        {
            using var client = CreateHttpClient(chapterUrl);
            var html = await client.GetStringAsync(chapterUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try known content selectors in priority order
            var contentNode =
                doc.DocumentNode.SelectSingleNode("//div[contains(@class,'chapter-detail')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[@id='chapter-content']")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content-chapter')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'box-chap')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'reading-content')]");

            if (contentNode == null)
                throw new Exception($"Không tìm thấy nội dung chương tại: {chapterUrl}");

            // Remove noise: scripts, ads, hidden elements
            RemoveNoiseFromNode(contentNode);

            var rawHtml = contentNode.InnerHtml;
            return ZeroWidthRegex.Replace(rawHtml, "").Trim();
        }

        // ── Private: AJAX chapter loading ─────────────────────────────────────

        private static async Task<List<ScrapedChapterLinkDto>> FetchAllChaptersAsync(
            HttpClient client,
            string storySlug,
            string csrfToken,
            CancellationToken ct)
        {
            var allChapters = new List<ScrapedChapterLinkDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int page = 1;

            while (true)
            {
                var formData = new Dictionary<string, string>
                {
                    { "story_slug", storySlug },
                    { "page",       page.ToString() }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, AjaxChaptersUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("X-CSRF-TOKEN",       csrfToken);
                request.Headers.Add("X-Requested-With",   "XMLHttpRequest");
                request.Headers.Add("Referer",            $"{BaseUrl}/truyen/{storySlug}");
                request.Headers.Add("Accept",             "application/json, text/javascript, */*");

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    // If AJAX fails on the first page, fall back to HTML already loaded
                    if (page == 1) break;
                    throw new Exception($"Lỗi khi tải trang chương {page}: {ex.Message}", ex);
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var ajaxResult = ParseAjaxChaptersResponse(json, storySlug);

                if (ajaxResult.Count == 0) break; // No more chapters

                foreach (var ch in ajaxResult)
                {
                    if (seen.Add(ch.Url))
                        allChapters.Add(ch);
                }

                // If fewer results than expected, we've reached the last page
                if (ajaxResult.Count < 20) break;

                page++;

                // Safety cap to avoid infinite loops
                if (page > 500) break;
            }

            // Sort ascending by chapter number
            allChapters.Sort((a, b) => a.ChapterNumber.CompareTo(b.ChapterNumber));
            return allChapters;
        }

        private static List<ScrapedChapterLinkDto> ParseAjaxChaptersResponse(string json, string storySlug)
        {
            var chapters = new List<ScrapedChapterLinkDto>();

            try
            {
                // The endpoint may return {html: "..."} or a plain HTML string
                // Try JSON first
                if (json.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("html", out var htmlElement))
                    {
                        json = htmlElement.GetString() ?? string.Empty;
                    }
                    else if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        json = dataElement.GetString() ?? string.Empty;
                    }
                }
            }
            catch { /* treat as raw HTML */ }

            if (string.IsNullOrWhiteSpace(json)) return chapters;

            var frag = new HtmlDocument();
            frag.LoadHtml(json);

            // Links are in format: <a href="/{storySlug}/chuong-X">Chương X</a>
            var links = frag.DocumentNode.SelectNodes("//a[@href]");
            if (links == null) return chapters;

            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "").Trim();
                var text = CleanText(link.InnerText);

                if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text)) continue;

                // Match chapter number from text
                var numMatch = ChapterNumRegex.Match(text);
                if (!numMatch.Success) continue;

                // Build absolute URL
                var absUrl = href.StartsWith("http") ? href : $"{BaseUrl}{href}";

                var chapterNumStr = numMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
                if (!int.TryParse(chapterNumStr, out var chapterNum))
                    chapterNum = chapters.Count + 1;

                chapters.Add(new ScrapedChapterLinkDto(chapterNum, text, absUrl));
            }

            return chapters;
        }

        // ── Private: Metadata extraction ──────────────────────────────────────

        private static string ExtractTitle(HtmlDocument doc)
        {
            // Try og:title first (most reliable)
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
            if (og != null)
            {
                var t = CleanText(og.GetAttributeValue("content", ""));
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // Try the h3.story-name element specific to giotruyen
            var h3 = doc.DocumentNode.SelectSingleNode("//h3[contains(@class,'story-name')]");
            if (h3 != null)
            {
                var t = CleanText(h3.InnerText);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // Fallback: title tag
            var titleTag = doc.DocumentNode.SelectSingleNode("//title");
            if (titleTag != null)
            {
                var raw = CleanText(titleTag.InnerText);
                foreach (var sep in new[] { " - ", " | ", " – " })
                {
                    var idx = raw.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                    if (idx > 0) { raw = raw[..idx]; break; }
                }
                if (!string.IsNullOrWhiteSpace(raw)) return raw;
            }

            return "Truyện chưa có tên";
        }

        private static string ExtractAuthor(HtmlDocument doc)
        {
            // giotruyen: <p><strong>Tác giả:</strong><a href="#">Name</a></p>
            var authorLink = doc.DocumentNode.SelectSingleNode(
                "//p[contains(.,'Tác giả')]/a")
                ?? doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'story-detail')]//p[contains(.,'Tác giả')]/a");

            if (authorLink != null)
            {
                var t = CleanText(authorLink.InnerText);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            var metaAuthor = doc.DocumentNode.SelectSingleNode("//meta[@property='article:author']");
            if (metaAuthor != null)
            {
                var t = CleanText(metaAuthor.GetAttributeValue("content", ""));
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            return "Đang cập nhật";
        }

        private static string ExtractCoverImage(HtmlDocument doc)
        {
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (og != null)
            {
                var src = og.GetAttributeValue("content", "").Trim();
                if (!string.IsNullOrEmpty(src)) return src;
            }

            // giotruyen: div.book-3d > img
            var img = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'book-3d')]//img")
                   ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'story-detail__top--image')]//img");

            if (img != null)
            {
                var src = img.GetAttributeValue("src", "")
                       ?? img.GetAttributeValue("data-src", "");
                if (!string.IsNullOrEmpty(src))
                    return src.StartsWith("http") ? src : $"{BaseUrl}{src}";
            }

            return string.Empty;
        }

        private static string ExtractDescription(HtmlDocument doc)
        {
            // og:description
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")
                  ?? doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            if (og != null)
            {
                var t = CleanText(og.GetAttributeValue("content", ""));
                if (t.Length > 20) return t;
            }

            // giotruyen: div.story-detail__top--desc
            var descNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'story-detail__top--desc')]");
            if (descNode != null)
            {
                var t = CleanText(descNode.InnerText);
                if (t.Length > 20) return t;
            }

            return string.Empty;
        }

        private static List<string> ExtractGenres(HtmlDocument doc)
        {
            var genres = new List<string>();

            // giotruyen: <div class="d-flex align-items-center flex-warp">links</div>
            // following the "Thể loại:" label
            var genreContainer = doc.DocumentNode.SelectSingleNode(
                "//*[contains(.,'Thể loại')]/following-sibling::div[1]")
                ?? doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'d-flex') and contains(.,'Thể loại')]");

            if (genreContainer != null)
            {
                var links = genreContainer.SelectNodes(".//a");
                if (links != null)
                    foreach (var a in links)
                    {
                        var t = CleanText(a.InnerText);
                        if (!string.IsNullOrEmpty(t)) genres.Add(t);
                    }
            }

            // Fallback: meta keywords
            if (genres.Count == 0)
            {
                var meta = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
                if (meta != null)
                {
                    var kws = meta.GetAttributeValue("content", "");
                    genres.AddRange(
                        kws.Split(',')
                           .Select(k => k.Trim())
                           .Where(k => k.Length > 0)
                           .Take(5));
                }
            }

            return genres;
        }

        // ── Private: helpers ──────────────────────────────────────────────────

        private static string ExtractStorySlug(string storyUrl, HtmlDocument doc)
        {
            // Prefer the hidden input that giotruyen embeds
            var input = doc.DocumentNode.SelectSingleNode("//input[@id='story_slug']");
            if (input != null)
            {
                var val = input.GetAttributeValue("value", "").Trim();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Parse from URL: /truyen/{slug}
            var match = StorySlugRegex.Match(storyUrl);
            if (match.Success) return match.Groups[1].Value;

            throw new Exception("Không thể xác định slug của truyện từ URL hoặc trang HTML.");
        }

        private static string ExtractCsrfToken(string html)
        {
            var match = CsrfTokenRegex.Match(html);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static HttpClient CreateHttpClient(string refererUrl)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en;q=0.8");
            try
            {
                var uri = new Uri(refererUrl);
                client.DefaultRequestHeaders.Add("Referer", $"{uri.Scheme}://{uri.Host}/");
            }
            catch { /* ignore */ }
            return client;
        }

        private static void RemoveNoiseFromNode(HtmlNode node)
        {
            var noiseXPath =
                ".//script | .//style | .//iframe | .//ins | .//noscript" +
                " | .//*[contains(@class,'ads')] | .//*[contains(@class,'advertisement')]" +
                " | .//*[contains(@style,'display:none')] | .//*[contains(@style,'display: none')]" +
                " | .//*[contains(@style,'visibility:hidden')]";

            var noiseNodes = node.SelectNodes(noiseXPath)?.ToList();
            if (noiseNodes != null)
                foreach (var n in noiseNodes) n.Remove();
        }

        private static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var text = WebUtility.HtmlDecode(input);
            text = ZeroWidthRegex.Replace(text, "");
            return text.Trim();
        }
    }
}
