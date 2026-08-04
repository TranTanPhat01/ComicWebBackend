namespace ComicWeb.Application.Features.Genres;

public static class DefaultGenreCatalog
{
    public static IReadOnlyList<(string Name, string Slug, string? Description)> Items { get; } =
    [
        ("Huyền Huyễn", "huyen-huyen", "Truyện huyền huyễn, kỳ ảo, phép thuật."),
        ("Đô Thị", "do-thi", "Truyện đời thường, tình cảm, hiện đại."),
        ("Tiên Hiệp", "tien-hiep", "Truyện tiên hiệp, võ giả, tu luyện."),
        ("Xuyên Không", "xuyen-khong", "Truyện xuyên không, chuyển sinh, thế giới khác."),
        ("Hệ Thống", "he-thong", "Truyện hệ thống, nhiệm vụ, tăng cấp."),
        ("Khoa Huyễn", "khoa-huyen", "Truyện khoa học viễn tưởng, giả tưởng."),
        ("Đấu Trí", "dau-tri", "Truyện đấu trí, chính trị, tính toán."),
        ("Đấu Khí", "dau-khi", "Truyện võ lâm, chiến đấu, tu luyện."),
        ("Hành Động", "hanh-dong", "Truyện hành động, đánh nhau, kịch tính."),
        ("Trùng Sinh", "trung-sinh", "Truyện trùng sinh, tái sinh, hồi sinh."),
        ("Phương Tây", "phuong-tay", "Truyện phương Tây, phong cách ngoại lai."),
        ("Tu Chân", "tu-chan", "Truyện tu chân, tu luyện, đạo pháp."),
        ("Phép Thuật", "phep-thuat", "Truyện phép thuật, ma pháp, phù thủy.")
    ];
}
