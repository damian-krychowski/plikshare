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
using PlikShare.AuditLog;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;
using Audit = PlikShare.AuditLog.Details.Audit;

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

        group.MapPut("/password-login-enabled", SetPasswordLogin)
            .WithName("SetPasswordLogin");
    }

    private static GetAuthSettingsResponseDto GetList(
        HttpContext httpContext,
        GetAuthProvidersQuery getAuthProvidersQuery,
        CheckUserHasSsoLoginQuery checkUserHasSsoLoginQuery,
        AppSettings appSettings)
    {
        var providers = getAuthProvidersQuery.Execute();
        var userId = httpContext.User.GetExternalId();

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

            CurrentUserHasSsoLinked = checkUserHasSsoLoginQuery.Execute(
                userId)
        };
    }

    private static async Task<Results<Ok<CreateOidcAuthProviderResponseDto>, BadRequest<HttpError>>> CreateOidcProvider(
        [FromBody] CreateOidcAuthProviderRequestDto request,
        CreateAuthProviderQuery createAuthProviderQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createAuthProviderQuery.Execute(
            name: request.Name,
            type: AuthProviderType.Oidc,
            clientId: request.ClientId,
            clientSecret: request.ClientSecret,
            issuerUrl: request.IssuerUrl,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateAuthProviderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = result.ExternalId!.Value,
                            Name = request.Name,
                            Type = AuthProviderType.Oidc.Value
                        }),
                    cancellationToken);

                return TypedResults.Ok(new CreateOidcAuthProviderResponseDto
                {
                    ExternalId = result.ExternalId!.Value
                });

            case CreateAuthProviderQuery.ResultCode.NameNotUnique:
                return HttpErrors.AuthProvider.NameNotUnique(request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateAuthProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Delete(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        DeleteAuthProviderQuery deleteAuthProviderQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await deleteAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case DeleteAuthProviderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = authProviderExternalId,
                            Name = result.Name!,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case DeleteAuthProviderQuery.ResultCode.NotFound:
                return HttpErrors.AuthProvider.NotFound(authProviderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeleteAuthProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateName(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        [FromBody] UpdateAuthProviderNameRequestDto request,
        UpdateAuthProviderNameQuery updateAuthProviderNameQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAuthProviderNameQuery.Execute(
            externalId: authProviderExternalId,
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateAuthProviderNameQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = authProviderExternalId,
                            Name = request.Name,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateAuthProviderNameQuery.ResultCode.NotFound:
                return HttpErrors.AuthProvider.NotFound(authProviderExternalId);

            case UpdateAuthProviderNameQuery.ResultCode.NameNotUnique:
                return HttpErrors.AuthProvider.NameNotUnique(request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateAuthProviderNameQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Activate(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        ActivateAuthProviderQuery activateAuthProviderQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await activateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ActivateAuthProviderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.ActivatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = authProviderExternalId,
                            Name = result.Name!,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case ActivateAuthProviderQuery.ResultCode.NotFound:
                return HttpErrors.AuthProvider.NotFound(authProviderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ActivateAuthProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Deactivate(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        DeactivateAuthProviderQuery deactivateAuthProviderQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await deactivateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case DeactivateAuthProviderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.DeactivatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = authProviderExternalId,
                            Name = result.Name!,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case DeactivateAuthProviderQuery.ResultCode.NotFound:
                return HttpErrors.AuthProvider.NotFound(authProviderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeactivateAuthProviderQuery),
                    resultValueStr: result.ToString());
        }
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
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAuthProviderQuery.Execute(
            externalId: authProviderExternalId,
            name: request.Name,
            clientId: request.ClientId,
            clientSecret: request.ClientSecret,
            issuerUrl: request.IssuerUrl,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateAuthProviderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.AuthProvider.UpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        authProvider: new Audit.AuthProviderRef
                        {
                            ExternalId = authProviderExternalId,
                            Name = request.Name,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateAuthProviderQuery.ResultCode.NotFound:
                return HttpErrors.AuthProvider.NotFound(authProviderExternalId);

            case UpdateAuthProviderQuery.ResultCode.NameNotUnique:
                return HttpErrors.AuthProvider.NameNotUnique(request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateAuthProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetPasswordLogin(
        [FromBody] SetPasswordLoginRequestDto request,
        HttpContext httpContext,
        AppSettings appSettings,
        CheckUserHasSsoLoginQuery checkUserHasSsoLoginQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (!request.IsEnabled)
        {
            var userId = httpContext.User.GetExternalId();

            if (!checkUserHasSsoLoginQuery.Execute(userId))
            {
                return HttpErrors.AuthProvider.UserHasNoSsoLogin();
            }
        }

        appSettings.SetPasswordLogin(request.IsEnabled);

        await auditLogService.Log(
            Audit.AuthProvider.PasswordLoginToggledEntry(
                actor: httpContext.GetAuditLogActorContext(),
                isEnabled: request.IsEnabled),
            cancellationToken);

        return TypedResults.Ok();
    }
}
