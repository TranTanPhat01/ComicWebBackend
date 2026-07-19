    using ComicWeb.Application.Common.Interface;
    using ComicWeb.Application.Dtos;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace ComicWeb.Application.Features.Stories.Queries
    {
        public record GetChapterDetailQuery(int ChapterId) : IRequest<ChapterDetailResultDto?>;

    public class GetChapterDetailQueryHandler : IRequestHandler<GetChapterDetailQuery, ChapterDetailResultDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetChapterDetailQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChapterDetailResultDto?> Handle(GetChapterDetailQuery request, CancellationToken cancellationToken)
        {
            var chapter = await _context.Chapters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ChapterId, cancellationToken);

            if (chapter == null) return null;

            // 1. Trường hợp chương bị khóa: Giấu Content và trả về Affiliate Link để Frontend bắt click
            if (chapter.IsLocked)
            {
                return new ChapterDetailResultDto(
                    chapter.Id,
                    chapter.ChapterNumber,
                    chapter.Title,
                    null, // Giấu nội dung chữ của truyện
                    chapter.AffiliateLink,
                    true
                );
            }

            // 2. BỔ SUNG TẠI ĐÂY: Trường hợp chương miễn phí công khai (hoặc đã mở khóa)
            return new ChapterDetailResultDto(
                chapter.Id,
                chapter.ChapterNumber,
                chapter.Title,
                chapter.Content, // Trả đầy đủ nội dung truyện
                null,            // Không cần kèm link affiliate quảng cáo
                false
            );
        }
    }
}
