using System.Text;
using PlikShare.AgentSkills.Contracts;
using PlikShare.Core.Configuration;

namespace PlikShare.AgentSkills;

public static class AgentSkillsEndpoints
{
    public const string SchemaVersion = "https://schemas.agentskills.io/discovery/0.2.0/schema.json";

    public static void MapAgentSkillsEndpoints(this WebApplication app)
    {
        var group = app
            .MapGroup("/.well-known/agent-skills")
            .WithTags("AgentSkills")
            .AllowAnonymous();

        group.MapMethods("/index.json", ["GET", "HEAD"], GetIndex)
            .WithName("GetAgentSkillsIndex");

        group.MapMethods("/{skillName}/SKILL.md", ["GET", "HEAD"], GetSkillMarkdown)
            .WithName("GetAgentSkillMarkdown");
    }

    private static IResult GetIndex(
        HttpContext httpContext,
        IConfig config)
    {
        var appUrl = config.AppUrl.TrimEnd('/');

        var skillBytes = PlikShareAgentSkill.GetSkillMarkdownBytes();

        var index = new AgentSkillsIndexDto
        {
            Schema = SchemaVersion,
            Skills =
            [
                new AgentSkillEntryDto
                {
                    Name = PlikShareAgentSkill.Name,
                    Type = "skill-md",
                    Description = PlikShareAgentSkill.Description,
                    Url = $"{appUrl}/.well-known/agent-skills/{PlikShareAgentSkill.Name}/SKILL.md",
                    Digest = PlikShareAgentSkill.ComputeDigest(skillBytes)
                }
            ]
        };

        ApplyDiscoveryHeaders(httpContext);

        return TypedResults.Ok(index);
    }

    private static IResult GetSkillMarkdown(
        string skillName,
        HttpContext httpContext)
    {
        if (skillName != PlikShareAgentSkill.Name)
            return Results.NotFound();

        ApplyDiscoveryHeaders(httpContext);

        var markdown = PlikShareAgentSkill.BuildSkillMarkdown();

        return Results.Text(
            content: markdown,
            contentType: "text/markdown",
            contentEncoding: Encoding.UTF8);
    }

    private static void ApplyDiscoveryHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
        httpContext.Response.Headers.CacheControl = "public, max-age=300";
    }
}
