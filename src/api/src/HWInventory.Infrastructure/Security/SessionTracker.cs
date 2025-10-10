using System;
using System.Collections.Generic;
using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class SessionTracker : ISessionTracker
{
    private readonly IAppDbContext _dbContext;

    public SessionTracker(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserSessionDto> RegisterAsync(RegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = string.IsNullOrWhiteSpace(request.PerformedBy) ? "system" : request.PerformedBy;
        var session = new UserSession
        {
            AppUserId = request.UserId,
            SessionIdentifier = request.SessionIdentifier,
            IssuedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            CreatedBy = actor,
            ModifiedBy = actor
        };

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserSessionDto(session.Id, session.SessionIdentifier, session.IssuedAtUtc, session.LastSeenAtUtc, session.IpAddress, session.UserAgent, session.IsRevoked);
    }

    public async Task<bool> TouchAsync(string sessionIdentifier, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.UserSessions.FirstOrDefaultAsync(x => x.SessionIdentifier == sessionIdentifier, cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (session.IsRevoked)
        {
            return false;
        }

        session.LastSeenAtUtc = DateTime.UtcNow;
        session.ModifiedBy = session.ModifiedBy ?? "system";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return !session.IsRevoked;
    }

    public async Task<IReadOnlyCollection<UserSessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions
            .Where(x => x.AppUserId == userId && !x.IsRevoked)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Select(x => new UserSessionDto(x.Id, x.SessionIdentifier, x.IssuedAtUtc, x.LastSeenAtUtc, x.IpAddress, x.UserAgent, x.IsRevoked))
            .ToListAsync(cancellationToken);

        return sessions;
    }

    public async Task RevokeAsync(Guid sessionId, string performedBy, string? reason, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.IsRevoked = true;
        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedBy = performedBy;
        session.TerminationReason = reason;
        session.ModifiedBy = performedBy;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(Guid userId, string performedBy, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions.Where(x => x.AppUserId == userId && !x.IsRevoked).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAtUtc = now;
            session.RevokedBy = performedBy;
            session.ModifiedBy = performedBy;
            session.TerminationReason = "Revoked by administrator";
        }

        if (sessions.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
