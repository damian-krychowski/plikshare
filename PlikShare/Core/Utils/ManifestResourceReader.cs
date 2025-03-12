using System.Reflection;

namespace PlikShare.Core.Utils;

public static class ManifestResourceReader
{
    public static string Read(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(name);

        if (stream == null)
        {
            throw new InvalidOperationException($"Cannot find '{name}' email template");
        }

        using var reader = new StreamReader(stream);
            
        return reader.ReadToEnd();
    }
}