using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;

namespace PlikShare.Users.UserEncryptionPassword;

public static class UserEncryptionPasswordEndpoints
{
    public static void MapUserEncryptionPasswordEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/user-encryption-password")
            .WithTags("User Encryption Password")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapPost("/setup", Setup).WithName("SetupUserEncryptionPassword");
        group.MapPost("/unlock", Unlock).WithName("UnlockUserEncryptionPassword");
        group.MapPost("/change", Change).WithName("ChangeUserEncryptionPassword");
        group.MapPost("/reset", Reset).WithName("ResetUserEncryptionPassword");
    }

    private static async Task<Results<Ok<SetupResponseDto>, BadRequest<HttpError>, Conflict<HttpError>>> Setup(
        [FromBody] SetupRequestDto request,
        HttpContext httpContext,
        SetupUserEncryptionPasswordOperation operation,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var result = await operation.Execute(
            user: user,
            encryptionPassword: request.EncryptionPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case SetupUserEncryptionPasswordOperation.ResultCode.AlreadyConfigured:
                return TypedResults.Conflict(new HttpError
                {
                    Code = "user-encryption-already-configured",
                    Message = "Encryption password is already set up. Use change or reset instead."
                });

            case SetupUserEncryptionPasswordOperation.ResultCode.UserNotFound:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-not-found",
                    Message = "The authenticated user was not found."
                });

            case SetupUserEncryptionPasswordOperation.ResultCode.Ok:
                UserEncryptionSessionCookie.Set(httpContext, user.ExternalId, result.PrivateKey!);
                return TypedResults.Ok(new SetupResponseDto(RecoveryCode: result.RecoveryCode!));

            default:
                throw new InvalidOperationException($"Unexpected result code: {result.Code}");
        }
    }

    private static Results<Ok, BadRequest<HttpError>> Unlock(
        [FromBody] UnlockRequestDto request,
        HttpContext httpContext,
        UnlockUserEncryptionPasswordOperation operation)
    {
        var user = httpContext.GetUserContext();

        var result = operation.Execute(user, request.EncryptionPassword);

        switch (result.Code)
        {
            case UnlockUserEncryptionPasswordOperation.ResultCode.NotConfigured:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-encryption-not-configured",
                    Message = "Encryption password has not been set up yet."
                });

            case UnlockUserEncryptionPasswordOperation.ResultCode.InvalidPassword:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "invalid-encryption-password",
                    Message = "The encryption password is incorrect."
                });

            case UnlockUserEncryptionPasswordOperation.ResultCode.Ok:
                UserEncryptionSessionCookie.Set(httpContext, user.ExternalId, result.PrivateKey!);
                return TypedResults.Ok();

            default:
                throw new InvalidOperationException($"Unexpected result code: {result.Code}");
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> Change(
        [FromBody] ChangeRequestDto request,
        HttpContext httpContext,
        ChangeUserEncryptionPasswordOperation operation,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var result = await operation.Execute(
            user: user,
            oldPassword: request.OldPassword,
            newPassword: request.NewPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ChangeUserEncryptionPasswordOperation.ResultCode.NotConfigured:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-encryption-not-configured",
                    Message = "Encryption password has not been set up yet."
                });

            case ChangeUserEncryptionPasswordOperation.ResultCode.InvalidOldPassword:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "invalid-old-encryption-password",
                    Message = "The provided old encryption password is incorrect."
                });

            case ChangeUserEncryptionPasswordOperation.ResultCode.UserNotFound:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-not-found",
                    Message = "The authenticated user was not found."
                });

            case ChangeUserEncryptionPasswordOperation.ResultCode.Ok:
                UserEncryptionSessionCookie.Set(httpContext, user.ExternalId, result.PrivateKey!);
                return TypedResults.Ok();

            default:
                throw new InvalidOperationException($"Unexpected result code: {result.Code}");
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> Reset(
        [FromBody] ResetRequestDto request,
        HttpContext httpContext,
        ResetUserEncryptionPasswordOperation operation,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var result = await operation.Execute(
            user: user,
            recoveryCode: request.RecoveryCode,
            newPassword: request.NewPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ResetUserEncryptionPasswordOperation.ResultCode.NotConfigured:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-encryption-not-configured",
                    Message = "Encryption password has not been set up yet."
                });

            case ResetUserEncryptionPasswordOperation.ResultCode.InvalidRecoveryCode:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "invalid-recovery-code",
                    Message = "The recovery code is invalid or does not match this user."
                });

            case ResetUserEncryptionPasswordOperation.ResultCode.UserNotFound:
                return TypedResults.BadRequest(new HttpError
                {
                    Code = "user-not-found",
                    Message = "The authenticated user was not found."
                });

            case ResetUserEncryptionPasswordOperation.ResultCode.Ok:
                UserEncryptionSessionCookie.Set(httpContext, user.ExternalId, result.PrivateKey!);
                return TypedResults.Ok();

            default:
                throw new InvalidOperationException($"Unexpected result code: {result.Code}");
        }
    }
}

public record SetupRequestDto(string EncryptionPassword);
public record SetupResponseDto(string RecoveryCode);

public record UnlockRequestDto(string EncryptionPassword);

public record ChangeRequestDto(string OldPassword, string NewPassword);

public record ResetRequestDto(string RecoveryCode, string NewPassword);
