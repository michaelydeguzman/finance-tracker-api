using System.Net;
using FinanceTracker.Application.Services.Email;

namespace FinanceTracker.Application.Services.Households;

/// <summary>
/// The one message households send. Every interpolated value is HTML-encoded — a household
/// name and a display name are both user-controlled text, and an invitation is exactly the
/// kind of mail a stranger can cause to be sent to an address they do not own.
/// </summary>
public static class HouseholdEmailFactory
{
    public static EmailMessage Invitation(
        string toAddress,
        string householdName,
        string invitedByLabel,
        string link)
    {
        var safeLink = WebUtility.HtmlEncode(link);
        var safeHousehold = WebUtility.HtmlEncode(householdName);
        var safeInviter = WebUtility.HtmlEncode(invitedByLabel);

        var intro =
            $"{invitedByLabel} has invited you to share finances with \"{householdName}\" on Finance Tracker. "
            + "Joining lets everyone in the household see each other's income, expenses, categories and dashboard.";

        var footer =
            "If you were not expecting this, you can ignore the message - nothing is shared until you accept, "
            + "and the invitation expires on its own.";

        var html = $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:32rem;line-height:1.5">
              <h1 style="font-size:1.25rem;margin:0 0 1rem">You have been invited to a household</h1>
              <p style="margin:0 0 1.25rem">
                {safeInviter} has invited you to share finances with &ldquo;{safeHousehold}&rdquo; on Finance Tracker.
                Joining lets everyone in the household see each other&rsquo;s income, expenses, categories and dashboard.
              </p>
              <p style="margin:0 0 1.5rem">
                <a href="{safeLink}" style="background:#1f6f5c;color:#fff;padding:0.6rem 1.1rem;border-radius:4px;text-decoration:none;display:inline-block">
                  View invitation
                </a>
              </p>
              <p style="margin:0 0 1.25rem;font-size:0.875rem;color:#555">
                If the button does not work, paste this into your browser:<br>
                <span style="word-break:break-all">{safeLink}</span>
              </p>
              <p style="margin:0;font-size:0.875rem;color:#555">{WebUtility.HtmlEncode(footer)}</p>
            </div>
            """;

        var text = $"You have been invited to a household\n\n{intro}\n\n{link}\n\n{footer}";

        return new EmailMessage(toAddress, $"Join \"{householdName}\" on Finance Tracker", html, text);
    }
}
