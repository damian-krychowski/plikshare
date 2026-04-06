using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Core.Emails;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.Id;
using PlikShare.EmailProviders.List.Contracts;
using PlikShare.EmailProviders.UpdateName.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.EmailProviders;

[Collection(IntegrationTestsCollection.Name)]
public class create_resend_email_providers_tests: TestFixture
{
    public create_resend_email_providers_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.RemoveAllEmailProviders();
    }
    
    [Fact]
    public async Task when_resend_email_provider_is_created_confirmation_code_is_correctly_sent()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        
        OneTimeCode.NextCodeToGenerate(confirmationCode);
        
        var user = await SignIn(
            user: Users.AppOwner);
        
        //when
        await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var (expectedTitle, expectedContent) = Emails.EmailProviderConfirmation(
            applicationName: AppSettings.ApplicationName.Name,
            emailProviderName: emailProviderName,
            confirmationCode: confirmationCode);
        
        var expectedHtml = EmailTemplates.Generic.Build(
            title: expectedTitle,
            content: expectedContent);
        
        ResendEmailServer.ReceivedEmails.Should().ContainEquivalentOf(new ResendEmailServer.ReceivedRequest(
            Body: new ResendRequestBody(
                From: emailFrom,
                To: [Users.AppOwner.Email],
                Subject: expectedTitle,
                Html: expectedHtml),
            AuthorizationHeader: $"Bearer {apiKey}"));
    }
    
    [Fact]
    public async Task when_resend_email_provider_is_created_it_is_visible_on_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        
        OneTimeCode.NextCodeToGenerate(confirmationCode);
        
        var user = await SignIn(
            user: Users.AppOwner);
        
        //when
        var response = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: response.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName,
                    EmailFrom: emailFrom,
                    IsConfirmed: false,
                    IsActive: false)
            ]));
    }
    
    [Fact]
    public async Task when_resend_email_provider_is_confirmed_it_is_reflected_on_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        
        OneTimeCode.NextCodeToGenerate(confirmationCode);
        
        var user = await SignIn(
            user: Users.AppOwner);
        
        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //when
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName,
                    EmailFrom: emailFrom,
                    IsConfirmed: true,
                    IsActive: false)
            ]));
    }
    
    
    [Fact]
    public async Task when_resend_email_provider_is_activated_it_is_reflected_on_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        
        OneTimeCode.NextCodeToGenerate(confirmationCode);
        
        var user = await SignIn(
            user: Users.AppOwner);
        
        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //when
        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName,
                    EmailFrom: emailFrom,
                    IsConfirmed: true,
                    IsActive: true)
            ]));
    }
    
    [Fact]
    public async Task activation_of_new_provider_deactivates_the_previous_one()
    {
        //given
        var confirmationCode1 = Guid.NewGuid().ToBase62();
        var confirmationCode2 = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName1 = $"Resend-{Guid.NewGuid().ToBase62()}";
        var emailProviderName2 = $"Resend-{Guid.NewGuid().ToBase62()}";
        
        OneTimeCode.NextCodeToGenerate(confirmationCode1);
        
        var user = await SignIn(
            user: Users.AppOwner);
        
        var provider1 = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName1,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider1.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode1),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider1.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //when
        OneTimeCode.NextCodeToGenerate(confirmationCode2);
        
        var provider2 = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName2,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider2.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode2),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider2.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider1.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName1,
                    EmailFrom: emailFrom,
                    IsConfirmed: true,
                    IsActive: false),
                
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider2.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName2,
                    EmailFrom: emailFrom,
                    IsConfirmed: true,
                    IsActive: true)
            ]));
    }

    [Fact]
    public async Task when_resend_email_provider_is_deactivated_it_is_reflected_on_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Deactivate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: emailProviderName,
                    EmailFrom: emailFrom,
                    IsConfirmed: true,
                    IsActive: false)
            ]));
    }

    [Fact]
    public async Task when_resend_email_provider_is_deleted_it_is_removed_from_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Delete(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task when_resend_email_provider_name_is_updated_it_is_reflected_on_the_list()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        var newName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.UpdateName(
            emailProviderExternalId: provider.ExternalId,
            request: new UpdateEmailProviderNameRequestDto(
                Name: newName),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider.ExternalId,
                    Type: EmailProviderType.Resend.Value,
                    Name: newName,
                    EmailFrom: emailFrom,
                    IsConfirmed: false,
                    IsActive: false)
            ]));
    }

    [Fact]
    public async Task creating_resend_email_provider_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.Created>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Created,
            assertDetails: details =>
            {
                details.Name.Should().Be(emailProviderName);
                details.Type.Should().Be(EmailProviderType.Resend.Value);
                details.EmailFrom.Should().Be(emailFrom);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task confirming_email_provider_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.ActivationChanged>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Confirmed,
            assertDetails: details => details.ExternalId.Should().Be(provider.ExternalId.Value),
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task activating_email_provider_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.ActivationChanged>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Activated,
            assertDetails: details => details.ExternalId.Should().Be(provider.ExternalId.Value),
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deactivating_email_provider_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Deactivate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.ActivationChanged>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Deactivated,
            assertDetails: details => details.ExternalId.Should().Be(provider.ExternalId.Value),
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task deleting_email_provider_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Delete(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.Deleted>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Deleted,
            assertDetails: details => details.ExternalId.Should().Be(provider.ExternalId.Value),
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_email_provider_name_should_produce_audit_log_entry()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var apiKey = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        var newName = $"Resend-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: apiKey),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.UpdateName(
            emailProviderExternalId: provider.ExternalId,
            request: new UpdateEmailProviderNameRequestDto(
                Name: newName),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.EmailProvider.NameUpdated>(
            expectedEventType: AuditLogEventTypes.EmailProvider.NameUpdated,
            assertDetails: details =>
            {
                details.ExternalId.Should().Be(provider.ExternalId.Value);
                details.Name.Should().Be(newName);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}