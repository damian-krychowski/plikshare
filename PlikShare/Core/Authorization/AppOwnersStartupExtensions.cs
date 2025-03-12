using Microsoft.AspNetCore.Identity;
using PlikShare.Core.IdentityProvider;
using PlikShare.Users.Entities;
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
            CreateUserIfDoesntExist( 
                owner: owner,
                password: appOwners.InitialPassword,
                userStore: userStore, 
                userManager: userManager).Wait();
        }

        Log.Information("[INITIALIZATION] App Owners initialization finished: {AppOwners}.",
            appOwners.Owners().Select(owner => EmailAnonymization.Anonymize(owner.Value)));
    }

    private static async Task CreateUserIfDoesntExist( 
        Email owner, 
        string password,
        IUserStore<ApplicationUser> userStore,
        UserManager<ApplicationUser> userManager)
    {
        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        
        var user = await userStore.FindByNameAsync(
            normalizedUserName: owner.Normalized,
            cancellationToken: CancellationToken.None);
            
        if(user is not null)
            return;

        user = new ApplicationUser();
            
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
    }
}