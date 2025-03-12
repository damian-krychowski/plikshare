using PlikShare.Core.Utils;

namespace PlikShare.Core.Emails.Templates;

public static class AlertEmailTemplate
{
    private static readonly string Template;
    
    static AlertEmailTemplate()
    {
        Template = ManifestResourceReader.Read(
            "PlikShare.Core.Emails.Templates.alert-email.html");
    }

    public static string Build(string title, DateTimeOffset eventDateTime, string content)
    {
        return Template
            .Replace("##Title##", title)
            .Replace("##EventDateTime##", eventDateTime.ToString("O"))
            .Replace("##Content##", content);
    }
}