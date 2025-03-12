using System.Text.Json.Serialization;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Integrations.Create.Contracts;

[JsonDerivedType(derivedType: typeof(CreateAwsTextractIntegrationRequestDto), typeDiscriminator: "aws-textract")]
[JsonDerivedType(derivedType: typeof(CreateOpenAiChatGptIntegrationRequestDto), typeDiscriminator: "openai-chatgpt")]
public abstract class CreateIntegrationRequestDto
{
    public required string Name { get; init; }
}

public class CreateAwsTextractIntegrationRequestDto : CreateIntegrationRequestDto
{
    public required string AccessKey { get; init; }
    public required string SecretAccessKey { get; init; }
    public required string Region { get; init; }
    public required StorageExtId StorageExternalId { get; init; }
}

public class CreateOpenAiChatGptIntegrationRequestDto : CreateIntegrationRequestDto
{
    public required string ApiKey { get; init; }
    public required StorageExtId StorageExternalId { get; init; }
}

public class CreateIntegrationResponseDto
{
    public required IntegrationExtId ExternalId { get; init; }
    public required CreateIntegrationWorkspaceDto Workspace { get; init; }
}

public class CreateIntegrationWorkspaceDto
{
    public required WorkspaceExtId ExternalId { get; init; }
    public required string Name { get; init; }
}

