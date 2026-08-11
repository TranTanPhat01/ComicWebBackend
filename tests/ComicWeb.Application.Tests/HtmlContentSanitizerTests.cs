using Xunit;
using ComicWeb.Persistence.Content;

namespace ComicWeb.Application.Tests
{
    public class HtmlContentSanitizerTests
    {
        private readonly HtmlContentSanitizer _sanitizer;

        public HtmlContentSanitizerTests()
        {
            _sanitizer = new HtmlContentSanitizer();
        }

        [Theory]
        [InlineData("<script>alert(1)</script>", "")]
        [InlineData("Hello <script>alert('XSS')</script>World", "Hello World")]
        [InlineData("<img src=x onerror=alert(1)>", "<img src=\"x\">")]
        [InlineData("<iframe src=\"http://malicious.com\"></iframe>", "")]
        [InlineData("<a href=\"javascript:alert(1)\">click me</a>", "<a>click me</a>")]
        [InlineData("<svg onload=alert(1)></svg>", "")]
        [InlineData("<div style=\"color: red;\" onclick=\"alert(1)\">Content</div>", "<div>Content</div>")]
        public void Sanitize_ShouldRemoveXssPayloads_WhileKeepingSafeHtml(string dirtyHtml, string expectedCleanHtml)
        {
            var result = _sanitizer.Sanitize(dirtyHtml);
            Assert.Equal(expectedCleanHtml, result);
        }

        [Theory]
        [InlineData("<p>Chương 1: Khởi đầu mới</p>", true)]
        [InlineData("<img src=\"cover.jpg\" />", true)]
        [InlineData("   ", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("<div></div>", false)]
        [InlineData("<script>alert(1)</script>", false)]
        public void IsMeaningful_ShouldIdentifyValidContent(string? html, bool expectedIsMeaningful)
        {
            var result = _sanitizer.IsMeaningful(html);
            Assert.Equal(expectedIsMeaningful, result);
        }
    }
}
