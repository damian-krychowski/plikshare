using System.Text.Json;
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
            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationEmailDefinition>>(
                json);

            var (title, content) = Emails.WorkspaceMembershipInvitation(
                applicationName: appSettings.ApplicationName.Name!,
                appUrl: config.AppUrl,
                inviterEmail: operation!.Definition.InviterEmail,
                workspaceName: operation.Definition.WorkspaceName,
                invitationCode: operation.Definition.InvitationCode);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetWorkspaceInvitationAcceptedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationAcceptedEmailDefinition>>(
                json);

            var (title, content) = Emails.WorkspaceMembershipInvitationAccepted(
                applicationName: appSettings.ApplicationName.Name!,
                inviteeEmail: operation!.Definition.InviteeEmail,
                workspaceName: operation.Definition.WorkspaceName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetWorkspaceInvitationRejectedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipInvitationRejectedEmailDefinition>>(
                json);

            var (title, content) = Emails.WorkspaceMembershipInvitationRejected(
                applicationName: appSettings.ApplicationName.Name!,
                memberEmail: operation!.Definition.MemberEmail,
                workspaceName: operation.Definition.WorkspaceName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetWorkspaceAccessRevokedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMembershipRevokedEmailDefinition>>(
                json);

            var (title, content) = Emails.WorkspaceMembershipRevoked(
                applicationName: appSettings.ApplicationName.Name!,
                workspaceName: operation!.Definition.WorkspaceName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetWorkspaceLeftEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<WorkspaceMemberLeftEmailDefinition>>(
                json);

            var (title, content) = Emails.WorkspaceMemberLeft(
                applicationName: appSettings.ApplicationName.Name!,
                memberEmail: operation!.Definition.MemberEmail,
                workspaceName: operation.Definition.WorkspaceName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetBoxInvitationEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationEmailDefinition>>(
                json);

            var (title, content) = Emails.BoxMembershipInvitation(
                applicationName: appSettings.ApplicationName.Name!,
                appUrl: config.AppUrl,
                inviterEmail: operation!.Definition.InviterEmail,
                boxName: operation.Definition.BoxName,
                invitationCode: operation.Definition.InvitationCode);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetBoxInvitationAcceptedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationAcceptedEmailDefinition>>(
                json);

            var (title, content) = Emails.BoxMembershipInvitationAccepted(
                applicationName: appSettings.ApplicationName.Name!,
                inviteeEmail: operation!.Definition.InviteeEmail,
                boxName: operation.Definition.BoxName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetBoxInvitationRejectedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipInvitationRejectedEmailDefinition>>(
                json);

            var (title, content) = Emails.BoxMembershipInvitationRejected(
                applicationName: appSettings.ApplicationName.Name!,
                inviteeEmail: operation!.Definition.InviteeEmail,
                boxName: operation.Definition.BoxName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetBoxAccessRevokedEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMembershipRevokedEmailDefinition>>(
                json);

            var (title, content) = Emails.BoxMembershipRevoked(
                applicationName: appSettings.ApplicationName.Name!,
                boxName: operation!.Definition.BoxName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetBoxLeftEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<BoxMemberLeftEmailDefinition>>(
                json);

            var (title, content) = Emails.BoxMemberLeft(
                applicationName: appSettings.ApplicationName.Name!,
                memberEmail: operation!.Definition.MemberEmail,
                boxName: operation.Definition.BoxName);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetAlertEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<AlertEmailDefinition>>(
                json);

            var title = $"{appSettings.ApplicationName.Name} - Alert - {operation!.Definition.Title}";

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

            var (title, content) = Emails.ConfirmationEmail(
                applicationName: appSettings.ApplicationName.Name!,
                link: operation!.Definition.Link);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private Email GetResetPasswordEmail(string json)
        {
            var operation = Json.Deserialize<EmailQueueJobDefinition<ResetPasswordEmailDefinition>>(
                json);

            var (title, content) = Emails.ResetPassword(
                applicationName: appSettings.ApplicationName.Name!,
                link: operation!.Definition.Link);

            var htmlContent = genericEmailTemplate.Build(
                title: title,
                content: content);

            return new Email(operation.Email, title, htmlContent);
        }

        private record Email(
            string To,
            string Title,
            string Content);
    }
}