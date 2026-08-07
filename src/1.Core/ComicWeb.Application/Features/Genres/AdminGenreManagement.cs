using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Genres;

public sealed record GenreListItemDto(int Id, string Name, string Slug, bool IsActive, int StoryCount);
public sealed record GetAdminGenresQuery() : IRequest<IReadOnlyList<GenreListItemDto>>;
public sealed record CreateGenreCommand(string Name, string? Slug, string? Description, bool IsActive = true) : IRequest<GenreListItemDto>;
public sealed record UpdateGenreCommand(int Id, string Name, string? Slug, string? Description, bool IsActive) : IRequest<GenreListItemDto>;
public sealed record DeleteGenreCommand(int Id) : IRequest;

public sealed class AdminGenreManagementHandler :
    IRequestHandler<GetAdminGenresQuery, IReadOnlyList<GenreListItemDto>>,
    IRequestHandler<CreateGenreCommand, GenreListItemDto>,
    IRequestHandler<UpdateGenreCommand, GenreListItemDto>,
    IRequestHandler<DeleteGenreCommand>
{
    private readonly IApplicationDbContext _db;

    public AdminGenreManagementHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<GenreListItemDto>> Handle(GetAdminGenresQuery request, CancellationToken ct)
    {
        return await _db.Genres.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new GenreListItemDto(x.Id, x.Name, x.Slug, x.IsActive, x.Stories.Count))
            .ToListAsync(ct);
    }

    public async Task<GenreListItemDto> Handle(CreateGenreCommand request, CancellationToken ct)
    {
        Validate(request.Name);
        var slug = NormalizeSlug(request.Slug, request.Name);
        if (await _db.Genres.AnyAsync(x => x.Slug == slug, ct)) throw Bad("GENRE_SLUG_CONFLICT", "Genre slug already exists.", 409);

        var genre = new Genre { Name = request.Name.Trim(), Slug = slug, Description = request.Description?.Trim(), IsActive = request.IsActive };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(ct);
        return new GenreListItemDto(genre.Id, genre.Name, genre.Slug, genre.IsActive, 0);
    }

    public async Task<GenreListItemDto> Handle(UpdateGenreCommand request, CancellationToken ct)
    {
        Validate(request.Name);
        var genre = await _db.Genres.FirstOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw Bad("GENRE_NOT_FOUND", "Genre was not found.", 404);
        var slug = NormalizeSlug(request.Slug, request.Name);
        if (await _db.Genres.AnyAsync(x => x.Id != request.Id && x.Slug == slug, ct)) throw Bad("GENRE_SLUG_CONFLICT", "Genre slug already exists.", 409);

        genre.Name = request.Name.Trim();
        genre.Slug = slug;
        genre.Description = request.Description?.Trim();
        genre.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return new GenreListItemDto(genre.Id, genre.Name, genre.Slug, genre.IsActive, genre.Stories.Count);
    }

    public async Task Handle(DeleteGenreCommand request, CancellationToken ct)
    {
        var genre = await _db.Genres.Include(x => x.Stories).FirstOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw Bad("GENRE_NOT_FOUND", "Genre was not found.", 404);
        if (genre.Stories.Any())
        {
            throw Bad("GENRE_IN_USE", $"Cannot delete genre that is in use by {genre.Stories.Count} stories. Deactivate it instead.", 400);
        }
        _db.Genres.Remove(genre);
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            throw Bad("INVALID_GENRE_NAME", "Genre name is required and must be 120 characters or fewer.", 400);
        }
    }

    private static string NormalizeSlug(string? requested, string name)
    {
        var raw = string.IsNullOrWhiteSpace(requested) ? name : requested;
        var slug = raw.Trim().ToLowerInvariant().Replace(" ", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9]+", "-");
        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "genre" : slug;
    }

    private static AppException Bad(string code, string detail, int status) => new(code, status, status == 404 ? "Not found" : status == 409 ? "Conflict" : "Validation failed", detail);
}
