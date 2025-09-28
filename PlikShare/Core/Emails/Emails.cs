using System.Text;
using Flurl;

namespace PlikShare.Core.Emails;

public static class Emails
{
    public static (string Title, string Content) EmailProviderConfirmation(
        string applicationName,
        string emailProviderName,
        string confirmationCode)
    {
        return (
            Title: $"{applicationName} - Confirm new email provider",
            Content: $"Use following code, to confirm '{emailProviderName}' email provider: " +
                     $"<br> {confirmationCode}"
        );
    }

    public static (string Title, string Content) UserInvitation(
        string applicationName,
        string appUrl,
        string inviterEmail,
        string invitationCode)
    {
        var msgBuilder = new StringBuilder();

        msgBuilder.Append($"User <strong>{inviterEmail}</strong> has invited you to join his PlikShare application.");
        msgBuilder.Append("<br><br>");
        msgBuilder.Append("Use the following link to register to the website:");
        msgBuilder.Append("<br><br>");

        var link = new Url(appUrl)
            .AppendPathSegment("sign-up")
            .AppendQueryParam("invitationCode", invitationCode)
            .ToString();

        msgBuilder.Append(link);

        return (
            Title: $"{applicationName} - you were invited",
            Content: msgBuilder.ToString()
        );
    }

    public static (string Title, string Content) WorkspaceMembershipInvitation(
        string applicationName,
        string appUrl,
        string inviterEmail,
        string workspaceName,
        string? invitationCode)
    {

        var msgBuilder = new StringBuilder();
        msgBuilder.Append($"User <strong>{inviterEmail}</strong> has invited you to join <strong>'{workspaceName}'</strong> Workspace.");

        if (invitationCode is not null)
        {
            msgBuilder.Append("<br><br>");
            msgBuilder.Append("Use the following link to register to the website:");
            msgBuilder.Append("<br><br>");

            var link = new Url(appUrl)
                .AppendPathSegment("sign-up")
                .AppendQueryParam("invitationCode", invitationCode)
                .ToString();

            msgBuilder.Append(link);
        }

        msgBuilder.Append("<br><br>");
        msgBuilder.Append($"You can find the invitation here: <br/> <a href=\"{appUrl}/workspaces\">{appUrl}/workspaces</a>");

        return (
            Title: $"{applicationName} - you were invited to Workspace",
            Content: msgBuilder.ToString()
        );
    }

    public static (string Title, string Content) WorkspaceMembershipInvitationAccepted(
        string applicationName,
        string inviteeEmail,
        string workspaceName)
    {
        return (
            Title: $"{applicationName} - your invitation was accepted",
            Content: $"User <strong>{inviteeEmail}</strong> has accepted your invitation to join <strong>'{workspaceName}'</strong> Workspace."
        );
    }

    public static (string Title, string Content) WorkspaceMembershipInvitationRejected(
        string applicationName,
        string memberEmail,
        string workspaceName)
    {
        return (
            Title: $"{applicationName} - workspace invitation was rejected",
            Content: $"User <strong>{memberEmail}</strong> has rejected your invitation to join <strong>'{workspaceName}'</strong> Workspace."
        );
    }

    public static (string Title, string Content) WorkspaceMembershipRevoked(
        string applicationName,
        string workspaceName)
    {
        return (
            Title: $"{applicationName} - access to workspace was revoked",
            Content: $"Your access to <strong>'{workspaceName}'</strong> Workspace was revoked."
        );
    }

    public static (string Title, string Content) WorkspaceMemberLeft(
        string applicationName,
        string memberEmail,
        string workspaceName)
    {
        return (
            Title: $"{applicationName} - user left your workspace",
            Content: $"User <strong>{memberEmail}</strong> has left <strong>'{workspaceName}'</strong> Workspace."
        );
    }

    public static (string Title, string Content) BoxMembershipInvitation(
        string applicationName,
        string appUrl,
        string inviterEmail,
        string boxName,
        string? invitationCode)
    {
        var msgBuilder = new StringBuilder();

        msgBuilder.Append($"User <strong>{inviterEmail}</strong> has invited you to join <strong>'{boxName}'</strong> File Box.");

        if (invitationCode is not null)
        {
            msgBuilder.Append("<br><br>");
            msgBuilder.Append("Use the following link to register to the website:");
            msgBuilder.Append("<br><br>");

            var link = new Url(appUrl)
                .AppendPathSegment("sign-up")
                .AppendQueryParam("invitationCode", invitationCode)
                .ToString();

            msgBuilder.Append(link);
        }

        msgBuilder.Append("<br><br>");
        msgBuilder.Append($"You can find the invitation here: <br/> <a href=\"{appUrl}/workspaces\">{appUrl}/workspaces</a>");

        return (
            Title: $"{applicationName} - you were invited to file box",
            Content: msgBuilder.ToString()
        );
    }

    public static (string Title, string Content) BoxMembershipInvitationAccepted(
        string applicationName,
        string inviteeEmail,
        string boxName)
    {
        return (
            Title: $"{applicationName} - box invitation was accepted",
            Content: $"User <strong>{inviteeEmail}</strong> has accepted your invitation to join <strong>'{boxName}'</strong> File Box."
        );
    }

    public static (string Title, string Content) BoxMembershipInvitationRejected(
        string applicationName,
        string inviteeEmail,
        string boxName)
    {
        return (
            Title: $"{applicationName} - box invitation was rejected",
            Content: $"User <strong>{inviteeEmail}</strong> has rejected your invitation to join <strong>'{boxName}'</strong> File Box."
        );
    }

    public static (string Title, string Content) BoxMembershipRevoked(
        string applicationName,
        string boxName)
    {
        return (
            Title: $"{applicationName} - your access to file box was revoked",
            Content: $"Your access to <strong>'{boxName}'</strong> File Box was revoked."
        );
    }

    public static (string Title, string Content) BoxMemberLeft(
        string applicationName,
        string memberEmail,
        string boxName)
    {
        return (
            Title: $"{applicationName} - user left your file box",
            Content: $"User <strong>{memberEmail}</strong> has left <strong>'{boxName}'</strong> File Box."
        );
    }

    public static (string Title, string Content) ConfirmationEmail(
        string applicationName,
        string link)
    {
        return (
            Title: $"{applicationName} - confirm your email",
            Content: "Click this link to confirm your email address: " +
                     "<br><br>" +
                     link
        );
    }

    public static (string Title, string Content) ResetPassword(
        string applicationName,
        string link)
    {
        return (
            Title: $"{applicationName} - reset your password",
            Content: "Click this link to reset your password: " +
                     "<br><br>" +
                     link
        );
    }
}