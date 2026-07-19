using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ComicWeb.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            //Đăng kí AutoMapper quét qua toàn bộ Assembly này
            services.AddAutoMapper(Assembly.GetExecutingAssembly());


            //Đăng kí MediatR quét qua toàn bộ các Handler trong assembly này
            services.AddMediatR(static cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

            return services;
        }
    }
}
