using ComicWeb.Persistence.Content;

namespace ComicWeb.WebApi.IntegrationTests;

public sealed class VietnameseSlugGeneratorTests
{
    private readonly VietnameseSlugGenerator _generator = new();

    [Theory]
    [InlineData("Đấu Phá Thương Khung", "dau-pha-thuong-khung")]
    [InlineData("  One   Piece! #1080 ", "one-piece-1080")]
    [InlineData("Tiếng Việt: đ/Đ", "tieng-viet-d-d")]
    public void Generate_normalizes_vietnamese_and_separators(string input, string expected)
        => Assert.Equal(expected, _generator.Generate(input));

    [Fact]
    public void Generate_returns_empty_for_no_usable_characters()
        => Assert.Equal(string.Empty, _generator.Generate(" --- "));
}
