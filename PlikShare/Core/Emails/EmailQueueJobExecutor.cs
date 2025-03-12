using System.Text;
using System.Text.Json;
using Flurl;
using PlikShare.Core.Configuration;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Emails.Templates;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;

namespace PlikShare.Core.Emails
{
    //todo add logging
    public class EmailQueueJobExecutor(
        IConfig config,
        AppSettings appSettings,
        EmailProviderStore emailProviderStore,
        GenericEmailTemplate genericEmailTemplate) : IQueueNormalJobExecutor
    {
        public string JobType => EmailQueueJobType.Value;
        public int Priority => QueueJobPriority.High;

        private string Title(string title) => $"{appSettings.ApplicationName.Name} - {title}";
        
        public async Task<QueueJobResult> Execute(
            string definitionJson, 
            Guid correlationId, 
            CancellationToken cancellationToken)
        {
            var emailTemplate = ExtractEmailTemplate(
                definitionJson);

            var email = GetEmail(
                definitionJson, 
                emailTemplate);

            if (emailProviderStore.EmailSender is null) 
                return QueueJobResult.Blocked;
            
            await emailProviderStore.EmailSender.SendEmail(
                to: email.To,
                subject: email.Title,
                htmlContent: email.Content,
                cancellationToken: cancellationToken);

            return QueueJobResult.Success;
        }

        private Email GetEmail(
            string definitionJson,  
            EmailTemplate emailTemplate)
        {
            return emailTemplate switch
            {
                EmailTemplate.UserInvitation => GetInvitationEmail(
                    definitionJson),
                
                EmailTemplate.WorkspaceMembershipInvitation => GetWorkspaceInvitationEmail(
                    definitionJson),
                
                EmailTemplate.WorkspaceMembershipInvitationAccepted => GetWorkspaceInvitationAcceptedEmail(
                    definitionJson),
                
                EmailTemplate.WorkspaceMembershipInvitationRejected => GetWorkspaceInvitationRejectedEmail(
                    definitionJson),
                
                EmailTemplate.WorkspaceMembershipRevoked => GetWorkspaceAccessRevokedEmail(
                    definitionJson),
                
                EmailTemplate.WorkspaceMemberLeft => GetWorkspaceLeftEmail(
                    definitionJson),
                
                EmailTemplate.BoxMembershipInvitation => GetBoxInvitationEmail(
                    definitionJson),
                
                EmailTemplate.BoxMembershipInvitationAccepted => GetBoxInvitationAcceptedEmail(
                    definitionJson),
                    
                EmailTemplate.BoxMembershipInvitationRejected => GetBoxInvitationRejectedEmail(
                    definitionJson),
                    
                EmailTemplate.BoxMembershipRevoked => GetBoxAccessRevokedEmail(
                    definitionJson),
                    
                EmailTemplate.BoxMemberLeft => GetBoxLeftEmail(
                    definitionJson),
                    
                EmailTemplate.Alert => GetAlertEmail(
                    definitionJson),
                    
                EmailTemplate.ConfirmationEmail => GetConfirmationEmail(
                    definitionJson),
                    
                EmailTemplate.ResetPassword => GetResetPasswordEmail(
                    definitionJson),
                    
                _ => throw new InvalidOperationException($"Unknown email template type '{emailTemplate}'.")
            };
        }

        private static EmailTemplate ExtractEmailTemplate(string definitionJson)
        {
            var jsonDocument = JsonDocument
                .Parse(definitionJson);
            
            var templateName = jsonDocument
                .RootElement
                .GetProperty("template")
                .GetString();
            
            if (!Enum.TryParse<EmailTemplate>(
                    value: templateName, 
                    ignoreCase: true, 
                    result: out var emailTemplate))
            {
                throw new InvalidOperationException(
                    $"Invalid email template type '{templateName}'.");
            }

            return emailTemplate;
        }

        private Email GetInvitationEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<UserInvitationEmailDefinition>>(
                json: json);

            var (title, content) = Emails.UserInvitation(
                applicationName: appSettings.ApplicationName.Name!,
                appUrl: config.AppUrl,
                inviterEmail: operation!.Definition.InviterEmail,
                invitationCode: operation.Definition.InvitationCode);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetWorkspaceInvitationEmail(string json)
        {
           var title = Title("you were invited to Workspace");

            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: PrepareInvitationMessage(
                    messageIntro: $"User <strong>{operation!.Definition.InviterEmail}</strong> has invited you to join <strong>'{operation.Definition.WorkspaceName}'</strong> Workspace.",
                    invitationCode: operation.Definition.InvitationCode));

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetWorkspaceInvitationAcceptedEmail(string json)
        {
            var title = Title("your invitation was accepted");

            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationAcceptedEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.InviteeEmail}</strong> has accepted your invitation to join <strong>'{operation.Definition.WorkspaceName}'</strong> Workspace.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetWorkspaceInvitationRejectedEmail(string json)
        {
            var title = Title("workspace invitation was rejected");

            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationRejectedEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.MemberEmail}</strong> has rejected your invitation to join <strong>'{operation.Definition.WorkspaceName}'</strong> Workspace.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetWorkspaceAccessRevokedEmail(string json)
        {
            var title = Title("access to workspace was revoked");

            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipRevokedEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"Your access to  <strong>'{operation!.Definition.WorkspaceName}'</strong> Workspace was revoked.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetWorkspaceLeftEmail(string json)
        {
            var title = Title("user left your workspace");

            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMemberLeftEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.MemberEmail}</strong>  has left <strong>'{operation.Definition.WorkspaceName}'</strong> Workspace.");

            return new Email(operation.Email, title, htmlContent);
        }
        
         private Email GetBoxInvitationEmail(string json)
         {
             var title = Title("you were invited to file box");
            
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: PrepareInvitationMessage(
                    messageIntro: $"User <strong>{operation!.Definition.InviterEmail}</strong> has invited you to join <strong>'{operation.Definition.BoxName}'</strong> File Box.",
                    invitationCode: operation.Definition.InvitationCode));

            return new Email(operation.Email, title, htmlContent);
        }
        
