using FluentAssertions;
using FluentAssertions.Primitives;

namespace PlikShare.IntegrationTests.Infrastructure;

public static class FluentAssertionsExtensions
{
    public static AndConstraint<StringAssertions> BeEquivalentUrl(
        this StringAssertions assertions,
        string expected,
        string because = "",
        params object[] becauseArgs)
    {
        var subject = assertions.Subject;

        subject.Should().NotBeNullOrEmpty(because, becauseArgs);

        new Uri(subject!)
            .Should()
            .Be(new Uri(expected), because, becauseArgs);

        return new AndConstraint<StringAssertions>(assertions);
    }
}
