using PlikShare.Core.Utils;

namespace PlikShare.IntegrationTests.Infrastructure.Mocks;

public class OneTimeCodeMock : IOneTimeCode
{
    private OneTimeCode _realOneTimeCode = new();
    
    public string? Code { get; private set; } 
    
    public void NextCodeToGenerate(string code)
    {
        Code = code;
    }
    
    public string Generate()
    {
        return Code ?? _realOneTimeCode.Generate();
    }
}