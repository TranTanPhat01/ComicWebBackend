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
            CreateMap<Story, StoryDto>();

            CreateMap<Chapter, ChapterListDto>();

            CreateMap<Story, StoryDetailDto>();
        }

    }
}