        private string PrepareInvitationMessage(
            string messageIntro,
            string? invitationCode) {

            var msgBuilder = new StringBuilder();

            msgBuilder.Append(messageIntro);

            if (invitationCode is not null)
            {                
                msgBuilder.Append("<br><br>");
                msgBuilder.Append($"Use the following link to register to the website:");
                msgBuilder.Append("<br><br>");

                var link = new Url(config.AppUrl)
                    .AppendPathSegment("sign-up")
                    .AppendQueryParam("invitationCode", invitationCode)
                    .ToString();
                
                msgBuilder.Append(link);
            }

            msgBuilder.Append("<br><br>");
            msgBuilder.Append($"You can find the invitation here: <br/> <a href=\"{config.AppUrl}/workspaces\">{config.AppUrl}/workspaces</a>");

            return msgBuilder.ToString();
        }
        
        private Email GetBoxInvitationAcceptedEmail(string json)
        {
            var title = Title("box invitation was accepted");

            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationAcceptedEmailDefinition>>(
                json);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.InviteeEmail}</strong> has accepted your invitation to join <strong>'{operation.Definition.BoxName}'</strong> File Box.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetBoxInvitationRejectedEmail(string json)
        {
            var title = Title("box invitation was rejected");
            
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationRejectedEmailDefinition>>(
                json);
            
            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.InviteeEmail}</strong> has rejected your invitation to join <strong>'{operation.Definition.BoxName}'</strong> File Box.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetBoxAccessRevokedEmail(string json)
        {
            var title = Title("your access to file box was revoked");
            
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipRevokedEmailDefinition>>(
                json);
            
            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"Your access to  <strong>'{operation!.Definition.BoxName}'</strong> File Box was revoked.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetBoxLeftEmail(string json)
        {
            var title = Title("user left your file box");
            
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMemberLeftEmailDefinition>>(
                json);
            
            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: $"User <strong>{operation!.Definition.MemberEmail}</strong> has left  <strong>'{operation.Definition.BoxName}'</strong> File Box.");

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetAlertEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<AlertEmailDefinition>>(
                json);

            var title = Title($"Alert - {operation!.Definition.Title}");
            
            var htmlContent = AlertEmailTemplate.Build(
                title: title,
                eventDateTime: operation.Definition.EventDateTime,
                content: operation.Definition.Content);

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetConfirmationEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<ConfirmationEmailDefinition>>(
                json);

            var title = Title("confirm your email");

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: "Click this link to confirm your email address: " +
                         "<br><br>" +
                         operation!.Definition.Link);

            return new Email(operation.Email, title, htmlContent);
        }
        
        private Email GetResetPasswordEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<ResetPasswordEmailDefinition>>(
                json);

            var title = Title("reset your password");

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: "Click this link to reset your password: " +
                         "<br><br>" +
                         operation!.Definition.Link);

            return new Email(operation.Email, title, htmlContent);
        }

        private record Email(
            string To,
            string Title,
            string Content);
    }
}
