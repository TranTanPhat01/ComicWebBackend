using System.Text.RegularExpressions;
using Xunit;

namespace ComicWeb.Application.Tests
{
    public class ScraperServiceTests
    {
        private static readonly Regex ZeroWidthCharsRegex = new Regex(@"[\u200B-\u200D\uFEFF\u200E\u200F]", RegexOptions.Compiled);

        [Fact]
        public void ZeroWidthCharacters_ShouldBeRemovedCorrectly()
        {
            // Verify that watermark characters like Zero-Width Space (U+200B),
            // Zero-Width Non-Joiner (U+200C), and Zero-Width Joiner (U+200D)
            // are cleaned out successfully.
            var dirtyText = "Bác sĩ, tôi​‌‌​​‌‌‌‍​‌‌‌​‌​‌‍​‌‌​​‌​‌‍​‌‌‌​​‌‌‍​‌‌‌​‌​​‍ có bệnh!";
            var cleanText = ZeroWidthCharsRegex.Replace(dirtyText, "");

            Assert.Equal("Bác sĩ, tôi có bệnh!", cleanText);
        }
    }
}
