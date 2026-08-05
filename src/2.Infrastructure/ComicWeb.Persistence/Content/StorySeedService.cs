using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace ComicWeb.Persistence.Content;

public sealed class StorySeedService(IServiceScopeFactory scopeFactory, ILogger<StorySeedService> logger) : IHostedService
{
    private static string GetChapterContent(string storyTitle, int chapterNumber)
    {
        return $@"
            <p>Ánh trăng vằng vặc rọi xuống đầu ngõ nhỏ, kéo dài bóng của những hàng cây cổ thụ già cỗi. Đêm đông lạnh buốt, gió rít qua khe cửa sổ tạo nên âm thanh u uất. Trong căn phòng tĩnh mịch, chỉ còn tiếng tích tắc đều đặn của chiếc đồng hồ treo tường và hơi thở nhè nhẹ đầy mệt mỏi.</p>
            
            <p>Đây là chương thứ {chapterNumber} của câu chuyện kỳ thú mang tên <strong>{storyTitle}</strong>. Mọi nút thắt cốt truyện bắt đầu được hé lộ từ đây. Những mối ân oán tình thù đan xen giữa các thế lực lớn, những mưu đồ sâu kín của kẻ địch ngỡ như hoàn hảo nay bắt đầu xuất hiện những vết rạn nứt đầu tiên.</p>
            
            <p>Nhân vật chính khẽ thở dài, tay nắm chặt cốc trà nóng đã nguội lạnh từ lâu. Trong đầu anh hiện lên muôn vàn ký ức hỗn độn của những ngày tháng gian khổ đã qua. Từ một kẻ vô danh bị người đời khinh rẻ, trải qua bao sóng gió, nay anh đã đứng trước ngưỡng cửa của sự thay đổi vận mệnh lớn lao. Nhưng cái giá phải trả cho quyền lực và danh vọng chưa bao giờ là rẻ.</p>
            
            <blockquote>""Mỗi con đường chúng ta chọn đi qua đều để lại dấu chân sâu đậm. Dẫu có trắc trở ngàn trùng, việc quay đầu chưa bao giờ nằm trong suy nghĩ của ta.""</blockquote>
            
            <p>Phía xa xa, tiếng vó ngựa dồn dập vang lên phá tan bầu không khí yên lặng của màn đêm. Bụi cuốn mù mịt dưới ánh đuốc bập bùng. Tiếng hô hoán của quân lính, tiếng binh khí va chạm chan chát vang lên từ phía cổng thành. Cuộc chiến sinh tử cận kề buộc tất cả những người trong cuộc phải đưa ra lựa chọn cuối cùng.</p>
            
            <p>Anh đứng dậy, bước ra ngoài ban công, gió thổi lộng làm tà áo tung bay. Đôi mắt anh sâu thẳm nhìn về phía chân trời xa xăm, nơi những ánh lửa đỏ rực đang nhuộm màu cả góc trời đêm. Hành trình này chỉ mới bắt đầu, và những thử thách phía trước chắc chắn sẽ còn cam go hơn gấp bội lần.</p>
        ";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextOptions<ComicWeb.Persistence.Contexts.ApplicationDbContext>>();
            await using var context = new ComicWeb.Persistence.Contexts.ApplicationDbContext(db);

            // Wipes the old seeded stories if they contain the old short placeholder
            var firstChapter = await context.Chapters.FirstOrDefaultAsync(cancellationToken);
            if (firstChapter != null && firstChapter.Content.Contains("Đây là nội dung chương 1 của bộ truyện"))
            {
                logger.LogInformation("Detected old short placeholder chapters. Wiping stories and re-seeding...");
                context.Chapters.RemoveRange(context.Chapters);
                context.Stories.RemoveRange(context.Stories);
                await context.SaveChangesAsync(cancellationToken);
            }

            // Check if database has any stories already
            var count = await context.Stories.AsNoTracking().CountAsync(cancellationToken);
            if (count > 0)
            {
                logger.LogInformation("Database already has stories. Skipping story seeding.");
                return;
            }

            logger.LogInformation("Database is empty. Seeding high-fidelity demo stories...");

            var allGenres = await context.Genres.ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Define the 7 high-fidelity demo stories from the frontend
            var demoStories = new List<(string Title, string Slug, string CoverUrl, string Description, string AuthorName, StoryStatus Status, string[] GenreNames)>
            {
                (
                    "Chồng Sạch Sẽ Dùng Giấy Thấm Dầu Của Thư Ký, Tôi Sát Phạt Quyết Đoán",
                    "chong-sach-se-dung-giay-tham-dau-cua-thu-ky-toi-sat-phat-quyet-doan",
                    "/images/demo/cover-huyen-huyen-01.webp",
                    "Kết hôn 5 năm, người chồng Cố Trạch Xuyên nổi tiếng có chứng sạch sẽ nghiêm trọng, thế nhưng lại dùng chung giấy thấm dầu với nữ thư ký. Hứa Tri Hạ dứt khoát ly hôn, vượt qua muôn vàn âm mưu hãm hại để lột trần bộ mặt của nam và tiểu tam trước pháp luật. Một câu chuyện vạch trần báo thù vô cùng sảng khoái.",
                    "Thất Miêu",
                    StoryStatus.Completed,
                    new[] { "Huyền Huyễn", "Đô Thị", "Đấu Trí" }
                ),
                (
                    "Phiếu Ăn Năm Ngàn Tệ",
                    "phieu-an-nam-ngan-te",
                    "/images/demo/cover-do-thi-01.webp",
                    "Tăng ca đến nửa đêm than thở với bạn trai, anh ta liền chuyển khoản 5000 tệ và bảo 'anh nuôi em', nhưng hôm sau lại bị cô phát hiện anh ta đang chế giễu mình trong nhóm chat anh em. Cô mang theo bằng chứng nợ nần phản kích đầy ngoạn mục, lấy lại lòng tự tôn và hiểu rõ phiếu ăn đích thực nhất chính là tự bản thân mình.",
                    "Đông Phương",
                    StoryStatus.Published,
                    new[] { "Đô Thị", "Hệ Thống", "Khoa Huyễn" }
                ),
                (
                    "Bố Là Ma Vương Sửa Chữa",
                    "bo-la-ma-vuong-sua-chua",
                    "/images/demo/cover-tien-hiep-01.webp",
                    "Tôi từ nhỏ đã thích tháo dỡ đồ đạc, tốt nghiệp đi làm bảo mẫu lương tháng mười vạn. Ngày đầu đi làm dùng kẹp tăm mở khóa cửa phòng, không ngờ lại phát hiện cố chủ là nam thần thời học sinh Lục Kỳ An. Sau tai nạn bại liệt anh ta vô cùng cáu gắt, tôi dùng kỹ năng sửa chữa để cảm hóa và giúp anh ta đứng lên lần nữa.",
                    "Bán Hạ",
                    StoryStatus.Published,
                    new[] { "Tiên Hiệp", "Huyền Huyễn", "Đấu Khí" }
                ),
                (
                    "Năm Sát Phong Thần: Nữ Chính Ngược Văn Không Phương Bồi Nữa",
                    "nam-sat-phong-than-nu-chinh-nguoc-van-khong-phuong-boi-nua",
                    "/images/demo/cover-xuyen-khong-01.webp",
                    "Tô Uyển Tình là nữ chính trong truyện ngược, mẹ cô vì cứu nam chính Thẩm Cận Từ mà qua đời, bản thân cô thì rơi vào vòng lặp cốt truyện vô chậm. Tự sát hay phản kháng đều vô dụng, cho đến khi bầu trời phủ đầy đạn mạc hiển thị dòng chữ 'Giết hắn đi'. Cô đã giết hắn 5 lần để phá vỡ vòng lặp, giành lại cuộc đời.",
                    "Sở Cuồng",
                    StoryStatus.Completed,
                    new[] { "Xuyên Không", "Huyền Huyễn", "Trùng Sinh" }
                ),
                (
                    "Trường Mẫu Giáo Thần Núi",
                    "truong-mau-giao-than-nui",
                    "/images/demo/cover-he-thong-01.webp",
                    "Cô giáo mầm non thất nghiệp Tô Niệm vô tình lạc vào thôn Thanh Nhai, phát hiện học sinh ở đây toàn là rắn, bọ ngựa, thằn lằn lửa nhỏ yêu quái. Viện trưởng hồ yêu Hồ Ly ôn nhu bí ẩn, các em nhỏ ngây thơ đáng yêu cùng sự bảo hộ của Thần Núi mang đến câu chuyện ấm áp và ngọt ngào.",
                    "Mộc Tử",
                    StoryStatus.Published,
                    new[] { "Hệ Thống", "Huyền Huyễn", "Hành Động" }
                ),
                (
                    "Công Tử, Ám Vệ Của Ngài Trộm Nhà Rồi!",
                    "cong-tu-am-ve-cua-ngai-trom-nha-roi",
                    "/images/demo/hero-featured.webp",
                    "Xuyên thành ác độc nữ phụ, đạn mạc mách bảo nếu bò lên giường nam chính sẽ bị đưa đến chỗ nhân vật phản diện hung ác. Quyết định từ bỏ nam chính, ngược lại đi công lược 18 vị ám vệ bên cạnh công tử! Từ lão lục đến thập tam, ai nấy đều bị cô trêu chọc đến đỏ mặt.",
                    "Thanh Phong",
                    StoryStatus.Completed,
                    new[] { "Huyền Huyễn", "Đấu Trí", "Khoa Huyễn" }
                ),
                (
                    "Về Việc Tôi Bị Bắt Sau Khi Chết Vì Quá Nhớ Dai",
                    "ve-viec-toi-bi-bat-sau-khi-chet-vi-qua-nho-dai",
                    "/images/demo/cover-khoa-huyen-01.webp",
                    "Lâm Tiểu Thảo đột tử vì tăng ca xuống địa phủ, lại vì nhớ rõ mồn một thời gian chết mà bị bắt làm nghi phạm trộm thời gian. Cô phát hiện mình là 'người bấm giờ' hiếm gặp có khả năng ngưng đọng thời gian, từ đó bắt đầu hành trình phá án tại cõi âm vô cùng ly kỳ và hài hước.",
                    "Phong Thần",
                    StoryStatus.Published,
                    new[] { "Khoa Huyễn", "Đô Thị", "Huyền Huyễn" }
                )
            };

            foreach (var item in demoStories)
            {
                var story = new Story
                {
                    Title = item.Title,
                    Slug = item.Slug,
                    Description = item.Description,
                    CoverImageUrl = item.CoverUrl,
                    AuthorName = item.AuthorName,
                    CreateAt = now.AddDays(-30),
                    UpdateAt = now
                };

                // Add genres
                foreach (var gName in item.GenreNames)
                {
                    var genreEntity = allGenres.FirstOrDefault(g => g.Name.Equals(gName, StringComparison.OrdinalIgnoreCase));
                    if (genreEntity != null)
                    {
                        story.Genres.Add(genreEntity);
                    }
                }

                // Explicitly set publish status using domain methods
                if (item.Status == StoryStatus.Completed)
                {
                    story.Publish(now.AddDays(-30));
                    story.Complete(now);
                }
                else if (item.Status == StoryStatus.Published)
                {
                    story.Publish(now.AddDays(-20));
                }

                // Add 5 chapters for each story so there is content to read
                for (int i = 1; i <= 5; i++)
                {
                    var chapter = new Chapter
                    {
                        ChapterNumber = i,
                        Title = $"Chương {i}: Khởi đầu hành trình mới",
                        Slug = $"chuong-{i}",
                        Content = GetChapterContent(item.Title, i),
                        IsLocked = i > 3, // Lock chapters after chapter 3 to test VIP locking features
                        CreatedAt = now.AddDays(-30 + i),
                        UpdateAt = now
                    };
                    chapter.Publish(now.AddDays(-30 + i));
                    story.Chapters.Add(chapter);
                }

                context.Stories.Add(story);
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Successfully seeded {Count} demo stories with chapters", demoStories.Count);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            logger.LogError(ex, "Failed to seed demo stories because database is unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
