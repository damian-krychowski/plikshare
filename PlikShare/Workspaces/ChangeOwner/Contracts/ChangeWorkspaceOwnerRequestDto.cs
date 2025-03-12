using PlikShare.Users.Id;

namespace PlikShare.Workspaces.ChangeOwner.Contracts;

public record ChangeWorkspaceOwnerRequestDto(UserExtId NewOwnerExternalId);