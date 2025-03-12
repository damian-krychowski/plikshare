using PlikShare.Users.Id;

namespace PlikShare.Users.Invite.Contracts;

public record InviteUsersRequestDto(
    List<string> Emails);

public record InviteUsersResponseDto(
    List<InvitedUserDto> Users);
    
public record InvitedUserDto(
    string Email,
    UserExtId ExternalId);