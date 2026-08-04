using ComicWeb.Application.Common.Interfaces;
using Ganss.Xss;
using System.Linq;

namespace ComicWeb.Persistence.Content;

public sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    private static readonly System.Collections.Generic.HashSet<string> TagsToRemoveContent = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "canvas", "noscript"
    };

    public HtmlContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // Clear all defaults to establish a strict allow-list
        _sanitizer.KeepChildNodes = true;
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedCssProperties.Clear();

        _sanitizer.RemovingTag += (sender, e) =>
        {
            if (TagsToRemoveContent.Contains(e.Tag.TagName))
            {
                e.Tag.InnerHtml = string.Empty;
            }
        };

        // Register custom allowed tags
        var allowedTags = new[]
        {
            "p", "br", "div", "span", "strong", "b", "em", "i", "u",
            "blockquote", "ul", "ol", "li", "h1", "h2", "h3", "h4", "img", "a"
        };
        foreach (var tag in allowedTags)
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        // Register custom allowed attributes
        var allowedAttributes = new[]
        {
            "href", "src", "alt", "title", "width", "height", "class"
        };
        foreach (var attr in allowedAttributes)
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        // Register custom allowed schemes
        var allowedSchemes = new[] { "http", "https" };
        foreach (var scheme in allowedSchemes)
        {
            _sanitizer.AllowedSchemes.Add(scheme);
        }
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }

    public bool IsMeaningful(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var parser = new AngleSharp.Html.Parser.HtmlParser();
        var document = parser.ParseDocument(html);

        // Meaningful if it contains any images
        var hasImages = document.QuerySelectorAll("img").Any();

        // Meaningful if it contains any non-whitespace text (AngleSharp automatically decodes entities)
        var textContent = document.Body?.TextContent ?? string.Empty;
        var hasText = !string.IsNullOrWhiteSpace(textContent);

        return hasImages || hasText;
    }
}
