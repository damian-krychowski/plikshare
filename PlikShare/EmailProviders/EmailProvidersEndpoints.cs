using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Emails;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Activate;
using PlikShare.EmailProviders.Confirm;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.Create;
using PlikShare.EmailProviders.Deactivate;
using PlikShare.EmailProviders.Delete;
using PlikShare.EmailProviders.EmailSender;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.AwsSes;
using PlikShare.EmailProviders.ExternalProviders.AwsSes.Create.Contracts;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.ExternalProviders.Smtp;
using PlikShare.EmailProviders.ExternalProviders.Smtp.Create;
using PlikShare.EmailProviders.Id;
using PlikShare.EmailProviders.List;
using PlikShare.EmailProviders.List.Contracts;
using PlikShare.EmailProviders.ResendConfirmationEmail;
using PlikShare.EmailProviders.ResendConfirmationEmail.Contracts;
using PlikShare.EmailProviders.UpdateName;
using PlikShare.EmailProviders.UpdateName.Contracts;
using PlikShare.Users.Entities;
using PlikShare.Users.Middleware;

namespace PlikShare.EmailProviders;

public static class EmailProvidersEndpoints
{
    public static void MapEmailProvidersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/email-providers")
            .WithTags("Email Providers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageEmailProviders));

        // Basic operations
        group.MapGet("/", GetList)
            .WithName("GetEmailProviders");

        group.MapDelete("/{emailProviderExternalId}", DeleteEmailProvider)
            .WithName("DeleteEmailProvider");

        group.MapPatch("/{emailProviderExternalId}/name", UpdateName)
            .WithName("UpdateEmailProviderName");

        // Activation operations
        group.MapPost("/{emailProviderExternalId}/activate", Activate)
            .WithName("ActivateEmailProvider");

        group.MapPost("/{emailProviderExternalId}/deactivate", Deactivate)
            .WithName("DeactivateEmailProvider");

        // Email confirmation operations
        group.MapPost("/{emailProviderExternalId}/resend-confirmation-email", ResendConfirmationEmail)
            .WithName("ResendConfirmationEmail");

        group.MapPost("/{emailProviderExternalId}/confirm", ConfirmEmailProvider)
            .WithName("ConfirmEmailProvider");

        // Provider-specific creation endpoints
        group.MapPost("/aws-ses", CreateAwsSesEmailProvider)
            .WithName("CreateAwsSesEmailProvider");

        group.MapPost("/resend", CreateResendEmailProvider)
            .WithName("CreateResendEmailProvider");

        group.MapPost("/smtp", CreateSmtpEmailProvider)
            .WithName("CreateSmtpEmailProvider");
    }

