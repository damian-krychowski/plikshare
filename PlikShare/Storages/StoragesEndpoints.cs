using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Storages.Create;
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
using PlikShare.Storages.S3.BackblazeB2;
using PlikShare.Storages.S3.BackblazeB2.Create;
using PlikShare.Storages.S3.BackblazeB2.Create.Contracts;
using PlikShare.Storages.S3.BackblazeB2.UpdateDetails;
using PlikShare.Storages.S3.BackblazeB2.UpdateDetails.Contracts;
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
using PlikShare.AuditLog;
using PlikShare.Storages.Entities;
using Audit = PlikShare.AuditLog.Details.Audit;

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

        // Backblaze B2
        group.MapPost("/backblaze-b2", CreateBackblazeB2Storage)
            .WithName("CreateBackblazeB2Storage");

        group.MapPatch("/backblaze-b2/{storageExternalId}/details", UpdateBackblazeB2StorageDetails)
            .WithName("UpdateBackblazeB2StorageDetails");
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
        HttpContext httpContext,
        AuditLogService auditLogService,
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

                await auditLogService.Log(
                    Audit.Storage.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = result.Name!,
                            Type = result.Type!
                        }),
                    cancellationToken);

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
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateStorageNameQuery.Execute(
            storageExternalId,
            request.Name,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateStorageNameQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = request.Name,
                            Type = result.Type!
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateStorageNameQuery.ResultCode.NotFound:
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            case UpdateStorageNameQuery.ResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(
                    request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateStorageNameQuery),
                    resultValueStr: result.ToString());
        }
    }

    // Cloudflare R2 Operations
    private static async Task<Results<Ok<CreateCloudflareR2StorageResponseDto>, BadRequest<HttpError>>> CreateCloudflareR2Storage(
        [FromBody] CreateCloudflareR2StorageRequestDto request,
        CloudflareR2StorageCreator cloudflareR2StorageCreator,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            creator: cloudflareR2StorageCreator,
            input: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageCreationResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = result.StorageExternalId!.Value,
                            Name = request.Name,
                            Type = StorageType.CloudflareR2
                        }),
                    cancellationToken);

                return TypedResults.Ok(
                    new CreateCloudflareR2StorageResponseDto(ExternalId: result.StorageExternalId!.Value));

            case StorageCreationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageCreationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageCreationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateCloudflareR2StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateCloudflareR2StorageDetailsRequestDto request,
        UpdateCloudflareR2StorageDetailsOperation updateCloudflareR2StorageDetailsOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateCloudflareR2StorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateCloudflareR2StorageDetailsOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.DetailsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = result.Name!,
                            Type = StorageType.CloudflareR2
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateCloudflareR2StorageDetailsOperation.ResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case UpdateCloudflareR2StorageDetailsOperation.ResultCode.NotFound:
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            case UpdateCloudflareR2StorageDetailsOperation.ResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(
                    request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateCloudflareR2StorageDetailsOperation),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateAwsS3StorageResponseDto>, BadRequest<HttpError>>> CreateAwsS3Storage(
        [FromBody] CreateAwsS3StorageRequestDto request,
        AwsS3StorageCreator awsS3StorageCreator,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            creator: awsS3StorageCreator,
            input: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            name: request.Name,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageCreationResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = result.StorageExternalId!.Value,
                            Name = request.Name,
                            Type = StorageType.AwsS3
                        }),
                    cancellationToken);

                return TypedResults.Ok(new CreateAwsS3StorageResponseDto(
                    ExternalId: result.StorageExternalId!.Value));

            case StorageCreationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageCreationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAwsS3StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateAwsS3StorageDetailsRequestDto request,
        UpdateAwsS3StorageDetailsOperation updateAwsS3StorageDetailsOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateAwsS3StorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateAwsS3StorageDetailsOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.DetailsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = result.Name!,
                            Type = StorageType.AwsS3
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateAwsS3StorageDetailsOperation.ResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case UpdateAwsS3StorageDetailsOperation.ResultCode.NotFound:
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateAwsS3StorageDetailsOperation),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateDigitalOceanSpacesStorageResponseDto>, BadRequest<HttpError>>> CreateDigitalOceanSpacesStorage(
        [FromBody] CreateDigitalOceanSpacesStorageRequestDto request,
        DigitalOceanStorageCreator digitalOceanStorageCreator,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var url = $"https://{request.Region}.digitaloceanspaces.com";

        var result = await createStorageFlow.Execute(
            creator: digitalOceanStorageCreator,
            input: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageCreationResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = result.StorageExternalId!.Value,
                            Name = request.Name,
                            Type = StorageType.DigitalOceanSpaces
                        }),
                    cancellationToken);

                return TypedResults.Ok(new CreateDigitalOceanSpacesStorageResponseDto(
                    ExternalId: result.StorageExternalId!.Value));

            case StorageCreationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageCreationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageCreationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateDigitalOceanStorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateDigitalOceanSpacesStorageDetailsRequestDto request,
        UpdateDigitalOceanSpacesStorageDetailsOperation updateDigitalOceanSpacesStorageDetailsOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateDigitalOceanSpacesStorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: $"https://{request.Region}.digitaloceanspaces.com"),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.DetailsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = result.Name!,
                            Type = StorageType.DigitalOceanSpaces
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case UpdateDigitalOceanSpacesStorageDetailsOperation.ResultCode.NotFound:
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateAwsS3StorageDetailsOperation),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateHardDriveStorageResponseDto>, BadRequest<HttpError>>> CreateHardDriveStorage(
        [FromBody] CreateHardDriveStorageRequestDto request,
        HardDriveStorageCreator hardDriveStorageCreator,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            creator: hardDriveStorageCreator,
            input: new HardDriveStorageCreator.Input(
                VolumePath: request.VolumePath,
                FolderPath: request.FolderPath),
            name: request.Name,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageCreationResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = result.StorageExternalId!.Value,
                            Name = request.Name,
                            Type = StorageType.HardDrive
                        }),
                    cancellationToken);

                return TypedResults.Ok(new CreateHardDriveStorageResponseDto(
                    ExternalId: result.StorageExternalId!.Value));

            case StorageCreationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageCreationResultCode.VolumeNotFound:
                return HttpErrors.Storage.VolumeNotFound(request.VolumePath);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
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

    // Backblaze B2 Operations
    private static async Task<Results<Ok<CreateBackblazeB2StorageResponseDto>, BadRequest<HttpError>>> CreateBackblazeB2Storage(
        [FromBody] CreateBackblazeB2StorageRequestDto request,
        BackblazeB2StorageCreator backblazeB2StorageCreator,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            creator: backblazeB2StorageCreator,
            input: new BackblazeB2DetailsEntity(
                KeyId: request.KeyId,
                ApplicationKey: request.ApplicationKey,
                Url: request.Url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageCreationResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = result.StorageExternalId!.Value,
                            Name = request.Name,
                            Type = StorageType.BackblazeB2
                        }),
                    cancellationToken);

                return TypedResults.Ok(
                    new CreateBackblazeB2StorageResponseDto
                    {
                        ExternalId = result.StorageExternalId!.Value
                    });

            case StorageCreationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageCreationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageCreationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateBackblazeB2StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateBackblazeB2StorageDetailsRequestDto request,
        UpdateBackblazeB2StorageDetailsOperation updateBackblazeB2StorageDetailsOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateBackblazeB2StorageDetailsOperation.Execute(
            externalId: storageExternalId,
            newDetails: new BackblazeB2DetailsEntity(
                KeyId: request.KeyId,
                ApplicationKey: request.ApplicationKey,
                Url: request.Url),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateBackblazeB2StorageDetailsOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Storage.DetailsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: new Audit.StorageRef
                        {
                            ExternalId = storageExternalId,
                            Name = result.Name!,
                            Type = StorageType.BackblazeB2
                        }),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateBackblazeB2StorageDetailsOperation.ResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case UpdateBackblazeB2StorageDetailsOperation.ResultCode.NotFound:
                return HttpErrors.Storage.NotFound(
                    storageExternalId);

            case UpdateBackblazeB2StorageDetailsOperation.ResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(
                    request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBackblazeB2StorageDetailsOperation),
                    resultValueStr: result.ToString());
        }
    }
}