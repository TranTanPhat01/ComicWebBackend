using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Newsletter;

public record SubscribeNewsletterCommand(string Email) : IRequest;
public record UnsubscribeNewsletterCommand(string Email) : IRequest;

public class NewsletterHandler :
    IRequestHandler<SubscribeNewsletterCommand>,
    IRequestHandler<UnsubscribeNewsletterCommand>
{
    private readonly IApplicationDbContext _db;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public NewsletterHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(SubscribeNewsletterCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new AppException("EMAIL_REQUIRED", 400, "Validation failed", "Email address is required.");
        }

        var trimmedEmail = request.Email.Trim();
        if (!EmailRegex.IsMatch(trimmedEmail))
        {
            throw new AppException("INVALID_EMAIL", 400, "Validation failed", "Invalid email address format.");
        }

        var normalizedEmail = trimmedEmail.ToUpperInvariant();
        var subscriber = await _db.NewsletterSubscribers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (subscriber == null)
        {
            subscriber = new NewsletterSubscriber
            {
                Email = trimmedEmail,
                NormalizedEmail = normalizedEmail,
                IsSubscribed = true,
                SubscribedAt = DateTime.UtcNow
            };
            _db.NewsletterSubscribers.Add(subscriber);
        }
        else if (!subscriber.IsSubscribed)
        {
            subscriber.IsSubscribed = true;
            subscriber.SubscribedAt = DateTime.UtcNow;
            subscriber.UnsubscribedAt = null;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task Handle(UnsubscribeNewsletterCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new AppException("EMAIL_REQUIRED", 400, "Validation failed", "Email address is required.");
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var subscriber = await _db.NewsletterSubscribers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (subscriber != null && subscriber.IsSubscribed)
        {
            subscriber.IsSubscribed = false;
            subscriber.UnsubscribedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