    private static GetEmailProvidersResponseDto GetList(GetEmailProvidersQuery getEmailProvidersQuery)
    {
        var result = getEmailProvidersQuery.Execute();

        return new GetEmailProvidersResponseDto(
            Items: result
                .Select(ep => new GetEmailProvidersItemResponseDto(
                    ExternalId: ep.ExternalId,
                    Type: ep.Type,
                    Name: ep.Name,
                    EmailFrom: ep.EmailFrom,
                    IsConfirmed: ep.IsConfirmed,
                    IsActive: ep.IsActive))
                .ToArray());
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteEmailProvider(
        [FromRoute] EmailProviderExtId emailProviderExternalId,
        DeleteEmailProviderQuery deleteEmailProviderQuery,
        EmailProviderStore emailProviderStore,
        CancellationToken cancellationToken)
    {
        var result = await deleteEmailProviderQuery.Execute(
            emailProviderExternalId,
            cancellationToken);

        switch (result.Code)
        {
            case DeleteEmailProviderQuery.ResultCode.Ok:
                emailProviderStore.TryRemove(
                    result.EmailProviderId);

                return TypedResults.Ok();

            case DeleteEmailProviderQuery.ResultCode.NotFound:
                return HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeleteEmailProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateName(
        [FromRoute] EmailProviderExtId emailProviderExternalId,
        [FromBody] UpdateEmailProviderNameRequestDto request,
        UpdateEmailProviderNameQuery updateEmailProviderNameQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateEmailProviderNameQuery.Execute(
            emailProviderExternalId,
            request.Name,
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateEmailProviderNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateEmailProviderNameQuery.ResultCode.NotFound =>
                HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId),

            UpdateEmailProviderNameQuery.ResultCode.NameNotUnique =>
                HttpErrors.EmailProvider.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateEmailProviderNameQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> Activate(
       [FromRoute] EmailProviderExtId emailProviderExternalId,
       ActivateEmailProviderQuery activateEmailProviderQuery,
       EmailProviderStore emailProviderStore,
       EmailSenderFactory emailSenderFactory,
       IQueue queue,
       CancellationToken cancellationToken)
    {
        var result = await activateEmailProviderQuery.Execute(
            emailProviderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ActivateEmailProviderQuery.ResultCode.Ok:
                {
                    emailProviderStore.SetEmailSender(
                        emailProviderId: result.EmailProvider!.Id,
                        emailSender: emailSenderFactory.Build(
                            emailProviderType: result.EmailProvider.Type,
                            emailFrom: result.EmailProvider.EmailFrom,
                            detailsJson: result.EmailProvider.DetailsJson));

                    queue.UnlockBlockedQueueJobs();

                    return TypedResults.Ok();
                }

            case ActivateEmailProviderQuery.ResultCode.NotFound:
                return HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId);

            case ActivateEmailProviderQuery.ResultCode.ProviderNotConfirmed:
                return HttpErrors.EmailProvider.NotConfirmed();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ActivateEmailProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Deactivate(
       [FromRoute] EmailProviderExtId emailProviderExternalId,
       DeactivateEmailProviderQuery deactivateEmailProviderQuery,
       EmailProviderStore emailProviderStore,
       CancellationToken cancellationToken)
    {
        var result = await deactivateEmailProviderQuery.Execute(
            emailProviderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case DeactivateEmailProviderQuery.ResultCode.Ok:
                {
                    emailProviderStore.TryRemove(result.EmailProviderId);
                    return TypedResults.Ok();
                }

            case DeactivateEmailProviderQuery.ResultCode.NotFound:
                return HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeactivateEmailProviderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> ResendConfirmationEmail(
        [FromRoute] EmailProviderExtId emailProviderExternalId,
        [FromBody] ResendEmailProviderConfirmationEmailRequestDto request,
        ResendConfirmationEmailOperation resendConfirmationEmailOperation,
        HttpContext httpContext)
    {
        var result = await resendConfirmationEmailOperation.Execute(
            externalId: emailProviderExternalId,
            emailTo: request.EmailTo is not null
                ? new Email(request.EmailTo)
                : httpContext.GetUserContext().Email);

        return result switch
        {
            ResendConfirmationEmailOperation.ResultCode.Ok => 
                TypedResults.Ok(),

            ResendConfirmationEmailOperation.ResultCode.NotFound =>
                HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId),

            ResendConfirmationEmailOperation.ResultCode.AlreadyConfirmed =>
                HttpErrors.EmailProvider.AlreadyConfirmed(
                    emailProviderExternalId),

            ResendConfirmationEmailOperation.ResultCode.CouldNotSendTestEmail =>
                HttpErrors.EmailProvider.CouldNotSendTestEmail(),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateEmailProviderOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> ConfirmEmailProvider(
        [FromRoute] EmailProviderExtId emailProviderExternalId,
        [FromBody] ConfirmEmailProviderRequestDto request,
        ConfirmEmailProviderQuery confirmEmailProviderQuery,
        CancellationToken cancellationToken)
    {
        var result = await confirmEmailProviderQuery.Execute(
            externalId: emailProviderExternalId,
            confirmationCode: request.ConfirmationCode,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            ConfirmEmailProviderQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            ConfirmEmailProviderQuery.ResultCode.NotFound =>
                HttpErrors.EmailProvider.NotFound(
                    emailProviderExternalId),

            ConfirmEmailProviderQuery.ResultCode.AlreadyConfirmed =>
                HttpErrors.EmailProvider.AlreadyConfirmed(
                    emailProviderExternalId),

            ConfirmEmailProviderQuery.ResultCode.WrongConfirmationCode =>
                HttpErrors.EmailProvider.WrongConfirmationCode(
                    request.ConfirmationCode),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(ConfirmEmailProviderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async
        Task<Results<Ok<CreateAwsSesEmailProviderResponseDto>, BadRequest<HttpError>, BadRequest<HttpErrorWithDetails>>>
        CreateAwsSesEmailProvider(
            [FromBody] CreateAwsSesEmailProviderRequestDto request,
            HttpContext httpContext,
            CreateEmailProviderOperation createEmailProviderOperation,
            CancellationToken cancellationToken)
    {
        var result = await createEmailProviderOperation.Execute(
            name: request.Name,
            type: EmailProviderType.AwsSes,
            emailFrom: request.EmailFrom,
            detailsJson: Json.Serialize(
                item: new AwsSesDetailsEntity(
                    AccessKey: request.AccessKey,
                    SecretAccessKey: request.SecretAccessKey,
                    Region: request.Region)),
            user: httpContext.GetUserContext(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateEmailProviderOperation.ResultCode.Ok => 
                TypedResults.Ok(new CreateAwsSesEmailProviderResponseDto(
                    ExternalId: result.EmailProviderExternalId!.Value)),

            CreateEmailProviderOperation.ResultCode.CouldNotSendTestEmail =>
                HttpErrors.EmailProvider.CouldNotSendTestEmailWithDetails(
                    result.InnerError!, 
                    "AWS SES"),

            CreateEmailProviderOperation.ResultCode.NameNotUnique =>
                HttpErrors.EmailProvider.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateEmailProviderOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async
        Task<Results<Ok<CreateAwsSesEmailProviderResponseDto>, BadRequest<HttpError>, BadRequest<HttpErrorWithDetails>>>
        CreateResendEmailProvider(
            [FromBody] CreateResendEmailProviderRequestDto request,
            HttpContext httpContext,
            CreateEmailProviderOperation createEmailProviderOperation,
            CancellationToken cancellationToken)
    {
        var result = await createEmailProviderOperation.Execute(
            name: request.Name,
            type: EmailProviderType.Resend,
            emailFrom: request.EmailFrom,
            detailsJson: Json.Serialize(
                item: new ResendDetailsEntity(
                    ApiKey: request.ApiKey)),
            user: httpContext.GetUserContext(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateEmailProviderOperation.ResultCode.Ok => 
                TypedResults.Ok(new CreateAwsSesEmailProviderResponseDto(
                    ExternalId: result.EmailProviderExternalId!.Value)),

            CreateEmailProviderOperation.ResultCode.CouldNotSendTestEmail =>
                HttpErrors.EmailProvider.CouldNotSendTestEmailWithDetails(
                    result.InnerError!, 
                    "Resend"),

            CreateEmailProviderOperation.ResultCode.NameNotUnique =>
                HttpErrors.EmailProvider.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateEmailProviderOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async
        Task<Results<Ok<CreateAwsSesEmailProviderResponseDto>, BadRequest<HttpError>, BadRequest<HttpErrorWithDetails>>>
        CreateSmtpEmailProvider(
            [FromBody] CreateSmtpEmailProviderRequestDto request,
            HttpContext httpContext,
            CreateEmailProviderOperation createEmailProviderOperation,
            CancellationToken cancellationToken)
    {
        var result = await createEmailProviderOperation.Execute(
            name: request.Name,
            type: EmailProviderType.Smtp,
            emailFrom: request.EmailFrom,
            detailsJson: Json.Serialize(
                item: new SmtpDetailsEntity(
                    Hostname: request.Hostname,
                    Port: request.Port,
                    SslMode: request.SslMode,
                    Username: request.Username,
                    Password: request.Password)),
            user: httpContext.GetUserContext(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateEmailProviderOperation.ResultCode.Ok => 
                TypedResults.Ok(new CreateAwsSesEmailProviderResponseDto(
                    ExternalId: result.EmailProviderExternalId!.Value)),

            CreateEmailProviderOperation.ResultCode.CouldNotSendTestEmail =>
                HttpErrors.EmailProvider.CouldNotSendTestEmailWithDetails(
                    result.InnerError!, 
                    "SMTP"),

            CreateEmailProviderOperation.ResultCode.NameNotUnique =>
                HttpErrors.EmailProvider.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateEmailProviderOperation),
                resultValueStr: result.ToString())
        };
    }
}