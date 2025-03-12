namespace PlikShare.EmailProviders.Entities;

public record EmailProviderType
{
    public string Value { get; }
    
    private EmailProviderType(string value)
    {
        Value = value;
    }

    public static EmailProviderType AwsSes { get; } = new("aws-ses");
    public static EmailProviderType Resend { get; } = new("resend");
    public static EmailProviderType Smtp { get; } = new("smtp");

    public static EmailProviderType Build(string type)
    {
        return type switch
        {
            "aws-ses" => AwsSes,
            "resend" => Resend,
            "smtp" => Smtp,
            _ => throw new InvalidOperationException($"Unknown type '{type}' of Email Provider.")
        };
    }
}