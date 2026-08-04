using ComicWeb.Application.Features.Genres;

namespace ComicWeb.Application.Tests;

public class GenreSeedingTests
{
    [Fact]
    public void Provides_default_genre_catalog_with_unique_slugs()
    {
        var genres = DefaultGenreCatalog.Items;

        Assert.NotEmpty(genres);
        Assert.Equal(genres.Count, genres.Select(x => x.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(genres, x => x.Name == "Huyền Huyễn");
        Assert.Contains(genres, x => x.Name == "Đô Thị");
        Assert.Contains(genres, x => x.Name == "Tiên Hiệp");
    }
}
