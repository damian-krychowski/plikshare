namespace PlikShare.Core.Utils;

public class OneTimeCode: IOneTimeCode
{
    private readonly Random _random = new();

    public string Generate()
    {
        return _random.Next(100000, 999999).ToString();
    }
}