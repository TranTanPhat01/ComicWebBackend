using AutoMapper;
using ComicWeb.Application.Dtos;
using ComicWeb.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Cấu hình map tự động từ Entity sang DTO gọn nhẹ
            // AutoMapper tự hiểu cách chuyển Enum Status thành String tương ứng
            CreateMap<Story, StoryDto>();

            CreateMap<Chapter, ChapterListDto>();

            CreateMap<Story, StoryDetailDto>();
        }

    }
}
