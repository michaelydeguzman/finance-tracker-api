using System.Net;
using FinanceTracker.Application.Services.Email;

namespace FinanceTracker.Application.Services.Auth;

/// <summary>
/// Composes the three emailed flows. Every interpolated value is HTML-encoded: a display
/// name is user-controlled text, and it must never be able to inject markup into a message
/// that also carries a working sign-in link.
/// </summary>
public static class AuthEmailFactory
{
    public static EmailMessage EmailVerification(string toAddress, string link) =>
        Build(
            toAddress,
            "Confirm your email address",
            "Confirm your email address",
            "Use the link below to confirm this address for your Finance Tracker account.",
            "Confirm email",
            link,
            "If you did not create an account, you can ignore this message.");

    public static EmailMessage MagicLink(string toAddress, string link) =>
        Build(
            toAddress,
            "Your sign-in link",
            "Sign in to Finance Tracker",
            "Use the link below to sign in. It can be used once, and expires shortly.",
            "Sign in",
            link,
            "If you did not request this, someone may have entered your address by mistake. No action is needed.");

    public static EmailMessage PasswordReset(string toAddress, string link) =>
        Build(
            toAddress,
            "Reset your password",
            "Reset your password",
            "Use the link below to choose a new password. It can be used once.",
            "Reset password",
            link,
            "If you did not request this, your password has not changed and no action is needed.");

    /// <summary>
    /// Sent when someone tries to register an address that already has an account. Registration
    /// answers identically whether or not the address is known, so this is what makes that
    /// silence safe: the real owner still finds out, and a prober learns nothing.
    /// </summary>
    public static EmailMessage AccountAlreadyExists(string toAddress, string resetLink) =>
        Build(
            toAddress,
            "Someone tried to create an account with your email",
            "You already have an account",
            "Someone just tried to sign up using this address. If that was you, sign in instead — "
                + "or use the link below if you have forgotten your password.",
            "Reset password",
            resetLink,
            "If this was not you, no action is needed. Your account has not changed.");

    private static EmailMessage Build(
        string toAddress,
        string subject,
        string heading,
        string intro,
        string callToAction,
        string link,
        string footer)
    {
        var safeLink = WebUtility.HtmlEncode(link);

        var html = $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:32rem;line-height:1.5">
              <h1 style="font-size:1.25rem;margin:0 0 1rem">{WebUtility.HtmlEncode(heading)}</h1>
              <p style="margin:0 0 1.25rem">{WebUtility.HtmlEncode(intro)}</p>
              <p style="margin:0 0 1.5rem">
                <a href="{safeLink}" style="background:#1f6f5c;color:#fff;padding:0.6rem 1.1rem;border-radius:4px;text-decoration:none;display:inline-block">
                  {WebUtility.HtmlEncode(callToAction)}
                </a>
              </p>
              <p style="margin:0 0 1.25rem;font-size:0.875rem;color:#555">
                If the button does not work, paste this into your browser:<br>
                <span style="word-break:break-all">{safeLink}</span>
              </p>
              <p style="margin:0;font-size:0.875rem;color:#555">{WebUtility.HtmlEncode(footer)}</p>
            </div>
            """;

        var text = $"{heading}\n\n{intro}\n\n{link}\n\n{footer}";

        return new EmailMessage(toAddress, subject, html, text);
    }
}
