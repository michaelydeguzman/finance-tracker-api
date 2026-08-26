using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services.Auth;

public sealed record AccessToken(string Value, DateTime ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
