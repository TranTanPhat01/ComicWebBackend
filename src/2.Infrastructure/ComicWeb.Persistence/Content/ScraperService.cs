using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Dtos;

namespace ComicWeb.Persistence.Content
{
    public sealed class ScraperService : IScraperService
    {
        private readonly IEnumerable<IScraperEngine> _engines;

        public ScraperService(IEnumerable<IScraperEngine> engines)
        {
            _engines = engines;
        }

        public Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct)
        {
            var engine = GetEngine(storyUrl);
            return engine.ScrapeStoryMetadataAsync(storyUrl, ct);
        }

        public Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct)
        {
            var engine = GetEngine(chapterUrl);
            return engine.ScrapeChapterContentAsync(chapterUrl, ct);
        }

        private IScraperEngine GetEngine(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Đường dẫn (URL) không được để trống.", nameof(url));
            }

            var engine = _engines.FirstOrDefault(e => e.CanHandle(url));
            if (engine == null)
            {
                throw new NotSupportedException("Nguồn truyện này hiện chưa được hệ thống hỗ trợ. Vui lòng nhập link thuộc trang nguontruyen.com hoặc truyenfull.");
            }

            return engine;
        }
    }
}
