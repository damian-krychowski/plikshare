using Microsoft.AspNetCore.Identity;
using PlikShare.Core.IdentityProvider;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using Serilog;

namespace PlikShare.Core.Authorization;

public static class AppOwnersStartupExtensions
{
    public static void InitializeAppOwners(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var appOwners = app
            .Services
            .GetRequiredService<AppOwners>();

        using var scope = app.Services.CreateScope();
        
        var userStore = scope
            .ServiceProvider
            .GetRequiredService<IUserStore<ApplicationUser>>();
        
        var userManager = scope
            .ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        
        foreach (var owner in appOwners.Owners())
        {
            var externalId = CreateUserIfDoesntExist( 
                owner: owner.Email,
                password: appOwners.InitialPassword,
                userStore: userStore, 
                userManager: userManager).Result;

            // AppOwners singleton is registered during SetupAuth (builder phase) based purely on
            // configuration (emails + initial password), before the database is available and
            // before Identity services can be resolved. The UserExtId for each owner only comes
            // into existence here, during app initialization, when we actually create/lookup the
            // underlying ApplicationUser. We back-fill it into the already-registered AppOwners
            // instance instead of constructing a second, "complete" AppOwners later — this way
            // consumers across the app always inject the same singleton and, once startup
            // finishes, get a fully populated object with ExternalId available.
            owner.SetExternalId(externalId);
        }

        Log.Information("[INITIALIZATION] App Owners initialization finished: {AppOwners}.",
            appOwners.Owners().Select(owner => owner.Email.Anonymize()));
    }

    private static async Task<UserExtId> CreateUserIfDoesntExist( 
        Email owner, 
        string password,
        IUserStore<ApplicationUser> userStore,
        UserManager<ApplicationUser> userManager)
    {
        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        
        var user = await userStore.FindByNameAsync(
            normalizedUserName: owner.Normalized,
            cancellationToken: CancellationToken.None);
            
        if (user is not null)
            return UserExtId.Parse(user.Id);

        user = new ApplicationUser
        {
            IsAppOwner = true
        };
            
        await userStore.SetUserNameAsync(
            user: user, 
            userName: owner.Value, 
            cancellationToken: CancellationToken.None);
            
        await emailStore.SetEmailAsync(
            user: user, 
            email: owner.Value,
            cancellationToken: CancellationToken.None);
            
        var result = await userManager.CreateAsync(
            user: user, 
            password: password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create initial user for AppOwner '{owner.Value}'. " +
                $"Reasons: {string.Join(separator: ", ", values: result.Errors.Select(selector: e => $"{e.Code}: {e.Description}"))}");
        }
            
        await emailStore.SetEmailConfirmedAsync(
            user: user,
            confirmed: true,
            cancellationToken: CancellationToken.None);

        var emailUpdateResult = await emailStore.UpdateAsync(
            user: user,
            cancellationToken: CancellationToken.None);

        if (!emailUpdateResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create confirm email for AppOwner '{owner.Value}'. " +
                $"Reasons: {string.Join(separator: ", ", values: result.Errors.Select(selector: e => $"{e.Code}: {e.Description}"))}");
        }

        Log.Information("AppOwner '{AppOwnerEmail}' user was created with initial password.",
            owner.Value);

        return UserExtId.Parse(user.Id);
    }
}