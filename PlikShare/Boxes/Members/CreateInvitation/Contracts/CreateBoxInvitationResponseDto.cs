using PlikShare.Users.Id;

namespace PlikShare.Boxes.Members.CreateInvitation.Contracts;

public class CreateBoxInvitationResponseDto
{    
    public required List<BoxInvitationMember> Members { get; set; }

    public record BoxInvitationMember(
        string Email, 
        UserExtId ExternalId);
}