namespace FinanceTracker.Application.Services.Auth;

public enum PasswordVerificationOutcome
{
    Failed,
    Succeeded,

    /// <summary>
    /// Correct password, but hashed with outdated parameters. The caller should rehash and
    /// store the result while it has the plaintext in hand.
    /// </summary>
    SucceededNeedsRehash
}

public interface IPasswordHashingService
{
    string Hash(string password);

    PasswordVerificationOutcome Verify(string hash, string providedPassword);

    /// <summary>
    /// Runs the same work as a real verification against a throwaway hash. Called when no
    /// account matches, so that "unknown email" and "wrong password" take indistinguishable
    /// time and cannot be told apart by a caller probing for valid addresses.
    /// </summary>
    void SimulateVerification();
}
