namespace PlikShare.Integrations.Aws.Textract;

public enum TextractFeature
{
    Tables = 0,
    Forms,
    Layout
}

public static class TextractFeatureExtensions
{
    public static List<string> ToAwsFormat(this IEnumerable<TextractFeature> features)
    {
        return features.Select(f => f.ToAwsFormat()).ToList();
    }

    public static string ToAwsFormat(this TextractFeature feature)
    {
        return feature switch
        {
            TextractFeature.Tables => "TABLES",
            TextractFeature.Forms => "FORMS",
            TextractFeature.Layout => "LAYOUT",

            _ => throw new ArgumentException($"Unsupported feature type: {feature}")
        };
    }
}