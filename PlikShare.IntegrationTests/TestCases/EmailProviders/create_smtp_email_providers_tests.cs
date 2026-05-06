using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlikShare.AuditLog;
using PlikShare.Core.Emails;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.Smtp;
using PlikShare.EmailProviders.ExternalProviders.Smtp.Create;
using PlikShare.EmailProviders.List.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.EmailProviders;

[Collection(IntegrationTestsCollection.Name)]
public class create_smtp_email_providers_tests : TestFixture
{
    private const string EmailFrom = "PlikShare <damian@plikshare.com>";
    private const string EmailFromAddress = "damian@plikshare.com";

    public create_smtp_email_providers_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.RemoveAllEmailProviders();
        hostFixture.SmtpTestServer.ClearReceivedEmails();
    }

    [Fact]
    public async Task when_smtp_provider_with_authentication_is_created_it_authenticates_and_sends_confirmation_email()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var smtpUsername = $"user-{Guid.NewGuid().ToBase62()}";
        var smtpPassword = $"pwd-{Guid.NewGuid().ToBase62()}";
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: true,
                Username: smtpUsername,
                Password: smtpPassword),
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

        var received = SmtpTestServer.GetLastEmailTo(Users.AppOwner.Email);
        received.Should().NotBeNull();
        received!.AuthenticatedUsername.Should().Be(smtpUsername);
        received.AuthenticatedPassword.Should().Be(smtpPassword);
        received.MailFrom.Should().Be(EmailFromAddress);
        received.RcptTo.Should().Equal(Users.AppOwner.Email);
        received.Subject.Should().Be(expectedTitle);
        // MimeKit appends a trailing CRLF when decoding the DATA section; trim both sides.
        received.HtmlBody.TrimEnd().Should().Be(expectedHtml.TrimEnd());
    }

    [Fact]
    public async Task when_anonymous_smtp_provider_is_created_it_sends_confirmation_email_without_authenticating()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: false,
                Username: string.Empty,
                Password: string.Empty),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var (expectedTitle, _) = Emails.EmailProviderConfirmation(
            applicationName: AppSettings.ApplicationName.Name,
            emailProviderName: emailProviderName,
            confirmationCode: confirmationCode);

        var received = SmtpTestServer.GetLastEmailTo(Users.AppOwner.Email);
        received.Should().NotBeNull();
        received!.AuthenticatedUsername.Should().BeNull(
            "anonymous SMTP must skip the AUTH handshake entirely");
        received.AuthenticatedPassword.Should().BeNull();
        received.MailFrom.Should().Be(EmailFromAddress);
        received.RcptTo.Should().Equal(Users.AppOwner.Email);
        received.Subject.Should().Be(expectedTitle);
    }

    [Fact]
    public async Task creating_anonymous_smtp_provider_with_credentials_blanks_them_in_persisted_details()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        var response = await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: false,
                Username: "leaking-username-should-not-persist",
                Password: "leaking-password-should-not-persist"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var details = ReadSmtpDetails(response.ExternalId.Value);

        details.RequiresAuthentication.Should().BeFalse();
        details.Username.Should().BeEmpty(
            "endpoint must blank credentials when authentication is disabled");
        details.Password.Should().BeEmpty();
    }

    [Fact]
    public async Task anonymous_smtp_provider_full_lifecycle_succeeds()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        var provider = await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: false,
                Username: string.Empty,
                Password: string.Empty),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var emailProviders = await Api.EmailProviders.Get(cookie: user.Cookie);

        emailProviders.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items:
            [
                new GetEmailProvidersItemResponseDto(
                    ExternalId: provider.ExternalId,
                    Type: EmailProviderType.Smtp.Value,
                    Name: emailProviderName,
                    EmailFrom: EmailFrom,
                    IsConfirmed: true,
                    IsActive: true)
            ]));
    }

    [Fact]
    public async Task creating_smtp_provider_with_authentication_records_audit_log_with_is_anonymous_false()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: true,
                Username: $"user-{Guid.NewGuid().ToBase62()}",
                Password: $"pwd-{Guid.NewGuid().ToBase62()}"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.EmailProvider.Created>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Created,
            assertDetails: details =>
            {
                details.EmailProvider.Name.Should().Be(emailProviderName);
                details.EmailProvider.Type.Should().Be(EmailProviderType.Smtp.Value);
                details.EmailFrom.Should().Be(EmailFrom);
                details.IsAnonymous.Should().BeFalse();
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task creating_anonymous_smtp_provider_records_audit_log_with_is_anonymous_true()
    {
        //given
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";

        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var user = await SignIn(user: Users.AppOwner);

        //when
        await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: false,
                Username: string.Empty,
                Password: string.Empty),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.EmailProvider.Created>(
            expectedEventType: AuditLogEventTypes.EmailProvider.Created,
            assertDetails: details =>
            {
                details.EmailProvider.Name.Should().Be(emailProviderName);
                details.EmailProvider.Type.Should().Be(EmailProviderType.Smtp.Value);
                details.IsAnonymous.Should().BeTrue();
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("user", "")]
    [InlineData("", "")]
    public async Task creating_smtp_provider_with_authentication_required_and_blank_credentials_returns_400(
        string username,
        string password)
    {
        //given
        var emailProviderName = $"Smtp-{Guid.NewGuid().ToBase62()}";
        var user = await SignIn(user: Users.AppOwner);

        //when
        var act = async () => await Api.EmailProviders.CreateSmtp(
            request: new CreateSmtpEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: EmailFrom,
                Hostname: SmtpTestServer.Hostname,
                Port: SmtpTestServer.PortNumber,
                SslMode: SslMode.None,
                RequiresAuthentication: true,
                Username: username,
                Password: password),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var thrown = await act.Should().ThrowAsync<TestApiCallException>();
        thrown.Which.StatusCode.Should().Be(400);
        thrown.Which.HttpError.Should().NotBeNull();
        thrown.Which.HttpError!.Code.Should().Be("email-provider-credentials-required");

        // Provider must NOT have been persisted, and no SMTP traffic must have happened.
        var emailProviders = await Api.EmailProviders.Get(cookie: user.Cookie);
        emailProviders.Items.Should().BeEmpty();

        SmtpTestServer.GetLastEmailTo(Users.AppOwner.Email).Should().BeNull();
    }

    private SmtpDetailsEntity ReadSmtpDetails(string externalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT ep_details_encrypted
                     FROM ep_email_providers
                     WHERE ep_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0))
            .WithParameter("$externalId", externalId)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"Email provider '{externalId}' was not found in the database.");

        var masterDataEncryption = HostFixture.App.Services.GetRequiredService<IMasterDataEncryption>();
        var json = masterDataEncryption.DecryptString(rows[0]);

        return Json.Deserialize<SmtpDetailsEntity>(json)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize SmtpDetailsEntity for provider '{externalId}'.");
    }
}
