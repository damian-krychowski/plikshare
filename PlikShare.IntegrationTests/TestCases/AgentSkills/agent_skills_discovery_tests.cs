using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Flurl.Http;
using PlikShare.AgentSkills;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AgentSkills;

[Collection(IntegrationTestsCollection.Name)]
public class agent_skills_discovery_tests : TestFixture
{
    public agent_skills_discovery_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task index_json_matches_discovery_spec_and_skill_md_digest()
    {
        // when
        var indexJson = await HostFixture.FlurlClient
            .Request($"{AppUrl}/.well-known/agent-skills/index.json")
            .GetStringAsync();

        // then
        using var doc = JsonDocument.Parse(indexJson);
        var root = doc.RootElement;

        root.GetProperty("$schema").GetString()
            .Should().Be(AgentSkillsEndpoints.SchemaVersion);

        var skills = root.GetProperty("skills");
        skills.GetArrayLength().Should().Be(1);

        var skill = skills[0];
        skill.GetProperty("name").GetString().Should().Be(PlikShareAgentSkill.Name);
        skill.GetProperty("type").GetString().Should().Be("skill-md");
        skill.GetProperty("description").GetString().Should().NotBeNullOrEmpty();

        var url = skill.GetProperty("url").GetString();
        url.Should().EndWith($"/.well-known/agent-skills/{PlikShareAgentSkill.Name}/SKILL.md");

        var digest = skill.GetProperty("digest").GetString();
        digest.Should().MatchRegex("^sha256:[0-9a-f]{64}$");

        // and when
        var skillMarkdown = await HostFixture.FlurlClient
            .Request(url!)
            .GetStringAsync();

        // then
        skillMarkdown.Should().StartWith("---");
        skillMarkdown.Should().Contain($"name: {PlikShareAgentSkill.Name}");

        var computedDigest = "sha256:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(skillMarkdown)))
            .ToLowerInvariant();

        computedDigest.Should().Be(digest);
    }

    [Fact]
    public async Task unknown_skill_returns_404()
    {
        var response = await HostFixture.FlurlClient
            .Request($"{AppUrl}/.well-known/agent-skills/does-not-exist/SKILL.md")
            .AllowAnyHttpStatus()
            .GetAsync();

        response.StatusCode.Should().Be(404);
    }
}
