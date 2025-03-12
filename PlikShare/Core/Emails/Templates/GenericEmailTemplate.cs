using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Emails.Templates;

public class GenericEmailTemplate
{
    private readonly string _template;
    
    public GenericEmailTemplate(
        IConfig config)
    {
        var rawTemplate = ManifestResourceReader.Read(
            "PlikShare.Core.Emails.Templates.generic-email.html");

        _template = rawTemplate.Replace("##AppUrl##", config.AppUrl);
    }

    public string Build(string title, string content)
    {
        return _template
            .Replace("##Title##", title)
            .Replace("##Content##", content);
    }
}