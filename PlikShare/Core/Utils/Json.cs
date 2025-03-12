using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlikShare.ArtificialIntelligence.GetMessages;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Boxes.Id;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Emails;
using PlikShare.Core.ExternalIds;
using PlikShare.EmailProviders.ExternalProviders.Smtp;
using PlikShare.EmailProviders.Id;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetDetails.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Integrations;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Aws.Textract.Id;
using PlikShare.Integrations.Aws.Textract.Jobs;
using PlikShare.Integrations.Aws.Textract.TestConfiguration.Contracts;
using PlikShare.Integrations.Id;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;
using PlikShare.Users.Id;
using PlikShare.Users.UpdatePermission.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.Core.Utils;

public static class Json
{
    public static readonly JsonSerializerOptions Options;
    public static readonly JsonSerializerOptions OptionsWithIndentation;

    static Json()
    {
        Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        
        Options.AddConverters();

        OptionsWithIndentation = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        OptionsWithIndentation.AddConverters();
    }

    public static string SerializeWithIndentation<T>(T item)
    {
        return JsonSerializer.Serialize<T>(item, OptionsWithIndentation);
    }

    public static byte[] SerializeToBlob<T>(T item)
    {
        var json = Serialize(item);
        return Encoding.UTF8.GetBytes(json);
    }

    public static string Serialize<T>(T item)
    {
        return JsonSerializer.Serialize<T>(item, Options);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(
            utf8Json: Encoding.UTF8.GetBytes(json),
            options: Options);
    }
}

public static class JsonConverters
{
    public static void AddConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new ExternalIdJsonConverter<UserExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<WorkspaceExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<FileUploadExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<BulkFileUploadExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<FolderExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<FileExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<FileArtifactExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<BoxLinkExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<BoxExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<StorageExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<IntegrationExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<EmailProviderExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<TextractJobExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<AiConversationExtId>());
        options.Converters.Add(new ExternalIdJsonConverter<AiMessageExtId>());
        
        options.Converters.Add(new JsonStringEnumConverter<EmailTemplate>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SslMode>(JsonNamingPolicy.CamelCase));

        //I prefer to have enum as kebab-case rather than camelCase because they look better when displayed directly on the frontend
        options.Converters.Add(new JsonStringEnumConverter<StorageEncryptionType>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<ContentDispositionType>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<IntegrationType>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<UpdateUserPermissionOperation>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<UploadAlgorithm>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<TestTextractConfigurationResultCode>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<TextractFeature>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<TextractJobStatus>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<FileArtifactType>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<FilePreviewDetailsField>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<AiMessageAuthorType>(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<FileType>(JsonNamingPolicy.KebabCaseLower));

        options.Converters.Add(new NullableByteArrayJsonConverter());
        options.Converters.Add(new S3FileKeyJsonConverter());
    }
}