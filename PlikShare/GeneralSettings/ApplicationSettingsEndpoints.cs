using Microsoft.AspNetCore.Authorization;
using PlikShare.Core.Authorization;
using PlikShare.GeneralSettings.GetStatus;
using PlikShare.GeneralSettings.GetStatus.Contracts;
using PlikShare.Users.Middleware;

namespace PlikShare.GeneralSettings;

public static class ApplicationSettingsEndpoints
{
    public static void MapApplicationSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/application-settings")
            .WithTags("Application Settings")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}",
            });

        group.MapGet("/status", GetStatus)
            .WithName("GetApplicationSettingsStatus");
    }

    private static IResult GetStatus(
        HttpContext httpContext,
        GetApplicationSettingsStatusQuery getApplicationSettingsStatusQuery)
    {
        var user = httpContext.GetUserContext();

        if (!user.Roles.IsAppOwner && user.Permissions is
            {
                CanManageStorages: false,
                CanManageEmailProviders: false
            })
        {
            return Results.Ok(new GetApplicationSettingsStatusResponseDto
            {
                IsEmailProviderConfigured = null,
                IsStorageConfigured = null
            });
        }

        var status = getApplicationSettingsStatusQuery.Execute();
        
        return Results.Ok(new GetApplicationSettingsStatusResponseDto
        {
            IsEmailProviderConfigured = user.Roles.IsAppOwner || user.Permissions.CanManageEmailProviders
                ? status.IsEmailProviderConfigured
                : null,

            IsStorageConfigured = user.Roles.IsAppOwner || user.Permissions.CanManageStorages
                ? status.IsStorageConfigured
                : null
        });
    }
}