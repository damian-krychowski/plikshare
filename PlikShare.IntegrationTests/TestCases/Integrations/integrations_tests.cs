using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Integrations;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.List.Contracts;
using PlikShare.Integrations.UpdateName.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Integrations;

[Collection(IntegrationTestsCollection.Name)]
public class integrations_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public integrations_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    [Fact]
    public async Task when_integration_is_created_it_is_visible_on_the_list()
    {
        //given
        var integrationName = Random.Name("Textract");

        //when
        var response = await Api.Integrations.Create(
            request: new CreateAwsTextractIntegrationRequestDto
            {
                Name = integrationName,
                StorageExternalId = Storage.ExternalId,
                AccessKey = Random.ClientId(),
                SecretAccessKey = Random.ClientSecret(),
                Region = "us-east-1"
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var integrations = await Api.Integrations.Get(
            cookie: AppOwner.Cookie);

        integrations.Items.Should().Contain(i =>
            i.ExternalId == response.ExternalId &&
            i.Name == integrationName &&
            i.Type == IntegrationType.AwsTextract);
    }

    [Fact]
    public async Task when_integration_is_deleted_it_is_removed_from_the_list()
    {
        //given
        var response = await CreateTextractIntegration();

        //when
        await Api.Integrations.Delete(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var integrations = await Api.Integrations.Get(
            cookie: AppOwner.Cookie);

        integrations.Items.Should().NotContain(i => i.ExternalId == response.ExternalId);
    }

    [Fact]
    public async Task when_integration_name_is_updated_it_is_reflected_on_the_list()
    {
        //given
        var response = await CreateTextractIntegration();
        var newName = Random.Name("RenamedIntegration");

        //when
        await Api.Integrations.UpdateName(
            externalId: response.ExternalId,
            request: new UpdateIntegrationNameRequestDto
            {
                Name = newName
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var integrations = await Api.Integrations.Get(
            cookie: AppOwner.Cookie);

        integrations.Items.Should().Contain(i =>
            i.ExternalId == response.ExternalId &&
            i.Name == newName);
    }

    [Fact]
    public async Task when_integration_is_activated_it_is_reflected_on_the_list()
    {
        //given
        var response = await CreateTextractIntegration();

        //when
        await Api.Integrations.Activate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var integrations = await Api.Integrations.Get(
            cookie: AppOwner.Cookie);

        integrations.Items.Should().Contain(i =>
            i.ExternalId == response.ExternalId &&
            i.IsActive);
    }

    [Fact]
    public async Task when_integration_is_deactivated_it_is_reflected_on_the_list()
    {
        //given
        var response = await CreateTextractIntegration();

        await Api.Integrations.Activate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Integrations.Deactivate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var integrations = await Api.Integrations.Get(
            cookie: AppOwner.Cookie);

        integrations.Items.Should().Contain(i =>
            i.ExternalId == response.ExternalId &&
            !i.IsActive);
    }

    [Fact]
    public async Task creating_integration_should_produce_audit_log_entry()
    {
        //given
        var integrationName = Random.Name("Textract");

        //when
        await Api.Integrations.Create(
            request: new CreateAwsTextractIntegrationRequestDto
            {
                Name = integrationName,
                StorageExternalId = Storage.ExternalId,
                AccessKey = Random.ClientId(),
                SecretAccessKey = Random.ClientSecret(),
                Region = "us-east-1"
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Integration.Created>(
            expectedEventType: AuditLogEventTypes.Integration.Created,
            assertDetails: details =>
            {
                details.Integration.Name.Should().Be(integrationName);
                details.Integration.Type.Should().Be(IntegrationType.AwsTextract.ToString());
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_integration_should_produce_audit_log_entry()
    {
        //given
        var response = await CreateTextractIntegration();

        //when
        await Api.Integrations.Delete(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Integration.Deleted>(
            expectedEventType: AuditLogEventTypes.Integration.Deleted,
            assertDetails: details => details.Integration.ExternalId.Should().Be(response.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_integration_name_should_produce_audit_log_entry()
    {
        //given
        var response = await CreateTextractIntegration();
        var newName = Random.Name("RenamedIntegration");

        //when
        await Api.Integrations.UpdateName(
            externalId: response.ExternalId,
            request: new UpdateIntegrationNameRequestDto
            {
                Name = newName
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Integration.NameUpdated>(
            expectedEventType: AuditLogEventTypes.Integration.NameUpdated,
            assertDetails: details =>
            {
                details.Integration.ExternalId.Should().Be(response.ExternalId);
                details.Integration.Name.Should().Be(newName);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task activating_integration_should_produce_audit_log_entry()
    {
        //given
        var response = await CreateTextractIntegration();

        //when
        await Api.Integrations.Activate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Integration.ActivationChanged>(
            expectedEventType: AuditLogEventTypes.Integration.Activated,
            assertDetails: details => details.Integration.ExternalId.Should().Be(response.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deactivating_integration_should_produce_audit_log_entry()
    {
        //given
        var response = await CreateTextractIntegration();

        await Api.Integrations.Activate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Integrations.Deactivate(
            externalId: response.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Integration.ActivationChanged>(
            expectedEventType: AuditLogEventTypes.Integration.Deactivated,
            assertDetails: details => details.Integration.ExternalId.Should().Be(response.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task creating_aws_textract_integration_on_full_encrypted_storage_returns_bad_request()
    {
        //given
        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Integrations.Create(
                request: new CreateAwsTextractIntegrationRequestDto
                {
                    Name = Random.Name("Textract"),
                    StorageExternalId = fullStorage.ExternalId,
                    AccessKey = Random.ClientId(),
                    SecretAccessKey = Random.ClientSecret(),
                    Region = "us-east-1"
                },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("integration-not-supported-on-encrypted-storage");
    }

    [Fact]
    public async Task creating_openai_chatgpt_integration_on_full_encrypted_storage_returns_bad_request()
    {
        //given
        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Integrations.Create(
                request: new CreateOpenAiChatGptIntegrationRequestDto
                {
                    Name = Random.Name("ChatGpt"),
                    StorageExternalId = fullStorage.ExternalId,
                    ApiKey = Random.ClientSecret()
                },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("integration-not-supported-on-encrypted-storage");
    }

    private async Task<CreateIntegrationResponseDto> CreateTextractIntegration()
    {
        return await Api.Integrations.Create(
            request: new CreateAwsTextractIntegrationRequestDto
            {
                Name = Random.Name("Textract"),
                StorageExternalId = Storage.ExternalId,
                AccessKey = Random.ClientId(),
                SecretAccessKey = Random.ClientSecret(),
                Region = "us-east-1"
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }
}
