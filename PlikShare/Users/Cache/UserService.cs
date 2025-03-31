using PlikShare.Core.Authorization;
using PlikShare.Core.Emails.Alerts;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;

namespace PlikShare.Users.Cache;

public class UserService(
    AppOwners appOwners,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery,
    AlertsService alertsService)
{
    public async Task<UserContext> GetOrCreateUserInvitation(
        Email email,
        CancellationToken cancellationToken)
    {
        var user = await getOrCreateUserInvitationQuery.Execute(
            email: email,
            cancellationToken: cancellationToken);

        if (user.WasJustCreated)
        { 
            alertsService.SendEmailAlert(
                title: "New user was created",
                content: $"User with email '{email.Anonymize()}' was just created",
                correlationId: Guid.NewGuid());
        }

        return new UserContext(
            Status: user.IsInvitation
                ? UserStatus.Invitation
                : UserStatus.Registered,
            Id: user.Id,
            ExternalId: user.ExternalId,
            Email: email,
            IsEmailConfirmed: user.IsEmailConfirmed,
            Stamps: new UserSecurityStamps(
                Security: user.SecurityStamp,
                Concurrency: user.ConcurrencyStamp),
            Roles: new UserRoles(
                IsAppOwner: appOwners.IsAppOwner(email),
                IsAdmin: user.IsAdmin),
            Permissions: new UserPermissions(
                CanAddWorkspace: user.CanAddWorkspace,
                CanManageGeneralSettings: user.CanManageGeneralSettings,
                CanManageUsers: user.CanManageUsers,
                CanManageStorages: user.CanManageStorages,
                CanManageEmailProviders: user.CanManageEmailProviders),
            Invitation: user.IsInvitation
                ? new UserInvitation(Code: user.InvitationCode!)
                : null,
            MaxWorkspaceNumber: user.MaxWorkspaceNumber,
            DefaultMaxWorkspaceSizeInBytes: user.DefaultMaxWorkspaceSizeInBytes);
    }    
}