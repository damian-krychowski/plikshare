using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Account.Contracts;
using PlikShare.Account.GetKnownUsers;
using PlikShare.Account.GetKnownUsers.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Core.IdentityProvider;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using Serilog;

namespace PlikShare.Account;

public static class AccountEndpoints
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapPost("/sign-out", SignAccountOut)
            .AllowAnonymous()  // to improve UI experience when UI would call this method not having the cookie
            .WithName("SignAccountOut");

        group.MapGet("/details", GetAccountDetails)
            .WithName("GetAccountDetails");

        group.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword");

        // 2FA endpoints
        group.MapGet("/2fa/status", Get2FaStatus)
            .WithName("Get2FaStatus");

        group.MapPost("/2fa/enable", Enable2Fa)
            .WithName("Enable2Fa");

        group.MapPost("/2fa/disable", Disable2Fa)
            .WithName("Disable2Fa");

        group.MapPost("/2fa/generate-recovery-codes", GenerateRecoveryCodes)
            .WithName("GenerateRecoveryCodes");

        group.MapGet("/known-users", GetKnownUsers)
            .WithName("GetKnownUsers");
    }

    private static GetKnownUsersResponseDto GetKnownUsers(
        HttpContext httpContext,
        GetKnownUsersQuery getKnownUsersQuery)
    {
        var user = httpContext.GetUserContext();

        var result = getKnownUsersQuery.Execute(
            user: user);

        return new GetKnownUsersResponseDto(
            Items: result
                .Select(u => new KnownUserDto(
                    ExternalId: u.ExternalId,
                    Email: u.Email))
                .ToArray());
    }

    private static async Task SignAccountOut(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
    }

    private static GetAccountDetailsResponseDto GetAccountDetails(HttpContext httpContext)
    {
        var user = httpContext.GetUserContext();

        return new GetAccountDetailsResponseDto(
            ExternalId: httpContext.User.GetExternalId(),
            Email: httpContext.User.GetEmail(),
            Roles: new GetAccountRolesResponseDto(
                IsAppOwner: user.Roles.IsAppOwner,
                IsAdmin: user.Roles.IsAdmin),
            Permissions: new GetAccountPermissionsResponseDto(
                CanAddWorkspace: user.Roles.IsAppOwner || user.Roles.IsAdmin || user.Permissions.CanAddWorkspace,
                CanManageGeneralSettings: user.Roles.IsAppOwner || (user.Roles.IsAdmin && user.Permissions.CanManageGeneralSettings),
                CanManageUsers: user.Roles.IsAppOwner || (user.Roles.IsAdmin && user.Permissions.CanManageUsers),
                CanManageStorages: user.Roles.IsAppOwner || (user.Roles.IsAdmin && user.Permissions.CanManageStorages),
                CanManageEmailProviders: user.Roles.IsAppOwner || (user.Roles.IsAdmin && user.Permissions.CanManageEmailProviders)),
            MaxWorkspaceNumber: user.MaxWorkspaceNumber);
    }

    private static async Task<ChangePasswordResponseDto> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User is null even though request was authenticated");
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(
            user: user,
            currentPassword: request.CurrentPassword,
            newPassword: request.NewPassword);

        if (!changePasswordResult.Succeeded)
        {
            Log.Warning("Something went wrong while changing password for User '{UserExternalId}'. Problem: {@Problems}",
                user.Id,
                changePasswordResult);

            if (changePasswordResult.Errors.Any(err => err.Code == "PasswordMismatch"))
            {
                return ChangePasswordResponseDto.PasswordMismatch;
            }

            return ChangePasswordResponseDto.Failed;
        }

        await signInManager.SignInAsync(
            user: user,
            isPersistent: httpContext.User.GetRememberMeOrDefault());

        return ChangePasswordResponseDto.Success;
    }

    private static async Task<Get2FaStatusResponseDto> Get2FaStatus(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User is null even though request was authenticated");
        }

        var is2FaEnabled = await userManager.GetTwoFactorEnabledAsync(user);

        if (is2FaEnabled)
        {
            var recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);

            return new Get2FaStatusResponseDto(
                IsEnabled: true,
                RecoveryCodesLeft: recoveryCodesLeft,
                QrCodeUri: null);
        }

        var qrCodeUri = await Get2FAQRCodeUri(user, userManager, signInManager, httpContext);

        return new Get2FaStatusResponseDto(
            IsEnabled: false,
            RecoveryCodesLeft: null,
            QrCodeUri: qrCodeUri);
    }

    private static async Task<Enable2FaResponseDto> Enable2Fa(
        [FromBody] Enable2FaRequestDto request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User is null even though request was authenticated");
        }

        var is2FaTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user: user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            token: request.VerificationCode);

        if (!is2FaTokenValid)
        {
            Log.Warning("Wrong verification code were provided while enabling 2FA for User '{UserExternalId}'.",
                user.Id);

            return Enable2FaResponseDto.InvalidVerificationCode;
        }

        var result = await userManager.SetTwoFactorEnabledAsync(
            user: user,
            enabled: true);

        if (!result.Succeeded)
        {
            Log.Warning("Something went wrong while enabling 2FA for User '{UserExternalId}'. Problem: {@Problems}",
                user.Id,
                result.Errors);

            return Enable2FaResponseDto.Failed;
        }

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(
            user: user,
            number: 5);

        await signInManager.SignInAsync(
            user: user,
            isPersistent: httpContext.User.GetRememberMeOrDefault());

        return Enable2FaResponseDto.Enabled(recoveryCodes ?? []);
    }

    private static async Task<Disable2FaResponseDto> Disable2Fa(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User is null even though request was authenticated");
        }

        var result = await userManager.SetTwoFactorEnabledAsync(
            user: user,
            enabled: false);

        if (!result.Succeeded)
        {
            Log.Warning("Something went wrong while disabling 2FA for User '{UserExternalId}'. Problem: {@Problems}",
                user.Id,
                result.Errors);

            return Disable2FaResponseDto.Failed;
        }

        await signInManager.SignInAsync(
            user: user,
            isPersistent: httpContext.User.GetRememberMeOrDefault());

        return Disable2FaResponseDto.Disabled;
    }

    private static async Task<GenerateRecoveryCodesResponseDto> GenerateRecoveryCodes(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User is null even though request was authenticated");
        }

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(
            user: user,
            number: 5);

        return new GenerateRecoveryCodesResponseDto(
            RecoveryCodes: recoveryCodes?.AsList() ?? []);
    }

    private static async Task<string> Get2FAQRCodeUri(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        HttpContext httpContext)
    {
        await userManager.ResetAuthenticatorKeyAsync(user);

        await signInManager.SignInAsync(
            user: user,
            isPersistent: httpContext.User.GetRememberMeOrDefault());

        var token = await userManager.GetAuthenticatorKeyAsync(user);

        return string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            "PlikShare",
            user.Email,
            token);
    }
}