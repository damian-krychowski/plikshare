using PlikShare.Users.Id;

namespace PlikShare.Account.GetKnownUsers.Contracts;

public record GetKnownUsersResponseDto(
    KnownUserDto[] Items);

public record KnownUserDto(
    UserExtId ExternalId,
    string Email);