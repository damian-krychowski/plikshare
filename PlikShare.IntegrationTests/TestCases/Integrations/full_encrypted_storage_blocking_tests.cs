using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.ArtificialIntelligence.SendFileMessage.Contracts;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Aws.Textract.Jobs.StartJob.Contracts;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Get.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Integrations;

[Collection(IntegrationTestsCollection.Name)]
public class full_encrypted_storage_blocking_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public full_encrypted_storage_blocking_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task workspace_on_full_encrypted_storage_exposes_no_integrations_even_when_active_integrations_exist_on_another_storage()
    {
        //given: an active Textract and an active ChatGPT on a non-encrypted storage
        var otherStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var textract = await Api.Integrations.Create(
            request: new CreateAwsTextractIntegrationRequestDto
            {
                Name = Random.Name("Textract"),
                StorageExternalId = otherStorage.ExternalId,
                AccessKey = Random.ClientId(),
                SecretAccessKey = Random.ClientSecret(),
                Region = "us-east-1"
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Integrations.Activate(
            externalId: textract.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var chatGpt = await Api.Integrations.Create(
            request: new CreateOpenAiChatGptIntegrationRequestDto
            {
                Name = Random.Name("ChatGpt"),
                StorageExternalId = otherStorage.ExternalId,
                ApiKey = Random.ClientSecret()
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Integrations.Activate(
            externalId: chatGpt.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var fullWorkspace = await CreateWorkspace(
            storage: fullStorage,
            user: AppOwner);

        //when
        var details = await Api.Workspaces.GetDetails(
            externalId: fullWorkspace.ExternalId,
            cookie: AppOwner.Cookie);

        //then: integration section is empty despite clients being globally registered
        details.Integrations.Should().BeEquivalentTo(new WorkspaceIntegrationsDto
        {
            Textract = null,
            ChatGpt = []
        });
    }

    [Fact]
    public async Task starting_textract_job_on_workspace_with_full_encrypted_storage_returns_bad_request()
    {
        //given
        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: fullStorage,
            user: AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Textract.StartJob(
                workspaceExternalId: workspace.ExternalId,
                request: new StartTextractJobRequestDto
                {
                    FileExternalId = FileExtId.NewId(),
                    Features = [TextractFeature.Layout]
                },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("integration-not-supported-on-encrypted-storage");
    }

    [Fact]
    public async Task sending_ai_file_message_on_workspace_with_full_encrypted_storage_returns_bad_request()
    {
        //given
        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: fullStorage,
            user: AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Ai.SendFileMessage(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: FileExtId.NewId(),
                request: new SendAiFileMessageRequestDto
                {
                    FileArtifactExternalId = FileArtifactExtId.NewId(),
                    ConversationExternalId = AiConversationExtId.NewId(),
                    MessageExternalId = AiMessageExtId.NewId(),
                    ConversationCounter = 0,
                    Message = "hello",
                    Includes = [],
                    AiIntegrationExternalId = IntegrationExtId.NewId(),
                    AiModel = "gpt-4o-mini"
                },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("integration-not-supported-on-encrypted-storage");
    }
}
