using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuthProviders.Activate;
using PlikShare.AuthProviders.Create;
using PlikShare.AuthProviders.Create.Contracts;
using PlikShare.AuthProviders.Deactivate;
using PlikShare.AuthProviders.Delete;
using PlikShare.AuthProviders.Entities;
using PlikShare.AuthProviders.Id;
using PlikShare.AuthProviders.List;
using PlikShare.AuthProviders.List.Contracts;
using PlikShare.AuthProviders.PasswordLogin;
using PlikShare.AuthProviders.PasswordLogin.Contracts;
using PlikShare.AuthProviders.TestConfiguration;
using PlikShare.AuthProviders.TestConfiguration.Contracts;
using PlikShare.AuthProviders.Update;
using PlikShare.AuthProviders.Update.Contracts;
using PlikShare.AuthProviders.UpdateName;
using PlikShare.AuthProviders.UpdateName.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;

namespace PlikShare.AuthProviders;

public static class AuthProvidersEndpoints
{
    public static void MapAuthProvidersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth-providers")
            .WithTags("Auth Providers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageAuth));

        group.MapGet("/", GetList)
            .WithName("GetAuthProviders");

        group.MapPost("/oidc", CreateOidcProvider)
            .WithName("CreateOidcAuthProvider");

        group.MapDelete("/{authProviderExternalId}", Delete)
            .WithName("DeleteAuthProvider");

        group.MapPatch("/{authProviderExternalId}/name", UpdateName)
            .WithName("UpdateAuthProviderName");

        group.MapPut("/{authProviderExternalId}", Update)
            .WithName("UpdateAuthProvider");

        group.MapPost("/test-configuration", TestConfiguration)
            .WithName("TestAuthProviderConfiguration");

        group.MapPost("/{authProviderExternalId}/activate", Activate)
            .WithName("ActivateAuthProvider");

        group.MapPost("/{authProviderExternalId}/deactivate", Deactivate)
            .WithName("DeactivateAuthProvider");

        group.MapPut("/password-login", SetPasswordLogin)
            .WithName("SetPasswordLogin");
    }

    private static GetAuthSettingsResponseDto GetList(
        HttpContext httpContext,
        GetAuthProvidersQuery getAuthProvidersQuery,
        CheckUserHasSsoLoginQuery checkUserHasSsoLoginQuery,
        AppSettings appSettings)
    {
        var providers = getAuthProvidersQuery.Execute();
        var userId = httpContext.User.GetDatabaseId();

        return new GetAuthSettingsResponseDto
        {
            Items = providers
                .Select(p => new GetAuthProvidersItemDto
                {
                    ExternalId = p.ExternalId,
                    Name = p.Name,
                    Type = p.Type,
                    IsActive = p.IsActive,
                    ClientId = p.ClientId,
                    IssuerUrl = p.IssuerUrl
                })
                .ToArray(),

            IsPasswordLoginEnabled = appSettings
                .PasswordLogin
                .IsEnabled,

            CurrentUserHasSsoLinked = checkUserHasSsoLoginQuery.Execute(userId)
        };
    }

    private static async Task<Results<Ok<CreateOidcAuthProviderResponseDto>, BadRequest<HttpError>>> CreateOidcProvider(
        [FromBody] CreateOidcAuthProviderRequestDto request,
        CreateAuthProviderQuery createAuthProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await createAuthProviderQuery.Execute(
            name: request.Name,
            type: AuthProviderType.Oidc,
            clientId: request.ClientId,
            clientSecret: request.ClientSecret,
            issuerUrl: request.IssuerUrl,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateAuthProviderQuery.ResultCode.Ok =>
                TypedResults.Ok(new CreateOidcAuthProviderResponseDto
                {
                    ExternalId = result.ExternalId!.Value.Value
                }),

            CreateAuthProviderQuery.ResultCode.NameNotUnique =>
                HttpErrors.AuthProvider.NameNotUnique(request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateAuthProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Delete(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        DeleteAuthProviderQuery deleteAuthProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await deleteAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            DeleteAuthProviderQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            DeleteAuthProviderQuery.ResultCode.NotFound =>
                HttpErrors.AuthProvider.NotFound(authProviderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeleteAuthProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateName(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        [FromBody] UpdateAuthProviderNameRequestDto request,
        UpdateAuthProviderNameQuery updateAuthProviderNameQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateAuthProviderNameQuery.Execute(
            externalId: authProviderExternalId,
            name: request.Name,
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateAuthProviderNameQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            UpdateAuthProviderNameQuery.ResultCode.NotFound =>
                HttpErrors.AuthProvider.NotFound(authProviderExternalId),

            UpdateAuthProviderNameQuery.ResultCode.NameNotUnique =>
                HttpErrors.AuthProvider.NameNotUnique(request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateAuthProviderNameQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Activate(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        ActivateAuthProviderQuery activateAuthProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await activateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        return result switch
        {
            ActivateAuthProviderQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            ActivateAuthProviderQuery.ResultCode.NotFound =>
                HttpErrors.AuthProvider.NotFound(authProviderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(ActivateAuthProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Deactivate(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        DeactivateAuthProviderQuery deactivateAuthProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await deactivateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        return result switch
        {
            DeactivateAuthProviderQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            DeactivateAuthProviderQuery.ResultCode.NotFound =>
                HttpErrors.AuthProvider.NotFound(authProviderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeactivateAuthProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok<TestAuthProviderConfigurationResponseDto>, BadRequest<HttpError>>> TestConfiguration(
        [FromBody] TestAuthProviderConfigurationRequestDto request,
        TestAuthProviderConfigurationOperation testConfigurationOperation,
        CancellationToken cancellationToken)
    {
        var result = await testConfigurationOperation.Execute(
            issuerUrl: request.IssuerUrl,
            clientId: request.ClientId,
            clientSecret: request.ClientSecret,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            TestAuthProviderConfigurationOperation.ResultCode.Ok =>
                TypedResults.Ok(new TestAuthProviderConfigurationResponseDto
                {
                    Code = "ok",
                    Details = result.Details ?? "Configuration is valid."
                }),

            _ => TypedResults.Ok(new TestAuthProviderConfigurationResponseDto
            {
                Code = "failed",
                Details = result.Details ?? "Configuration test failed."
            })
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> Update(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        [FromBody] UpdateAuthProviderRequestDto request,
        UpdateAuthProviderQuery updateAuthProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            name: request.Name,
            clientId: request.ClientId,
            clientSecret: request.ClientSecret,
            issuerUrl: request.IssuerUrl,
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateAuthProviderQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            UpdateAuthProviderQuery.ResultCode.NotFound =>
                HttpErrors.AuthProvider.NotFound(authProviderExternalId),

            UpdateAuthProviderQuery.ResultCode.NameNotUnique =>
                HttpErrors.AuthProvider.NameNotUnique(request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateAuthProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static Results<Ok, BadRequest<HttpError>> SetPasswordLogin(
        [FromBody] SetPasswordLoginRequestDto request,
        HttpContext httpContext,
        AppSettings appSettings,
        CheckUserHasSsoLoginQuery checkUserHasSsoLoginQuery)
    {
        if (!request.IsEnabled)
        {
            var userId = httpContext.User.GetDatabaseId();

            if (!checkUserHasSsoLoginQuery.Execute(userId))
            {
                return HttpErrors.AuthProvider.UserHasNoSsoLogin();
            }
        }

        appSettings.SetPasswordLogin(request.IsEnabled);

        return TypedResults.Ok();
    }
}
