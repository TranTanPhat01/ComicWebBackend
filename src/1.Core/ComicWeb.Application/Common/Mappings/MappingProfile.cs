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
            // Map Story -> StoryDto, converting Status enum to string explicitly
            CreateMap<Story, StoryDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<Chapter, ChapterListDto>();

            CreateMap<Story, StoryDetailDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        }

    }
}
