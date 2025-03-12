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

        msgBuilder.Append(
            $"User <strong>{inviterEmail}</strong> has invited you to join his PlikShare application.");

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
}