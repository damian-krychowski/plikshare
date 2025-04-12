using Microsoft.AspNetCore.Http.HttpResults;
using PlikShare.BoxExternalAccess.Authorization;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.BoxExternalAccess.Handler.GetContent;
using PlikShare.BoxExternalAccess.Handler.GetHtml;
using PlikShare.BulkDelete;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Utils;
using PlikShare.Files.BulkDownload;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.Comment.CreateComment;
using PlikShare.Files.Preview.GetZipContentDownloadLink;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Files.Rename;
using PlikShare.Folders.Create;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Folders.Rename;
using PlikShare.Uploads.CompleteFileUpload;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Uploads.FilePartUpload.Initiate;
using PlikShare.Uploads.GetDetails;
using PlikShare.Uploads.GetDetails.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.CountSelectedItems;
using PlikShare.Workspaces.CountSelectedItems.Contracts;
using PlikShare.Workspaces.SearchFilesTree;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.BoxExternalAccess.Handler;

public class BoxExternalAccessHandler(
    GetBoxContentHandler getBoxContentHandler,
    GetFileDownloadLinkOperation getFileDownloadLinkOperation,
    GetBulkDownloadLinkOperation getBulkDownloadLinkOperation,
    UpdateFileNameQuery updateFileNameQuery,
    UpdateFolderNameQuery updateFolderNameQuery,
    MoveItemsToFolderQuery moveItemsToFolderQuery,
    GetUploadsListQuery getUploadsListQuery,
    BulkInitiateFileUploadOperation bulkInitiateFileUploadOperation,
    ConvertFileUploadToFileOperation convertFileUploadToFileOperation,
    InitiateFilePartUploadOperation initiateFilePartUploadOperation,
    CompleteFilePartUploadQuery completeFilePartUploadQuery,
    GetFileUploadDetailsQuery getFileUploadDetailsQuery,
    BulkDeleteQuery bulkDeleteQuery,
    GetBoxHtmlQuery getBoxHtmlQuery,
    GetZipFileDetailsOperation getZipFileDetailsOperation,
    GetZipContentDownloadLinkOperation getZipContentDownloadLinkOperation,
    GetOrCreateFolderQuery getOrCreateFolderQuery,
    CreateFolderQuery createFolderQuery,
    CountSelectedItemsQuery countSelectedItemsQuery,
    SearchFilesTreeQuery searchFilesTreeQuery,
    WorkspaceCache workspaceCache)
{
    public Results<Ok<GetBoxHtmlResponseDto>, NotFound<HttpError>> GetBoxHtml(
        BoxAccess boxAccess)
    {
        var result = getBoxHtmlQuery.Execute(
            box: boxAccess.Box);
        
        return result.Code switch
        {
            GetBoxHtmlQuery.ResultCode.Ok => 
                TypedResults.Ok(
                    new GetBoxHtmlResponseDto(
                        HeaderHtml: result.Html!.Header,
                        FooterHtml: result.Html.Footer)),

            GetBoxHtmlQuery.ResultCode.BoxNotFound =>
                HttpErrors.Box.NotFound(
                    boxAccess.Box.ExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetBoxHtmlQuery),
                resultValueStr: result.Code.ToString())
        };
    }
    
    public Results<Ok<GetBoxDetailsAndContentResponseDto>, NotFound<HttpError>> GetBoxDetailsContent(
        HttpContext httpContext,
        BoxAccess boxAccess,
        FolderExtId? folderExternalId)
    {
        return getBoxContentHandler.GetDetailsAndContent(
            httpContext: httpContext,
            boxAccess: boxAccess,
            folderExternalId: folderExternalId);
    }
    
    public Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetBoxContent(
        HttpContext httpContext,
        BoxAccess boxAccess,
        FolderExtId? folderExternalId)
    {
        return getBoxContentHandler.GetContent(
            httpContext: httpContext,
            boxAccess: boxAccess,
            folderExternalId: folderExternalId);
    }
    
    public async Task<Results<Ok<GetBoxFileDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>> GetFileDownloadLink(
        FileExtId fileExternalId,
        string contentDisposition,
        BoxAccess boxAccess,
        int? boxLinkId,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken)
    {
        if (!ContentDispositionHelper.TryParse(contentDisposition, out var contentDispositionType))
            return HttpErrors.File.InvalidContentDisposition(contentDisposition);

        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var result = await getFileDownloadLinkOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            fileExternalId: fileExternalId,
            contentDisposition: contentDispositionType,
            boxFolderId: boxAccess.Box.Folder.Id,
            boxLinkId: boxLinkId,
            userIdentity: boxAccess.UserIdentity,
            enforceInternalPassThrough: enforceInternalPassThrough,
            cancellationToken: cancellationToken);
        
        return result.Code switch
        {
            GetFileDownloadLinkOperation.ResultCode.Ok => 
                TypedResults.Ok(
                    new GetBoxFileDownloadLinkResponseDto(
                        result.DownloadPreSignedUrl!)),

            GetFileDownloadLinkOperation.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    public Results<Ok<GetBulkDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult> GetBulkDownloadLink(
        GetBulkDownloadLinkRequestDto request,
        BoxAccess boxAccess,
        int? boxLinkId)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var result = getBulkDownloadLinkOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            request: request,
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder.Id,
            boxLinkId: boxLinkId);

        return result.Code switch
        {
            GetBulkDownloadLinkOperation.ResultCode.Ok => 
                TypedResults.Ok(new GetBulkDownloadLinkResponseDto
                {
                    PreSignedUrl = result.PreSignedUrl!
                }),

            GetBulkDownloadLinkOperation.ResultCode.FilesNotFound => 
                HttpErrors.File.SomeFilesNotFound(
                    result.NotFoundFileExternalIds ?? []),

            GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound =>
                HttpErrors.Folder.NotFound(
                    result.NotFoundFolderExternalIds ?? []),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    public async Task<Results<Ok, NotFound<HttpError>, StatusCodeHttpResult>> UpdateFileName(
        FileExtId fileExternalId,
        UpdateBoxFileNameRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var resultCode = await updateFileNameQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            fileExternalId: fileExternalId,
            name: request.Name,
            boxFolderId: boxAccess.Box.Folder.Id,
            userIdentity: boxAccess.UserIdentity,
            isRenameAllowedByBoxPermissions: boxAccess.Permissions is { AllowList: true, AllowRenameFile: true },
            cancellationToken: cancellationToken);
        
        return resultCode switch
        {
            UpdateFileNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),
            
            UpdateFileNameQuery.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateFileNameQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    public async ValueTask<Results<Ok<GetZipFileDetailsResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, JsonHttpResult<HttpError>, StatusCodeHttpResult>> GetZipFilePreviewDetails(
        HttpContext httpContext,
        FileExtId fileExternalId,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var result = await getZipFileDetailsOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            fileExternalId: fileExternalId,
            boxFolderId: boxAccess.Box.Folder.Id,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipFileDetailsOperation.ResultCode.Ok => 
                TypedResults.Ok(new GetZipFileDetailsResponseDto
                {
                    Items = result
                        .Entries!
                        .Where(e => e.UncompressedSize > 0)
                        .Select(e => new GetZipFileDetailsItemDto
                        {
                            FilePath = e.FileName,
                            CompressedSizeInBytes = e.CompressedSize,
                            SizeInBytes = e.UncompressedSize,
                            CompressionMethod = e.CompressionMethod,
                            FileNameLength = e.FileNameLength,
                            IndexInArchive = e.IndexInArchive,
                            OffsetToLocalFileHeader = e.OffsetToLocalFileHeader
                        })
                        .ToList()
                }),

            GetZipFileDetailsOperation.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            GetZipFileDetailsOperation.ResultCode.WrongFileExtension => 
                HttpErrors.File.WrongFileExtension(
                    fileExternalId,
                    ".zip"),

            _ => throw new UnexpectedOperationResultException(operationName: nameof(CreateFileCommentQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    public Results<Ok<GetZipContentDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult> GetZipContentDownloadLink(
        FileExtId fileExternalId,
        GetZipContentDownloadLinkRequestDto request,
        BoxAccess boxAccess,
        int? boxLinkId,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var result = getZipContentDownloadLinkOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            fileExternalId: fileExternalId,
            zipFile: request.Item,
            contentDisposition: request.ContentDisposition,
            boxFolderId: boxAccess.Box.Folder.Id,
            boxLinkId: boxLinkId,
            userIdentity: boxAccess.UserIdentity,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipContentDownloadLinkOperation.ResultCode.Ok => 
                TypedResults.Ok(new GetZipContentDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!)),

            GetZipContentDownloadLinkOperation.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId)
            ,

            GetZipContentDownloadLinkOperation.ResultCode.WrongFileExtension =>
                HttpErrors.File.WrongFileExtension(
                    fileExternalId,
                    ".zip"),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    public async Task<Results<Ok<BulkDeleteResponseDto>, StatusCodeHttpResult>>  BulkDelete(
        FileExtId[] fileExternalIds,
        FolderExtId[] folderExternalIds,
        FileUploadExtId[] fileUploadExternalIds,
        BoxAccess boxAccess,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        if(folderExternalIds.Length > 0 && boxAccess.Permissions is not { AllowList: true, AllowDeleteFolder: true })
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        
        var response = await bulkDeleteQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            fileExternalIds: fileExternalIds,
            folderExternalIds: folderExternalIds,
            fileUploadExternalIds: fileUploadExternalIds,
            boxFolderId: boxAccess.Box.Folder.Id,
            userIdentity: boxAccess.UserIdentity,
            isFileDeleteAllowedByBoxPermissions: boxAccess.Permissions is {AllowList: true, AllowDeleteFile: true},
            correlationId: correlationId,
            cancellationToken: cancellationToken);
        
        return TypedResults.Ok(response);
    }

    public async Task<Results<Ok<BulkCreateFolderResponseDto>, BadRequest<HttpError>, NotFound<HttpError>, StatusCodeHttpResult>> BulkCreateFolders(
        BulkCreateFolderRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var parentFolderExternalId = request.ParentExternalId is null
            ? boxAccess.Box.Folder.ExternalId
            : FolderExtId.Parse(request.ParentExternalId);

        var result = await getOrCreateFolderQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            parentFolderExternalId: parentFolderExternalId,
            boxFolderId: boxAccess.Box.Folder.Id,
            userIdentity: boxAccess.UserIdentity,
            folderTreeItems: request.FolderTrees ?? [],
            ensureUniqueNames: request.EnsureUniqueNames,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetOrCreateFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(
                    result.Response),

            GetOrCreateFolderQuery.ResultCode.ParentFolderNotFound => 
                HttpErrors.Folder.NotFound(
                    parentFolderExternalId),

            GetOrCreateFolderQuery.ResultCode.DuplicatedNamesFound => 
                HttpErrors.Folder.DuplicatedNamesOnInput(
                    result.TemporaryIdsWithDuplications ?? []),

            GetOrCreateFolderQuery.ResultCode.DuplicatedTemporaryIds => 
                HttpErrors.Folder.DuplicatedTemporaryIds(
                    result.TemporaryIdsWithDuplications ?? []),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetOrCreateFolderQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    public async Task<Results<Ok<CreateFolderResponseDto>, NotFound<HttpError>, StatusCodeHttpResult>> CreateFolder(
        CreateFolderRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var folderExternalId = request.ParentExternalId ?? boxAccess.Box.Folder.ExternalId;

        var result = await createFolderQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            name: request.Name,
            folderExternalId: request.ExternalId,
            parentFolderExternalId: folderExternalId,
            boxFolderId: boxAccess.Box.Folder.Id,
            userIdentity: boxAccess.UserIdentity,
            cancellationToken: cancellationToken);
        
        return result switch
        {
            CreateFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(new CreateFolderResponseDto
                {
                    ExternalId = request.ExternalId
                }),

            CreateFolderQuery.ResultCode.ParentFolderNotFound => 
                HttpErrors.Folder.NotFound(
                    folderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFolderQuery),
                resultValueStr: result.ToString())
        };
    }

    
    public async Task<Results<Ok, NotFound<HttpError>, StatusCodeHttpResult>> UpdateFolderName(
        FolderExtId folderExternalId,
        UpdateBoxFolderNameRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            folderExternalId: folderExternalId,
            name: request.Name,
            boxFolderId: boxAccess.Box.Folder.Id,
            userIdentity: boxAccess.UserIdentity,
            isOperationAllowedByBoxPermissions: boxAccess.Permissions is {AllowList: true, AllowRenameFolder: true},
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateFolderNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),
            
            UpdateFolderNameQuery.ResultCode.FolderNotFound => 
                HttpErrors.Folder.NotFound(
                    folderExternalId),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateFolderNameQuery),
                resultValueStr: resultCode.ToString())
        };
    }
    
    public async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>>  MoveItemsToFolder(
        MoveBoxItemsToFolderRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var destinationFolderExternalId = request.DestinationFolderExternalId ?? boxAccess.Box.Folder.ExternalId;

        var resultCode = await moveItemsToFolderQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            folderExternalIds: request.FolderExternalIds,
            fileExternalIds: request.FileExternalIds,
            fileUploadExternalIds: request.FileUploadExternalIds,
            destinationFolderExternalId: destinationFolderExternalId,
            boxFolderId: boxAccess.Box.Folder.Id,
            cancellationToken: cancellationToken);
        
        return resultCode switch
        {
            MoveItemsToFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(),
            
            MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound => 
                HttpErrors.Folder.NotFound(
                    destinationFolderExternalId),
            
            MoveItemsToFolderQuery.ResultCode.FoldersNotFound => 
                HttpErrors.Folder.SomeFolderNotFound(),
            
            MoveItemsToFolderQuery.ResultCode.FilesNotFound => 
                HttpErrors.Folder.SomeFileNotFound(),
            
            MoveItemsToFolderQuery.ResultCode.UploadsNotFound => 
                HttpErrors.Folder.SomeFileUploadNotFound(),
            
            MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder => 
                HttpErrors.Folder.CannotMoveFoldersToOwnSubfolders(),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(MoveItemsToFolderQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    public async Task<Results<Ok<BulkInitiateFileUploadResponseDto>, NotFound<HttpError>, StatusCodeHttpResult, BadRequest<HttpError>>> BulkInitiateFileUpload(
        BulkInitiateFileUploadRequestDto request,
        BoxAccess boxAccess,
        int? boxLinkId,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        if (!boxAccess.Box.Workspace.IsBucketCreated)
            return HttpErrors.Workspace.BucketNotReady();

        for (var i = 0; i < request.Items.Length; i++)
        {
            request.Items[i].FolderExternalId ??= boxAccess.Box.Folder.ExternalId.Value;
        }

        var result = await bulkInitiateFileUploadOperation.Execute(
            workspace: boxAccess.Box.Workspace, 
            fileDetailsList: request.Items,
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder.Id,
            boxLinkId: boxLinkId,
            cancellationToken: cancellationToken);

        await workspaceCache.InvalidateEntry(
            workspaceId: boxAccess.Box.Workspace.Id,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            BulkInitiateFileUploadOperation.ResultCode.Ok => 
                TypedResults.Ok(
                    result.Response),

            BulkInitiateFileUploadOperation.ResultCode.FoldersNotFound => 
                HttpErrors.Folder.NotFound(
                    result.MissingFolders),

            BulkInitiateFileUploadOperation.ResultCode.NotEnoughSpace =>
                HttpErrors.Box.NotEnoughSpace(),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(BulkInitiateFileUploadOperation),
                resultValueStr: result.Code.ToString())
        };
    }
    public Results<Ok<GetFileUploadDetailsResponseDto>, NotFound<HttpError>> GetFileUploadDetails(
        FileUploadExtId fileUploadExternalId,
        BoxAccess boxAccess)
    {
        //probably should have check for boxId, however i am checking for user identity, and this call returns
        //only info about upload parts cound and already uploaded parts, and user need to know it externalId
        //so i think it is ok not to check it
        var result = getFileUploadDetailsQuery.Execute(
            uploadExternalId: fileUploadExternalId,
            workspace: boxAccess.Box.Workspace,
            userIdentity: boxAccess.UserIdentity);

        return result.Code switch
        {
            GetFileUploadDetailsQuery.ResultCode.Ok =>
                TypedResults.Ok(new GetFileUploadDetailsResponseDto
                {
                    Algorithm = result.Details!.Algorithm,
                    ExpectedPartsCount = result.Details.ExpectedPartsCount,
                    AlreadyUploadedPartNumbers = result.Details.AlreadyUploadedPartNumbers
                }),

            GetFileUploadDetailsQuery.ResultCode.NotFound => 
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileUploadDetailsQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    public async Task<Results<Ok<InitiateBoxFilePartUploadResponseDto>, NotFound<HttpError>>> InitiateFilePartUpload(
        FileUploadExtId fileUploadExternalId, 
        int partNumber,
        BoxAccess boxAccess,
        int? boxLinkId,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken)
    {
        var result = await initiateFilePartUploadOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            boxLinkId: boxLinkId,
            userIdentity: boxAccess.UserIdentity,
            enforceInternalPassThrough: enforceInternalPassThrough,
            cancellationToken: cancellationToken);
        
        return result.Code switch
        {
            InitiateFilePartUploadOperation.ResultCode.FilePartUploadInitiated =>
                TypedResults.Ok(new InitiateBoxFilePartUploadResponseDto(
                    UploadPreSignedUrl: result.Details!.UploadPreSignedUrl,
                    StartsAtByte: result.Details.StartsAtByte,
                    EndsAtByte: result.Details.EndsAtByte,
                    IsCompleteFilePartUploadCallbackRequired: result.Details.IsCompleteFilePartUploadCallbackRequired)),
    
            InitiateFilePartUploadOperation.ResultCode.FileUploadNotFound =>
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),
    
            InitiateFilePartUploadOperation.ResultCode.FileUploadPartNumberNotAllowed =>
                HttpErrors.Upload.PartNotAllowed(
                    fileUploadExternalId,
                    partNumber),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(InitiateFilePartUploadOperation),
                resultValueStr: result.Code.ToString())
        };
    }
    
    public async ValueTask<Results<Ok, NotFound<HttpError>>> CompleteFilePartUpload(
        FileUploadExtId fileUploadExternalId, 
        int partNumber,
        CompleteBoxFilePartUploadRequestDto request,
        BoxAccess boxAccess,
        CancellationToken cancellationToken)
    {
        var result = await completeFilePartUploadQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            eTag: request.ETag,
            userIdentity: boxAccess.UserIdentity,
            cancellationToken: cancellationToken);
        
        return result switch
        {
            CompleteFilePartUploadQuery.ResultCode.Ok =>
                TypedResults.Ok(),
    
            CompleteFilePartUploadQuery.ResultCode.FileUploadNotFound =>
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),
    
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CompleteFilePartUploadQuery),
                resultValueStr: result.ToString())
        };
    }
    
    public async ValueTask<Results<Ok<CompleteBoxFileUploadResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> CompleteUpload(
        FileUploadExtId fileUploadExternalId,
        BoxAccess boxAccess,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var result = await convertFileUploadToFileOperation.Execute(
            workspace: boxAccess.Box.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            userIdentity:boxAccess.UserIdentity,
            correlationId: correlationId,
            cancellationToken: cancellationToken);
    
        return result.Code switch
        {
            ConvertFileUploadToFileOperation.ResultCode.Ok => TypedResults.Ok(
                new CompleteBoxFileUploadResponseDto(
                    FileExternalId: result.FileExternalId)),

            ConvertFileUploadToFileOperation.ResultCode.FileUploadNotFound =>
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),

            ConvertFileUploadToFileOperation.ResultCode.FileUploadNotYetCompleted =>
                HttpErrors.Upload.NotCompleted(
                    fileUploadExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(ConvertFileUploadToFileOperation),
                resultValueStr: result.Code.ToString())
        };
    }
    
    public GetUploadsListResponseDto ListUploads(
        HttpContext httpContext,
        BoxAccess boxAccess)
    {
        var response = getUploadsListQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder!.Id);

        return response;
    }

    public Results<Ok<CountSelectedItemsResponseDto>, StatusCodeHttpResult>  CountSelectedItems(
        CountSelectedItemsRequestDto request,
        BoxAccess boxAccess)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var response = countSelectedItemsQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            request: request,
            boxFolderId: boxAccess.Box.Folder.Id);

        return TypedResults.Ok(response);
    }

    public Results<Ok<SearchFilesTreeResponseDto>, StatusCodeHttpResult> SearchFilesTree(
        SearchFilesTreeRequestDto request,
        BoxAccess boxAccess)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var response = searchFilesTreeQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            request: request,
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder.Id);

        return TypedResults.Ok(response);
    }
}