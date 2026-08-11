using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Generic fallback scraper engine that uses heuristic content detection
    /// (similar to Firefox Reader Mode) to extract story content from any website.
    /// This engine handles all URLs that no other engine can handle.
    ///
    /// Algorithm:
    ///   1. Metadata  — Scores candidate nodes by text density + semantic role
    ///   2. Chapters  — Detects chapter links using Vietnamese/English regex patterns
    ///   3. Content   — Picks the div with the most paragraph text as the reading area
    /// </summary>
    public sealed class GenericFallbackEngine : IScraperEngine
    {
        // ── Patterns ────────────────────────────────────────────────────────
        private static readonly Regex ChapterNumRegex = new Regex(
            @"(?:chương|chapter|ch\.?)\s*(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ZeroWidthRegex = new Regex(
            @"[\u200B-\u200D\uFEFF\u200E\u200F]",
            RegexOptions.Compiled);

        private static readonly Regex MultiSpaceRegex = new Regex(
            @"\s{3,}",
            RegexOptions.Compiled);

        // Tags that are always noise — never the main content
        private static readonly HashSet<string> NoiseTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "nav", "header", "footer", "iframe",
            "form", "button", "noscript", "aside", "ads", "advertisement"
        };

        // Class/id keywords that indicate a content container
        private static readonly string[] ContentKeywords =
        {
            "chapter-content", "chapter_content", "reading-content", "read-content",
            "truyen-content", "noi-dung", "box-chap", "box-truyen",
            "chapter-c", "article-content", "post-content", "entry-content",
            "content-story", "story-content", "text-content", "main-content"
        };

        // Class/id keywords that indicate a noise container (sidebar, ads, etc.)
        private static readonly string[] NoiseKeywords =
        {
            "sidebar", "ads", "advertisement", "comment", "related", "share",
            "social", "nav", "menu", "header", "footer", "breadcrumb", "copyright",
            "notification", "banner", "popup", "modal", "overlay"
        };

        // ── IScraperEngine ───────────────────────────────────────────────────

        /// <summary>This engine handles every URL as a last resort fallback.</summary>
        public bool CanHandle(string url) => !string.IsNullOrWhiteSpace(url);

        public async Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct)
        {
            using var client = CreateHttpClient(storyUrl);
            var html = await client.GetStringAsync(storyUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove noise nodes globally before any extraction
            RemoveNoiseNodes(doc.DocumentNode);

            var title      = ExtractTitle(doc, storyUrl);
            var author     = ExtractAuthor(doc);
            var cover      = ExtractCoverImage(doc, storyUrl);
            var desc       = ExtractDescription(doc);
            var genres     = ExtractGenres(doc);
            var chapters   = ExtractChapterList(doc, storyUrl);

            return new ScrapedStoryMetadataDto(title, desc, cover, author, genres, chapters);
        }

        public async Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct)
        {
            using var client = CreateHttpClient(chapterUrl);
            var html = await client.GetStringAsync(chapterUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            RemoveNoiseNodes(doc.DocumentNode);

            var contentNode = FindContentNode(doc.DocumentNode);
            if (contentNode == null)
                throw new Exception($"Không thể tìm thấy vùng nội dung trên trang: {chapterUrl}");

            // Clean hidden tracker elements
            var hiddenNodes = contentNode.SelectNodes(
                ".//*[contains(@style,'display:none') or contains(@style,'display: none') " +
                "or contains(@style,'visibility:hidden') or contains(@style,'visibility: hidden')]");
            if (hiddenNodes != null)
                foreach (var n in hiddenNodes.ToList()) n.Remove();

            var rawHtml = contentNode.InnerHtml;
            return SanitizeContent(rawHtml);
        }

        // ── Private helpers: HTTP ─────────────────────────────────────────────

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
            catch { /* ignore malformed urls */ }
            return client;
        }

        // ── Private helpers: Metadata extraction ──────────────────────────────

        private static string ExtractTitle(HtmlDocument doc, string storyUrl)
        {
            // 1. og:title
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
            if (og != null)
            {
                var t = CleanText(og.GetAttributeValue("content", ""));
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // 2. <title> tag — strip site name after " - " or " | "
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

            // 3. First <h1>
            var h1 = doc.DocumentNode.SelectSingleNode("//h1");
            if (h1 != null)
            {
                var t = CleanText(h1.InnerText);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            return "Truyện chưa có tên";
        }

        private static string ExtractAuthor(HtmlDocument doc)
        {
            // Patterns like "Tác giả: <a>Name</a>" or "Author: Name"
            var authorNode = doc.DocumentNode.SelectSingleNode(
                "//*[contains(text(),'Tác giả') or contains(text(),'tác giả') " +
                "or contains(text(),'Author') or contains(text(),'author')]/following-sibling::*[1]")
                ?? doc.DocumentNode.SelectSingleNode(
                "//*[contains(text(),'Tác giả') or contains(text(),'Author')]/a");

            if (authorNode != null)
            {
                var t = CleanText(authorNode.InnerText);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // Meta author
            var meta = doc.DocumentNode.SelectSingleNode("//meta[@name='author']");
            if (meta != null)
            {
                var t = CleanText(meta.GetAttributeValue("content", ""));
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            return "Đang cập nhật";
        }

        private static string ExtractCoverImage(HtmlDocument doc, string storyUrl)
        {
            // 1. og:image is almost always the cover
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (og != null)
            {
                var src = og.GetAttributeValue("content", "").Trim();
                if (!string.IsNullOrEmpty(src)) return ToAbsoluteUrl(src, storyUrl);
            }

            // 2. First <img> in element with class containing "cover" or "thumb"
            var coverImg = doc.DocumentNode.SelectSingleNode(
                "//*[contains(@class,'cover') or contains(@class,'thumb') or contains(@class,'poster')]//img")
                ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class,'cover') or contains(@class,'poster')]");

            if (coverImg != null)
            {
                var src = coverImg.GetAttributeValue("src", "")
                       ?? coverImg.GetAttributeValue("data-src", "");
                if (!string.IsNullOrEmpty(src)) return ToAbsoluteUrl(src, storyUrl);
            }

            return string.Empty;
        }

        private static string ExtractDescription(HtmlDocument doc)
        {
            // 1. og:description
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")
                  ?? doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            if (og != null)
            {
                var t = CleanText(og.GetAttributeValue("content", ""));
                if (t.Length > 20) return t;
            }

            // 2. Element with "desc" or "synopsis" in class
            var descNode = doc.DocumentNode.SelectSingleNode(
                "//*[contains(@class,'desc') or contains(@class,'synopsis') " +
                "or contains(@class,'summary') or contains(@class,'gioi-thieu') " +
                "or contains(@class,'tom-tat') or contains(@id,'summary')]");
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

            // Nodes following "Thể loại" label
            var genreContainer = doc.DocumentNode.SelectSingleNode(
                "//*[contains(text(),'Thể loại') or contains(text(),'Genres') or contains(text(),'Genre')]");

            if (genreContainer != null)
            {
                var links = genreContainer.ParentNode?.SelectNodes(".//a")
                         ?? genreContainer.SelectNodes("following-sibling::*/a");
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
                    var keywords = meta.GetAttributeValue("content", "");
                    genres.AddRange(keywords.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).Take(5));
                }
            }

            return genres;
        }

        private static List<ScrapedChapterLinkDto> ExtractChapterList(HtmlDocument doc, string storyUrl)
        {
            var chapters = new List<ScrapedChapterLinkDto>();

            // Find all <a> tags that match chapter pattern
            var allLinks = doc.DocumentNode.SelectNodes("//a[@href]");
            if (allLinks == null) return chapters;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var link in allLinks)
            {
                var text = CleanText(link.InnerText);
                var href = link.GetAttributeValue("href", "").Trim();

                if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text)) continue;
                if (href.StartsWith("#") || href.StartsWith("javascript")) continue;

                // Must match chapter pattern
                var match = ChapterNumRegex.Match(text);
                if (!match.Success) continue;

                var absHref = ToAbsoluteUrl(href, storyUrl);
                if (!seen.Add(absHref)) continue;

                var chapterNumStr = match.Groups[1].Value.Replace(",", ".").Replace(".", "");
                if (!int.TryParse(chapterNumStr, out var chapterNum)) chapterNum = chapters.Count + 1;

                chapters.Add(new ScrapedChapterLinkDto(chapterNum, text, absHref));
            }

            // Sort ascending by chapter number
            chapters.Sort((a, b) => a.ChapterNumber.CompareTo(b.ChapterNumber));
            return chapters;
        }

        // ── Private helpers: Content extraction ──────────────────────────────

        /// <summary>
        /// Finds the DOM node most likely to contain reading text using a scoring approach.
        /// Score = total inner text length of <p> children.
        /// This mimics the core idea behind Mozilla Readability.
        /// </summary>
        private static HtmlNode? FindContentNode(HtmlNode root)
        {
            // First, try well-known class/id keywords
            foreach (var keyword in ContentKeywords)
            {
                var node = root.SelectSingleNode(
                    $"//*[contains(@class,'{keyword}') or contains(@id,'{keyword}')]");
                if (node != null && GetTextLength(node) > 200)
                    return node;
            }

            // Fallback: score every div/article/section by paragraph text length
            var candidates = root.SelectNodes("//div|//article|//section");
            if (candidates == null) return null;

            HtmlNode? best = null;
            int bestScore = 0;

            foreach (var candidate in candidates)
            {
                // Skip noisy containers
                if (IsNoiseNode(candidate)) continue;

                var paragraphs = candidate.SelectNodes(".//p");
                if (paragraphs == null) continue;

                int score = paragraphs.Sum(p => GetTextLength(p));

                // Boost score for semantic article tags
                if (candidate.Name.Equals("article", StringComparison.OrdinalIgnoreCase))
                    score = (int)(score * 1.3);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            // Must have at least 300 characters of text to be considered valid
            return bestScore >= 300 ? best : null;
        }

        private static void RemoveNoiseNodes(HtmlNode root)
        {
            var toRemove = root.SelectNodes(
                "//script|//style|//nav|//header|//footer|//iframe|//noscript|//form")
                ?.ToList();
            if (toRemove == null) return;
            foreach (var node in toRemove) node.Remove();
        }

        private static bool IsNoiseNode(HtmlNode node)
        {
            if (NoiseTags.Contains(node.Name)) return true;
            var cls = node.GetAttributeValue("class", "").ToLowerInvariant();
            var id  = node.GetAttributeValue("id", "").ToLowerInvariant();
            return NoiseKeywords.Any(k => cls.Contains(k) || id.Contains(k));
        }

        private static int GetTextLength(HtmlNode node)
        {
            var text = WebUtility.HtmlDecode(node.InnerText ?? "");
            return ZeroWidthRegex.Replace(text, "").Trim().Length;
        }

        // ── Private helpers: String utilities ────────────────────────────────

        private static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var text = WebUtility.HtmlDecode(input);
            text = ZeroWidthRegex.Replace(text, "");
            text = MultiSpaceRegex.Replace(text, " ");
            return text.Trim();
        }

        private static string SanitizeContent(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var result = ZeroWidthRegex.Replace(html, "");
            return result.Trim();
        }

        private static string ToAbsoluteUrl(string url, string baseUrl)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            try
            {
                var baseUri = new Uri(baseUrl);
                if (url.StartsWith("//"))
                    return baseUri.Scheme + ":" + url;
                if (url.StartsWith("/"))
                    return $"{baseUri.Scheme}://{baseUri.Host}{url}";
                return new Uri(baseUri, url).AbsoluteUri;
            }
            catch { return url; }
        }
    }
}
