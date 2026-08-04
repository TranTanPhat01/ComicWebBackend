using ComicWeb.Persistence.Content;
using Xunit;

namespace ComicWeb.WebApi.IntegrationTests;

public sealed class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sanitizer = new();

    [Fact]
    public void Script_tags_are_removed_but_inner_text_is_kept()
    {
        var input = "<script>alert(1)</script><p>Hello</p>";
        var output = _sanitizer.Sanitize(input);
        Assert.DoesNotContain("script", output);
        Assert.Contains("<p>Hello</p>", output);
    }

    [Fact]
    public void Event_handler_attributes_are_removed()
    {
        var input = "<img src=\"https://example.com/a.jpg\" onerror=\"alert(1)\">";
        var output = _sanitizer.Sanitize(input);
        Assert.Contains("src=\"https://example.com/a.jpg\"", output);
        Assert.DoesNotContain("onerror", output);
    }

    [Fact]
    public void Javascript_url_scheme_is_removed_or_sanitized()
    {
        var input = "<a href=\"javascript:alert(1)\">Click</a>";
        var output = _sanitizer.Sanitize(input);
        Assert.DoesNotContain("javascript", output);
        Assert.Contains("Click", output);
    }

    [Fact]
    public void Safe_links_are_preserved()
    {
        var input = "<a href=\"https://example.com\" class=\"btn\">Safe</a>";
        var output = _sanitizer.Sanitize(input);
        Assert.Contains("href=\"https://example.com\"", output);
        Assert.Contains("class=\"btn\"", output);
        Assert.Contains("Safe", output);
    }

    [Fact]
    public void Safe_images_are_preserved()
    {
        var input = "<img src=\"https://example.com/page.jpg\" alt=\"page\" width=\"100\" height=\"50\">";
        var output = _sanitizer.Sanitize(input);
        Assert.Contains("src=\"https://example.com/page.jpg\"", output);
        Assert.Contains("alt=\"page\"", output);
        Assert.Contains("width=\"100\"", output);
        Assert.Contains("height=\"50\"", output);
    }

    [Fact]
    public void Iframe_is_removed()
    {
        var input = "<iframe src=\"https://example.com\"></iframe><p>Keep</p>";
        var output = _sanitizer.Sanitize(input);
        Assert.DoesNotContain("iframe", output);
        Assert.Contains("<p>Keep</p>", output);
    }

    [Fact]
    public void Style_is_removed()
    {
        var input = "<p style=\"color: red; font-size: 20px;\">Text</p><style>p { color: blue; }</style>";
        var output = _sanitizer.Sanitize(input);
        Assert.DoesNotContain("style", output);
        Assert.Contains("<p>Text</p>", output);
    }

    [Fact]
    public void Unknown_tags_are_removed_but_text_is_preserved()
    {
        var input = "<unknown-tag>Keep this text</unknown-tag>";
        var output = _sanitizer.Sanitize(input);
        Assert.DoesNotContain("unknown-tag", output);
        Assert.Contains("Keep this text", output);
    }

    [Fact]
    public void Unicode_and_Vietnamese_characters_are_preserved()
    {
        var input = "<p>Truyện tranh chữ tiếng Việt có dấu: á, à, ả, ã, ạ, đ.</p>";
        var output = _sanitizer.Sanitize(input);
        Assert.Contains("Truyện tranh chữ tiếng Việt có dấu: á, à, ả, ã, ạ, đ.", output);
    }

    [Fact]
    public void Sanitization_is_idempotent()
    {
        var input = "<p class=\"normal\"><script>alert(1)</script>Hello <a href=\"javascript:void(0)\">World</a></p>";
        var firstPass = _sanitizer.Sanitize(input);
        var secondPass = _sanitizer.Sanitize(firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Handles_null_empty_and_whitespace(string? input, string expected)
    {
        var output = _sanitizer.Sanitize(input);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("<script>alert(1)</script>", false)]
    [InlineData("<p><br></p>", false)]
    [InlineData("<div><span>   </span><br/></div>", false)]
    [InlineData("<p>Hello</p>", true)]
    [InlineData("<img src=\"https://example.com/image.png\" />", true)]
    [InlineData("Just plain text", true)]
    [InlineData("<a href=\"https://google.com\">Link</a>", true)]
    public void IsMeaningful_identifies_content_correctly(string? input, bool expected)
    {
        var sanitized = _sanitizer.Sanitize(input);
        var result = _sanitizer.IsMeaningful(sanitized);
        Assert.Equal(expected, result);
    }
}
