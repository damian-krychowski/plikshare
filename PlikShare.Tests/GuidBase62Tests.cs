using FluentAssertions;
using PlikShare.Core.Utils;

namespace PlikShare.Tests;

public class GuidBase62Tests
{
    [Fact]
    public void can_convert_guid_to_base62_and_back()
    {
        var guid = Guid.NewGuid();
        var base62 = guid.ToBase62();
        var guidConvertedBack = GuidBase62.FromBase62ToGuid(base62);

        guidConvertedBack.Should().Be(guid);
    }
}