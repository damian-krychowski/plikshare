using PlikShare.Users.Invite;

namespace PlikShare.IntegrationTests.Infrastructure.Mocks;

public class OneTimeInvitationCodeMock : IOneTimeInvitationCode
{
    private readonly OneTimeInvitationCode _realOneTimeInvitationCode = new();
    private readonly Queue<string> _predefinedCodes = new();

    public void AddCode(string code)
    {
        _predefinedCodes.Enqueue(code);
    }

    public void AddCodes(IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            _predefinedCodes.Enqueue(code);
        }
    }

    public string Generate()
    {
        return _predefinedCodes.Count > 0
            ? _predefinedCodes.Dequeue()
            : _realOneTimeInvitationCode.Generate();
    }
}