using FluentAssertions;
using PlikShare.Core.Emails;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.List.Contracts;
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
            applicationName: "PlikShare",
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
}