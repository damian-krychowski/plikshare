using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Storages.Delete;
using PlikShare.Storages.HardDrive.Create;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.HardDrive.GetVolumes;
using PlikShare.Storages.HardDrive.GetVolumes.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Storages.List;
using PlikShare.Storages.List.Contracts;
using PlikShare.Storages.S3.AwsS3;
using PlikShare.Storages.S3.AwsS3.Create;
using PlikShare.Storages.S3.AwsS3.Create.Contracts;
using PlikShare.Storages.S3.AwsS3.UpdateDetails;
using PlikShare.Storages.S3.AwsS3.UpdateDetails.Contracts;
using PlikShare.Storages.S3.CloudflareR2;
using PlikShare.Storages.S3.CloudflareR2.Create;
using PlikShare.Storages.S3.CloudflareR2.Create.Contracts;
using PlikShare.Storages.S3.CloudflareR2.UpdateDetails;
using PlikShare.Storages.S3.CloudflareR2.UpdateDetails.Contracts;
using PlikShare.Storages.S3.DigitalOcean;
using PlikShare.Storages.S3.DigitalOcean.Create;
using PlikShare.Storages.S3.DigitalOcean.Create.Contracts;
using PlikShare.Storages.S3.DigitalOcean.UpdateDetails;
using PlikShare.Storages.S3.DigitalOcean.UpdateDetails.Contracts;
using PlikShare.Storages.UpdateName;
using PlikShare.Storages.UpdateName.Contracts;

namespace PlikShare.Storages;

