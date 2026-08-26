using FinanceTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Application.Services.Auth;

/// <summary>
/// Thin wrapper over ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>, which is
/// PBKDF2-HMAC-SHA512 with a per-hash salt and an embedded format marker — so a future
/// parameter change is detectable per row rather than a flag day.
///
/// Only the hasher is borrowed; none of the rest of the Identity stack is in play.
/// </summary>
public sealed class PasswordHashingService : IPasswordHashingService
{
    private static readonly User HashingSubject = new();

    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>A hash of a value nobody knows, used only to burn equivalent CPU time.</summary>
    private readonly string _decoyHash;

    public PasswordHashingService()
    {
        _decoyHash = _hasher.HashPassword(HashingSubject, Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => _hasher.HashPassword(HashingSubject, password);

    public PasswordVerificationOutcome Verify(string hash, string providedPassword) =>
        _hasher.VerifyHashedPassword(HashingSubject, hash, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Succeeded,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SucceededNeedsRehash,
            _ => PasswordVerificationOutcome.Failed
        };

    public void SimulateVerification() =>
        _hasher.VerifyHashedPassword(HashingSubject, _decoyHash, "not-the-password");
}
