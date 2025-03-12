namespace PlikShare.Core.Emails;

public enum EmailTemplate
{
    WorkspaceMembershipInvitation = 1,
    WorkspaceMembershipInvitationAccepted,
    WorkspaceMembershipInvitationRejected,
    WorkspaceMembershipRevoked,
    WorkspaceMemberLeft,
    
    BoxMembershipInvitation,
    BoxMembershipInvitationAccepted,
    BoxMembershipInvitationRejected,
    BoxMembershipRevoked,
    BoxMemberLeft,
    
    Alert,
    
    ConfirmationEmail,
    ResetPassword,
    
    UserInvitation
}