public static class StoragesEndpoints
{
    public static void MapStoragesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/storages")
            .WithTags("Storages")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageStorages));

        // Basic storage operations
        group.MapGet("/", GetStorages)
            .WithName("GetStorages");

        group.MapDelete("/{storageExternalId}", DeleteStorage)
            .WithName("DeleteStorage");

        group.MapPatch("/{storageExternalId}/name", UpdateName)
            .WithName("UpdateStorageName");

        // Cloudflare R2
        group.MapPost("/cloudflare-r2", CreateCloudflareR2Storage)
            .WithName("CreateCloudflareR2Storage");

        group.MapPatch("/cloudflare-r2/{storageExternalId}/details", UpdateCloudflareR2StorageDetails)
            .WithName("UpdateCloudflareR2StorageDetails");

        // AWS S3
        group.MapPost("/aws-s3", CreateAwsS3Storage)
            .WithName("CreateAwsS3Storage");

        group.MapPatch("/aws-s3/{storageExternalId}/details", UpdateAwsS3StorageDetails)
            .WithName("UpdateAwsS3StorageDetails");

        // DigitalOcean Spaces
        group.MapPost("/digitalocean-spaces", CreateDigitalOceanSpacesStorage)
            .WithName("CreateDigitalOceanSpacesStorage");

        group.MapPatch("/digitalocean-spaces/{storageExternalId}/details", UpdateDigitalOceanStorageDetails)
            .WithName("UpdateDigitalOceanStorageDetails");

        // Hard Drive
        group.MapPost("/hard-drive", CreateHardDriveStorage)
            .WithName("CreateHardDriveStorage");

        group.MapGet("/hard-drive/volumes", GetHardDriveVolumes)
            .WithName("GetHardDriveVolumes");
    }

    // Basic Storage Operations
    private static GetStoragesResponseDto GetStorages(GetStoragesQuery getStoragesQuery)
    {
        var storages = getStoragesQuery.Execute();

        return new GetStoragesResponseDto
        {
            Items = storages
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteStorage(
        [FromRoute] StorageExtId storageExternalId,
        DeleteStorageQuery deleteStorageQuery,
        StorageClientStore storageClientStore,
        CancellationToken cancellationToken)
    {
        var result = await deleteStorageQuery.Execute(
            storageExternalId,
            cancellationToken);

        switch (result.Code)
        {
            case DeleteStorageQuery.ResultCode.Ok:
                storageClientStore.RemoveClient(
                    result.StorageId);

                return TypedResults.Ok();

            case DeleteStorageQuery.ResultCode.NotFound: 
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            case DeleteStorageQuery.ResultCode.WorkspacesOrIntegrationAttached: 
                return HttpErrors.Storage.WorkspaceOrIntegrationAttached(
                    storageExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(DeleteStorageQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateName(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateStorageNameRequestDto request,
        UpdateStorageNameQuery updateStorageNameQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateStorageNameQuery.Execute(
            storageExternalId, 
            request.Name,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            UpdateStorageNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateStorageNameQuery.ResultCode.NotFound => 
                HttpErrors.Storage.NotFound(
                    storageExternalId),

            UpdateStorageNameQuery.ResultCode.NameNotUnique => 
                HttpErrors.Storage.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateStorageNameQuery),
                resultValueStr: result.ToString())
        };
    }

    // Cloudflare R2 Operations
    private static async Task<Results<Ok<CreateCloudflareR2StorageResponseDto>, BadRequest<HttpError>>> CreateCloudflareR2Storage(
        [FromBody] CreateCloudflareR2StorageRequestDto request,
        CreateCloudflareR2StorageOperation createCloudflareR2StorageOperation,
        CancellationToken cancellationToken)
    {
        var result = await createCloudflareR2StorageOperation.Execute(
            name: request.Name,
            details: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateCloudflareR2StorageOperation.ResultCode.Ok => TypedResults.Ok(
                new CreateCloudflareR2StorageResponseDto(ExternalId: result.StorageExternalId!.Value)),

            CreateCloudflareR2StorageOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            CreateCloudflareR2StorageOperation.ResultCode.NameNotUnique =>
                HttpErrors.Storage.NameNotUnique(
                    request.Name),

            CreateCloudflareR2StorageOperation.ResultCode.InvalidUrl =>
                HttpErrors.Storage.InvalidUrl(
                    request.Url),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateCloudflareR2StorageOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateCloudflareR2StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateCloudflareR2StorageDetailsRequestDto request,
        UpdateCloudflareR2StorageDetailsOperation updateCloudflareR2StorageDetailsOperation,
        CancellationToken cancellationToken)
    {
        var result = await updateCloudflareR2StorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateCloudflareR2StorageDetailsOperation.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateCloudflareR2StorageDetailsOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            UpdateCloudflareR2StorageDetailsOperation.ResultCode.NotFound => 
                HttpErrors.Storage.NotFound(
                    storageExternalId),

            UpdateCloudflareR2StorageDetailsOperation.ResultCode.InvalidUrl => 
                HttpErrors.Storage.InvalidUrl(
                    request.Url),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateCloudflareR2StorageOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok<CreateAwsS3StorageResponseDto>, BadRequest<HttpError>>> CreateAwsS3Storage(
        [FromBody] CreateAwsS3StorageRequestDto request,
        CreateAwsS3StorageOperation createAwsS3StorageOperation,
        CancellationToken cancellationToken)
    {
        var result = await createAwsS3StorageOperation.Execute(
            name: request.Name,
            details: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateAwsS3StorageOperation.ResultCode.Ok => TypedResults.Ok(new CreateAwsS3StorageResponseDto(
                ExternalId: result.StorageExternalId!.Value)),

            CreateAwsS3StorageOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            CreateAwsS3StorageOperation.ResultCode.NameNotUnique => 
                HttpErrors.Storage.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateAwsS3StorageOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAwsS3StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateAwsS3StorageDetailsRequestDto request,
        UpdateAwsS3StorageDetailsOperation updateAwsS3StorageDetailsOperation,
        CancellationToken cancellationToken)
    {
        var result = await updateAwsS3StorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateAwsS3StorageDetailsOperation.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateAwsS3StorageDetailsOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            UpdateAwsS3StorageDetailsOperation.ResultCode.NotFound => 
                HttpErrors.Storage.NotFound(
                    storageExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateAwsS3StorageDetailsOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok<CreateDigitalOceanSpacesStorageResponseDto>, BadRequest<HttpError>>> CreateDigitalOceanSpacesStorage(
        [FromBody] CreateDigitalOceanSpacesStorageRequestDto request,
        CreateDigitalOceanStorageOperation createDigitalOceanStorageOperation,
        CancellationToken cancellationToken)
    {
        var result = await createDigitalOceanStorageOperation.Execute(
            name: request.Name,
            details: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: $"https://{request.Region}.digitaloceanspaces.com"),
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateDigitalOceanStorageOperation.ResultCode.Ok => TypedResults.Ok(new CreateDigitalOceanSpacesStorageResponseDto(
                ExternalId: result.StorageExternalId!.Value)),

            CreateDigitalOceanStorageOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            CreateDigitalOceanStorageOperation.ResultCode.NameNotUnique => 
                HttpErrors.Storage.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateAwsS3StorageOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateDigitalOceanStorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateDigitalOceanSpacesStorageDetailsRequestDto request,
        UpdateDigitalOceanSpacesStorageDetailsOperation updateDigitalOceanSpacesStorageDetailsOperation,
        CancellationToken cancellationToken)
    {
        var result = await updateDigitalOceanSpacesStorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: $"https://{request.Region}.digitaloceanspaces.com"),
            cancellationToken: cancellationToken);

        return result switch
        {
            UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.CouldNotConnect => 
                HttpErrors.Storage.ConnectionFailed(),

            UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.NotFound => 
                HttpErrors.Storage.NotFound(
                    storageExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateAwsS3StorageDetailsOperation),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok<CreateHardDriveStorageResponseDto>, BadRequest<HttpError>>> CreateHardDriveStorage(
        [FromBody] CreateHardDriveStorageRequestDto request,
        CreateHardDriveStorageOperation createHardDriveStorageOperation,
        CancellationToken cancellationToken)
    {
        var result = await createHardDriveStorageOperation.Execute(
            name: request.Name,
            volumePath: request.VolumePath,
            folderPath: request.FolderPath,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateHardDriveStorageOperation.ResultCode.Ok => TypedResults.Ok(new CreateHardDriveStorageResponseDto(
                ExternalId: result.StorageExternalId!.Value)),

            CreateHardDriveStorageOperation.ResultCode.NameNotUnique =>
                HttpErrors.Storage.NameNotUnique(
                    request.Name),

            CreateHardDriveStorageOperation.ResultCode.VolumeNotFound => 
                HttpErrors.Storage.VolumeNotFound(
                    request.VolumePath),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateAwsS3StorageOperation),
                resultValueStr: result.ToString())
        };
    }

    private static GetHardDriveVolumesResponseDto GetHardDriveVolumes(
        GetHardDriveVolumesOperation getHardDriveVolumesOperation)
    {
        var result = getHardDriveVolumesOperation.Execute();

        return new GetHardDriveVolumesResponseDto(
            Items: result
                .Volumes
                .Select(v => new HardDriveVolumeItemDto(
                    Path: v.Path,
                    RestrictedFolderPaths: v.RestrictedFolderPaths))
                .ToArray());
    }
}