using Flurl;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Auth.CheckInvitation;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.IdentityProvider;
using PlikShare.Core.Queue;
using PlikShare.GeneralSettings;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        // Sign up and email confirmation
        group.MapPost("/sign-up", SignUp)
            .WithName("SignUp");

        group.MapPost("/resend-confirmation-link", ResendConfirmationLink)
            .WithName("ResendConfirmationLink");

        group.MapPost("/confirm-email", ConfirmEmail)
            .WithName("ConfirmEmail");

        // Sign in
        group.MapPost("/sign-in", SignIn)
            .WithName("SignIn");

        group.MapPost("/sign-in-2fa", SignIn2Fa)
            .WithName("SignIn2Fa");

        group.MapPost("/sign-in-recovery-code", SignInRecoveryCode)
            .WithName("SignInRecoveryCode");

        // Password management
        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword");

        group.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword");
    }

    private static async Task<SignUpUserResponseDto> SignUp(
        [FromBody] SignUpUserRequestDto request,
        HttpContext httpContext,
        AppSettings appSettings,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserStore<ApplicationUser> userStore,
        IQueue queue,
        IClock clock,
        DbWriteQueue dbWriteQueue,
        CheckUserInvitationCodeQuery checkUserInvitationCodeQuery,
        CancellationToken cancellationToken)
    {
        var areAllRequiredCheckboxesPresent = appSettings
            .RequiredSignUpCheckboxesIds
            .All(request.SelectedCheckboxIds.Contains);

        if(!areAllRequiredCheckboxesPresent)
            return SignUpUserResponseDto.SignUpCheckboxesMissing;

        if (appSettings.ApplicationSignUp == AppSettings.SignUpSetting.OnlyInvitedUsers)
        {
            if (string.IsNullOrWhiteSpace(request.InvitationCode))
                return SignUpUserResponseDto.InvitationRequired;

            var invitationCheckResult = checkUserInvitationCodeQuery.Execute(
                email: request.Email,
                invitationCode: request.InvitationCode);

            if (invitationCheckResult == CheckUserInvitationCodeQuery.ResultCode.WrongInvitationCode)
                return SignUpUserResponseDto.InvitationRequired;
        }

        var user = new ApplicationUser
        {
            SelectedCheckboxIds = request.SelectedCheckboxIds
        };

        await userStore.SetUserNameAsync(
            user,
            request.Email,
            cancellationToken);

        var emailStore = GetEmailStore(userManager, userStore);

        await emailStore.SetEmailAsync(
            user,
            request.Email,
            cancellationToken);

        var result = await userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(err => err.Code == "DuplicateUserName"))
            {
                Log.Warning("Attempt to register a user '{UserEmail}' which already exists.",
                    EmailAnonymization.Anonymize(request.Email));

                return SignUpUserResponseDto.ConfirmationEmailSent;
            }

            throw new InvalidOperationException("Some errors on register user");
        }

        if (appSettings.ApplicationSignUp == AppSettings.SignUpSetting.OnlyInvitedUsers)
        {
            await emailStore.SetEmailConfirmedAsync(
                user: user,
                confirmed: true,
                cancellationToken: cancellationToken);

            var emailUpdateResult = await emailStore.UpdateAsync(
                 user: user,
                 cancellationToken: cancellationToken);

            if (!emailUpdateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Something went wrong while confirming email of user '{EmailAnonymization.Anonymize(request.Email)}' after he created account with invitationCode: '{request.InvitationCode}'. " +
                    $"Errors: {string.Join(", ", emailUpdateResult.Errors.Select(e => $"{e.Code}:{e.Description}"))}");
            }

            await signInManager.SignInAsync(
                user: user,
                isPersistent: false);

            return SignUpUserResponseDto.SingedUpAndSignedIn;
        }
        else
        {
            await SendConfirmationLinkEmail(
                user: user, 
                userManager: userManager, 
                queue: queue, 
                clock: clock, 
                dbWriteQueue: dbWriteQueue, 
                httpContext: httpContext, 
                cancellationToken: cancellationToken);

            return SignUpUserResponseDto.ConfirmationEmailSent;
        }
    }

    private static async Task<ResendConfirmationLinkResponseDto> ResendConfirmationLink(
        [FromBody] ResendConfirmationLinkRequestDto request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IQueue queue,
        IClock clock,
        DbWriteQueue dbWriteQueue,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            Log.Warning(
                "Attempt to resend confirmation link for user '{UserEmail}' which doesn't exist.",
                request.Email);

            return ResendConfirmationLinkResponseDto.ConfirmationEmailSent;
        }

        if (user.EmailConfirmed)
        {
            Log.Warning(
                "Attempt to resend confirmation link for user '{UserExternalId}' whose email was already confirmed.",
                user.Id);

            return ResendConfirmationLinkResponseDto.ConfirmationEmailSent;
        }

        await SendConfirmationLinkEmail(
            user: user,
            userManager: userManager,
            queue: queue,
            clock: clock,
            dbWriteQueue: dbWriteQueue,
            httpContext: httpContext,
            cancellationToken: cancellationToken);

        return ResendConfirmationLinkResponseDto.ConfirmationEmailSent;
    }

    private static async Task<ConfirmEmailResponseDto> ConfirmEmail(
        [FromBody] ConfirmEmailRequestDto request,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserExternalId);

        if (user is null)
        {
            Log.Warning("Attempt to confirm user '{UserExternalId}' which does not exist. " +
                        "Confirmation code: '{Code}'",
                request.UserExternalId,
                request.Code);

            return ConfirmEmailResponseDto.InvalidToken;
        }

        var result = await userManager.ConfirmEmailAsync(
            user: user,
            token: request.Code);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(err => err.Code == "InvalidToken"))
            {
                return ConfirmEmailResponseDto.InvalidToken;
            }

            Log.Error("Something went wrong while confirming user '{UserExternalId}' email with " +
                      "code '{Code}'. Confirmation result {@Result}",
                request.UserExternalId,
                request.Code,
                result);

            throw new InvalidOperationException("Some errors on confirm user email");
        }

        return ConfirmEmailResponseDto.EmailConfirmed;
    }

    private static async Task<SignInUserResponseDto> SignIn(
        [FromBody] SignInUserRequestDto request,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var result = await signInManager.PasswordSignInAsync(
            userName: request.Email,
            password: request.Password,
            isPersistent: request.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
            return SignInUserResponseDto.Successful;

        if (result.RequiresTwoFactor)
            return SignInUserResponseDto.Required2Fa;

        if (result.IsLockedOut)
        {
            //todo handle lockout
        }

        return SignInUserResponseDto.Failed;
    }

    private static async Task<SignInUser2FaResponseDto> SignIn2Fa(
        [FromBody] SignInUser2FaRequestDto request,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user is null)
        {
            return SignInUser2FaResponseDto.Failed;
        }

        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            code: request.VerificationCode,
            isPersistent: request.RememberMe,
            rememberClient: request.RememberDevice);

        if (result.Succeeded)
            return SignInUser2FaResponseDto.Successful;

        if (result.IsLockedOut)
        {
            //todo handle lockout
        }

        return SignInUser2FaResponseDto.InvalidVerificationCode;
    }

    private static async Task<SignInUserRecoveryCodeResponseDto> SignInRecoveryCode(
        [FromBody] SignInUserRecoveryCodeRequestDto request,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user is null)
        {
            return SignInUserRecoveryCodeResponseDto.Failed;
        }

        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(
            recoveryCode: request.RecoveryCode);

        if (result.Succeeded)
            return SignInUserRecoveryCodeResponseDto.Successful;

        if (result.IsLockedOut)
        {
            //todo handle lockout
        }

        return SignInUserRecoveryCodeResponseDto.InvalidRecoveryCode;
    }

    private static async Task ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfig config,
        IClock clock,
        IQueue queue,
        DbWriteQueue dbWriteQueue,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.UserManager.FindByEmailAsync(request.Email);

        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = new Url(config.AppUrl)
            .AppendPathSegment("reset-password")
            .AppendQueryParam("userId", user.Id)
            .AppendQueryParam("code", code)
            .ToString();

        await dbWriteQueue.Execute(
            operationToEnqueue: context => queue.Enqueue(
                correlationId: httpContext.GetCorrelationId(),
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<ResetPasswordEmailDefinition>
                {
                    Email = user.Email!,
                    Definition = new ResetPasswordEmailDefinition(Link: link),
                    Template = EmailTemplate.ResetPassword
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: context,
                transaction: null),
            cancellationToken: cancellationToken);
    }

    private static async Task<ResetPasswordResponseDto> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserExternalId);

        if (user is null)
        {
            Log.Warning("Attempt to reset password for user '{UserExternalId}' which does not exist. " +
                       "Confirmation code: '{Code}'",
                request.UserExternalId,
                request.Code);

            return ResetPasswordResponseDto.InvalidToken;
        }

        var result = await userManager.ResetPasswordAsync(
            user: user,
            token: request.Code,
            newPassword: request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(err => err.Code == "InvalidToken"))
            {
                return ResetPasswordResponseDto.InvalidToken;
            }

            Log.Error("Something went wrong while resetting password of user '{UserExternalId}' with " +
                     "code '{Code}'. Confirmation result {@Result}",
                request.UserExternalId,
                request.Code,
                result);

            throw new InvalidOperationException("Some errors on reset user password");
        }

        return ResetPasswordResponseDto.PasswordReset;
    }

    private static async Task SendConfirmationLinkEmail(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IQueue queue,
        IClock clock,
        DbWriteQueue dbWriteQueue,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = new Url(httpContext.RequestServices.GetRequiredService<IConfig>().AppUrl)
            .AppendPathSegment("email-confirmation")
            .AppendQueryParam("userId", user.Id)
            .AppendQueryParam("code", code)
            .ToString();
        
        await dbWriteQueue.Execute(
            operationToEnqueue: context => queue.Enqueue(
                correlationId: httpContext.GetCorrelationId(),
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<ConfirmationEmailDefinition>
                {
                    Email = user.Email!,
                    Definition = new ConfirmationEmailDefinition(Link: link),
                    Template = EmailTemplate.ConfirmationEmail
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: context,
                transaction: null),
            cancellationToken: cancellationToken);
    }

    private static IUserEmailStore<ApplicationUser> GetEmailStore(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore)
    {
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<ApplicationUser>)userStore;
    }
}