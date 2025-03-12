using PlikShare.Boxes.Permissions;
using PlikShare.Users.Cache;

namespace PlikShare.Boxes.Cache;

public record BoxMembershipContext(
    bool? WasInvitationAccepted,
    UserContext? Inviter,
    UserContext Member,
    BoxContext Box,
    BoxPermissions Permissions);