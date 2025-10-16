namespace HWInventory.Application.Abstractions;

public interface ISessionTracker
{
    Task<UserSessionDto> RegisterAsync(RegisterSessionRequest request, CancellationToken cancellationToken = default);
    Task<bool> TouchAsync(string sessionIdentifier, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid sessionId, string performedBy, string? reason, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(Guid userId, string performedBy, CancellationToken cancellationToken = default);
}

public record RegisterSessionRequest(Guid UserId, string SessionIdentifier, string PerformedBy, string? IpAddress, string? UserAgent);

public record UserSessionDto(Guid Id, string SessionIdentifier, DateTime IssuedAtUtc, DateTime LastSeenAtUtc, string? IpAddress, string? UserAgent, bool IsRevoked);
