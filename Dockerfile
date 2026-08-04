FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ComicWebBackend.sln", "./"]
COPY ["src/1.Core/ComicWeb.Domain/ComicWeb.Domain.csproj", "src/1.Core/ComicWeb.Domain/"]
COPY ["src/1.Core/ComicWeb.Application/ComicWeb.Application.csproj", "src/1.Core/ComicWeb.Application/"]
COPY ["src/2.Infrastructure/ComicWeb.Persistence/ComicWeb.Persistence.csproj", "src/2.Infrastructure/ComicWeb.Persistence/"]
COPY ["src/3.Presentation/ComicWeb.WebApi/ComicWeb.WebApi.csproj", "src/3.Presentation/ComicWeb.WebApi/"]

RUN dotnet restore \
    "src/3.Presentation/ComicWeb.WebApi/ComicWeb.WebApi.csproj"

COPY . .

RUN dotnet publish "src/3.Presentation/ComicWeb.WebApi/ComicWeb.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

ENTRYPOINT ["dotnet", "ComicWeb.WebApi.dll"]
