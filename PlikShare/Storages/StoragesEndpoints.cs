using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Storages.Create;
using PlikShare.Storages.Delete;
using PlikShare.Storages.HardDrive;
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
using PlikShare.Storages.UpdateDetails;
using PlikShare.Storages.S3.DigitalOcean;
using PlikShare.Storages.S3.DigitalOcean.Create;
using PlikShare.Storages.S3.DigitalOcean.Create.Contracts;
using PlikShare.Storages.S3.DigitalOcean.UpdateDetails;
using PlikShare.Storages.S3.DigitalOcean.UpdateDetails.Contracts;
using PlikShare.Storages.UnlockFullEncryption;
using PlikShare.Storages.ResetMasterPassword;
using PlikShare.Storages.ChangeMasterPassword;
using PlikShare.Storages.UpdateName;
using PlikShare.Storages.UpdateName.Contracts;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Queries;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Core.Clock;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
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

        // Full encryption unlock — available for any authenticated user (not admin-only)
        var unlockGroup = app.MapGroup("/api/storages")
            .WithTags("Storages")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal
            });

        unlockGroup.MapPost("/{storageExternalId}/unlock-full-encryption", UnlockFullEncryption)
            .WithName("UnlockFullEncryptionStorage");

        unlockGroup.MapPost("/{storageExternalId}/reset-master-password", ResetMasterPassword)
            .WithName("ResetStorageMasterPassword");

        unlockGroup.MapPost("/{storageExternalId}/change-master-password", ChangeMasterPassword)
            .WithName("ChangeStorageMasterPassword");
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
        CloudflareR2StorageClientFactory cloudflareR2StorageClientFactory,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            factory: cloudflareR2StorageClientFactory,
            input: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            masterPassword: request.MasterPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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
                    new CreateCloudflareR2StorageResponseDto(
                        ExternalId: result.StorageExternalId!.Value,
                        RecoveryCode: result.RecoveryCode));

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            case StorageOperationResultCode.MasterPasswordRequired:
                return HttpErrors.Storage.MasterPasswordRequired();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateCloudflareR2StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateCloudflareR2StorageDetailsRequestDto request,
        CloudflareR2StorageClientFactory cloudflareR2StorageClientFactory,
        UpdateStorageFlow updateStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateStorageFlow.Execute(
            factory: cloudflareR2StorageClientFactory,
            externalId: storageExternalId,
            input: new CloudflareR2DetailsEntity(
                AccessKeyId: request.AccessKeyId,
                SecretAccessKey: request.SecretAccessKey,
                Url: request.Url),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateAwsS3StorageResponseDto>, BadRequest<HttpError>>> CreateAwsS3Storage(
        [FromBody] CreateAwsS3StorageRequestDto request,
        AwsS3StorageClientFactory awsS3StorageClientFactory,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            factory: awsS3StorageClientFactory,
            input: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            name: request.Name,
            encryptionType: request.EncryptionType,
            masterPassword: request.MasterPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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
                    ExternalId: result.StorageExternalId!.Value,
                    RecoveryCode: result.RecoveryCode));

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageOperationResultCode.MasterPasswordRequired:
                return HttpErrors.Storage.MasterPasswordRequired();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateAwsS3StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateAwsS3StorageDetailsRequestDto request,
        AwsS3StorageClientFactory awsS3StorageClientFactory,
        UpdateStorageFlow updateStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateStorageFlow.Execute(
            factory: awsS3StorageClientFactory,
            externalId: storageExternalId,
            input: new AwsS3DetailsEntity(
                AccessKey: request.AccessKey,
                SecretAccessKey: request.SecretAccessKey,
                Region: request.Region),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateDigitalOceanSpacesStorageResponseDto>, BadRequest<HttpError>>> CreateDigitalOceanSpacesStorage(
        [FromBody] CreateDigitalOceanSpacesStorageRequestDto request,
        DigitalOceanStorageClientFactory digitalOceanStorageClientFactory,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var url = $"https://{request.Region}.digitaloceanspaces.com";

        var result = await createStorageFlow.Execute(
            factory: digitalOceanStorageClientFactory,
            input: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            masterPassword: request.MasterPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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
                    ExternalId: result.StorageExternalId!.Value,
                    RecoveryCode: result.RecoveryCode));

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(url);

            case StorageOperationResultCode.MasterPasswordRequired:
                return HttpErrors.Storage.MasterPasswordRequired();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateDigitalOceanStorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateDigitalOceanSpacesStorageDetailsRequestDto request,
        DigitalOceanStorageClientFactory digitalOceanStorageClientFactory,
        UpdateStorageFlow updateStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var url = $"https://{request.Region}.digitaloceanspaces.com";

        var result = await updateStorageFlow.Execute(
            factory: digitalOceanStorageClientFactory,
            externalId: storageExternalId,
            input: new DigitalOceanSpacesDetailsEntity(
                AccessKey: request.AccessKey,
                SecretKey: request.SecretKey,
                Url: url),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok<CreateHardDriveStorageResponseDto>, BadRequest<HttpError>>> CreateHardDriveStorage(
        [FromBody] CreateHardDriveStorageRequestDto request,
        HardDriveStorageClientFactory hardDriveStorageClientFactory,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            factory: hardDriveStorageClientFactory,
            input: new HardDriveStorageClientFactory.Input(
                VolumePath: request.VolumePath,
                FolderPath: request.FolderPath),
            name: request.Name,
            encryptionType: request.EncryptionType,
            masterPassword: request.MasterPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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
                    ExternalId: result.StorageExternalId!.Value,
                    RecoveryCode: result.RecoveryCode));

            case StorageOperationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageOperationResultCode.VolumeNotFound:
                return HttpErrors.Storage.VolumeNotFound(request.VolumePath);

            case StorageOperationResultCode.MasterPasswordRequired:
                return HttpErrors.Storage.MasterPasswordRequired();

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
        BackblazeB2StorageClientFactory backblazeB2StorageClientFactory,
        CreateStorageFlow createStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createStorageFlow.Execute(
            factory: backblazeB2StorageClientFactory,
            input: new BackblazeB2DetailsEntity(
                KeyId: request.KeyId,
                ApplicationKey: request.ApplicationKey,
                Url: request.Url),
            name: request.Name,
            encryptionType: request.EncryptionType,
            masterPassword: request.MasterPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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
                        ExternalId = result.StorageExternalId!.Value,
                        RecoveryCode = result.RecoveryCode
                    });

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NameNotUnique:
                return HttpErrors.Storage.NameNotUnique(request.Name);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            case StorageOperationResultCode.MasterPasswordRequired:
                return HttpErrors.Storage.MasterPasswordRequired();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateBackblazeB2StorageDetails(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UpdateBackblazeB2StorageDetailsRequestDto request,
        BackblazeB2StorageClientFactory backblazeB2StorageClientFactory,
        UpdateStorageFlow updateStorageFlow,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateStorageFlow.Execute(
            factory: backblazeB2StorageClientFactory,
            externalId: storageExternalId,
            input: new BackblazeB2DetailsEntity(
                KeyId: request.KeyId,
                ApplicationKey: request.ApplicationKey,
                Url: request.Url),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case StorageOperationResultCode.Ok:
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

            case StorageOperationResultCode.CouldNotConnect:
                return HttpErrors.Storage.ConnectionFailed();

            case StorageOperationResultCode.NotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case StorageOperationResultCode.InvalidUrl:
                return HttpErrors.Storage.InvalidUrl(request.Url);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateStorageFlow),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> ChangeMasterPassword(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] ChangeMasterPasswordRequestDto request,
        ChangeMasterPasswordOperation changeMasterPasswordOperation,
        GetStorageAuditContextQuery getStorageAuditContextQuery,
        AuditLogService auditLogService,
        IClock clock,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var storageAuditRef = getStorageAuditContextQuery.Execute(storageExternalId);
        var actor = httpContext.GetAuditLogActorContext();

        var result = await changeMasterPasswordOperation.Execute(
            storageExternalId: storageExternalId,
            oldPassword: request.OldPassword,
            newPassword: request.NewPassword,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ChangeMasterPasswordOperation.ResultCode.StorageNotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case ChangeMasterPasswordOperation.ResultCode.EncryptionModeMismatch:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordChangeFailedEntry(
                            actor: actor,
                            storage: storageAuditRef,
                            reason: "encryption-mode-mismatch"),
                        cancellationToken);
                return HttpErrors.Storage.EncryptionModeMismatch();

            case ChangeMasterPasswordOperation.ResultCode.InvalidOldPassword:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordChangeFailedEntry(
                            actor: actor,
                            storage: storageAuditRef,
                            reason: "invalid-old-password"),
                        cancellationToken);
                return HttpErrors.Storage.InvalidMasterPassword();

            case ChangeMasterPasswordOperation.ResultCode.Ok:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordChangedEntry(
                            actor: actor,
                            storage: storageAuditRef),
                        cancellationToken);

                httpContext.Response.Cookies.Append(
                    FullEncryptionSessionCookie.GetCookieName(storageExternalId),
                    result.ProtectedKek!,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        IsEssential = true,
                        Expires = clock.UtcNow.Add(TimeSpan.FromMinutes(30))
                    });

                return TypedResults.Ok();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ChangeMasterPasswordOperation),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, BadRequest<HttpError>, NotFound<HttpError>>> ResetMasterPassword(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] ResetMasterPasswordRequestDto request,
        ResetMasterPasswordOperation resetMasterPasswordOperation,
        GetStorageAuditContextQuery getStorageAuditContextQuery,
        AuditLogService auditLogService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var storageAuditRef = getStorageAuditContextQuery.Execute(storageExternalId);
        var actor = httpContext.GetAuditLogActorContext();

        var resultCode = await resetMasterPasswordOperation.Execute(
            storageExternalId: storageExternalId,
            recoveryCode: request.RecoveryCode,
            newPassword: request.NewPassword,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case ResetMasterPasswordOperation.ResultCode.StorageNotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case ResetMasterPasswordOperation.ResultCode.EncryptionModeMismatch:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordResetFailedEntry(
                            actor: actor,
                            storage: storageAuditRef,
                            reason: "encryption-mode-mismatch"),
                        cancellationToken);
                return HttpErrors.Storage.EncryptionModeMismatch();

            case ResetMasterPasswordOperation.ResultCode.MalformedRecoveryCode:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordResetFailedEntry(
                            actor: actor,
                            storage: storageAuditRef,
                            reason: "malformed-recovery-code"),
                        cancellationToken);
                return HttpErrors.Storage.MalformedRecoveryCode();

            case ResetMasterPasswordOperation.ResultCode.InvalidRecoveryCode:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordResetFailedEntry(
                            actor: actor,
                            storage: storageAuditRef,
                            reason: "invalid-recovery-code"),
                        cancellationToken);
                return HttpErrors.Storage.InvalidRecoveryCode();

            case ResetMasterPasswordOperation.ResultCode.Ok:
                if (storageAuditRef is not null)
                    await auditLogService.Log(
                        Audit.Storage.MasterPasswordResetEntry(
                            actor: actor,
                            storage: storageAuditRef),
                        cancellationToken);
                return TypedResults.Ok();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ResetMasterPasswordOperation),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static Results<Ok, BadRequest<HttpError>, NotFound<HttpError>> UnlockFullEncryption(
        [FromRoute] StorageExtId storageExternalId,
        [FromBody] UnlockFullEncryptionRequestDto request,
        UnlockFullEncryptionOperation unlockFullEncryptionOperation,
        IClock clock,
        HttpContext httpContext)
    {
        var result = unlockFullEncryptionOperation.Execute(
            storageExternalId: storageExternalId,
            masterPassword: request.MasterPassword);

        switch (result.Code)
        {
            case UnlockFullEncryptionOperation.ResultCode.StorageNotFound:
                return HttpErrors.Storage.NotFound(storageExternalId);

            case UnlockFullEncryptionOperation.ResultCode.EncryptionModeMismatch:
                return HttpErrors.Storage.EncryptionModeMismatch();

            case UnlockFullEncryptionOperation.ResultCode.InvalidPassword:
                return HttpErrors.Storage.InvalidMasterPassword();

            case UnlockFullEncryptionOperation.ResultCode.Ok:
                httpContext.Response.Cookies.Append(
                    FullEncryptionSessionCookie.GetCookieName(storageExternalId),
                    result.CookieValue!,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        IsEssential = true,
                        Expires = clock.UtcNow.Add(TimeSpan.FromMinutes(30))
                    });

                return TypedResults.Ok();

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UnlockFullEncryptionOperation),
                    resultValueStr: result.ToString());
        }
    }

}

public record UnlockFullEncryptionRequestDto(
    string MasterPassword);

public record ResetMasterPasswordRequestDto(
    string RecoveryCode,
    string NewPassword);

public record ChangeMasterPasswordRequestDto(
    string OldPassword,
    string NewPassword);