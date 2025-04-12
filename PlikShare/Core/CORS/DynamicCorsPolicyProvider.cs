using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Configuration;
using PlikShare.Files.PreSignedLinks;

namespace PlikShare.Core.CORS;

public class DynamicCorsPolicyProvider(
    IConfig config,
    IOptions<CorsOptions> options,
    BoxLinkCache boxLinkCache,
    PreSignedUrlsService preSignedUrlsService) : ICorsPolicyProvider
{
    private readonly CorsOptions _options = options.Value;

    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        string? origin = context.Request.Headers.Origin;

        if (!string.IsNullOrEmpty(origin) && origin == config.AppUrl)
        {
            return Task.FromResult(_options.GetPolicy(_options.DefaultPolicyName));
        }

        if (policyName == CorsPolicies.BoxLink)
        {
            return HandleBoxLinkPolicy(context);
        }

        if (policyName == CorsPolicies.PreSignedLink)
        {
            return HandlePreSignedLinkPolicy(context);
        }

        return Task.FromResult(_options.GetPolicy(policyName ?? _options.DefaultPolicyName));
    }

    private async Task<CorsPolicy?> HandleBoxLinkPolicy(
        HttpContext context)
    {
        var accessCodeStr = context.Request.RouteValues["accessCode"]?.ToString();

        if (string.IsNullOrWhiteSpace(accessCodeStr))
            return _options.GetPolicy(_options.DefaultPolicyName);

        var boxLink = await boxLinkCache.TryGetBoxLink(
            accessCode: accessCodeStr,
            cancellationToken: context.RequestAborted);

        return GetPolicyForBoxLink(
            boxLink: boxLink);
    }

    private async Task<CorsPolicy?> HandlePreSignedLinkPolicy(
        HttpContext context)
    {
        var protectedPayload = context.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedPayload))
            return _options.GetPolicy(_options.DefaultPolicyName);

        var (success, boxLinkId) = preSignedUrlsService.TryExtractBoxLinkIdFromProtectedData(
            protectedPayload);

        if(!success)
            return _options.GetPolicy(_options.DefaultPolicyName);

        if(boxLinkId is null)
            return _options.GetPolicy(_options.DefaultPolicyName);

        var boxLink = await boxLinkCache.TryGetBoxLink(
            boxLinkId: boxLinkId.Value,
            cancellationToken: context.RequestAborted);
        
        return GetPolicyForBoxLink(
            boxLink: boxLink);
    }

    private CorsPolicy? GetPolicyForBoxLink(BoxLinkContext? boxLink)
    {
        if (boxLink is null)
            return _options.GetPolicy(_options.DefaultPolicyName);

        if (boxLink.WidgetOrigins is null)
            return _options.GetPolicy(_options.DefaultPolicyName);

        var policyBuilder = new CorsPolicyBuilder()
            .WithOrigins(boxLink.WidgetOrigins.ToArray())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();

        var policy = policyBuilder.Build();

        return policy;
    }
}