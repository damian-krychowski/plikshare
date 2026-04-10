using System.Threading.Channels;
using PlikShare.AuditLog.Queries;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.AuditLog;

public class AuditLogService(
    AuditLogChannel channel,
    GetFileAuditContextQuery getFileAuditContextQuery,
    GetFolderAuditContextQuery getFolderAuditContextQuery,
    GetFileUploadAuditContextQuery getFileUploadAuditContextQuery,
    GetStorageAuditContextQuery getStorageAuditContextQuery)
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<AuditLogService>();

    public async ValueTask LogWithStorageContext(
        StorageExtId storageExternalId,
        Func<AuditLogDetails.StorageRef, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var storageRef = getStorageAuditContextQuery.Execute(
            storageExternalId: storageExternalId);

        if (storageRef is null)
        {
            Logger.Warning(
                "Could not resolve storage audit context for Storage '{StorageExternalId}', skipping audit log entry",
                storageExternalId);

            return;
        }

        await Log(buildEntry(storageRef), cancellationToken);
    }

    public async ValueTask LogWithFolderContext(
        FolderExtId? folderExternalId,
        Func<AuditLogDetails.FolderRef?, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var folderRef = folderExternalId is not null
            ? getFolderAuditContextQuery.Execute(folderExternalId.Value)
            : null;

        await Log(buildEntry(folderRef), cancellationToken);
    }

    public async ValueTask LogWithFolderContext(
        FolderExtId folderExternalId,
        Func<AuditLogDetails.FolderRef, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var folderRef = getFolderAuditContextQuery.Execute(
            folderExternalId: folderExternalId);

        if (folderRef is null)
        {
            Logger.Warning(
                "Could not resolve folder audit context for Folder '{FolderExternalId}', skipping audit log entry",
                folderExternalId);

            return;
        }

        await Log(buildEntry(folderRef), cancellationToken);
    }

    public async ValueTask LogWithFileContext(
        FileExtId fileExternalId,
        Func<AuditLogDetails.FileRef, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileRef = getFileAuditContextQuery.Execute(
            fileExternalId: fileExternalId);

        if (fileRef is null)
        {
            Logger.Warning(
                "Could not resolve file audit context for File '{FileExternalId}', skipping audit log entry",
                fileExternalId);

            return;
        }

        await Log(buildEntry(fileRef), cancellationToken);
    }

    public async ValueTask LogWithFileContexts(
        List<FileExtId> fileExternalIds,
        Func<Dictionary<FileExtId, AuditLogDetails.FileRef>, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileRefs = getFileAuditContextQuery.ExecuteMany(
            fileExternalIds: fileExternalIds);

        await Log(buildEntry(fileRefs), cancellationToken);
    }


    public BulkItemsContext GetBulkItemsContext(
        List<FolderExtId> folderExternalIds,
        List<FileExtId> fileExternalIds,
        List<FileUploadExtId> fileUploadExternalIds)
    {
        var folders = getFolderAuditContextQuery.ExecuteMany(folderExternalIds);
        var files = getFileAuditContextQuery.ExecuteMany(fileExternalIds);
        var fileUploads = getFileUploadAuditContextQuery.ExecuteMany(fileUploadExternalIds);

        return new BulkItemsContext(
            Folders: folders.Values.ToList(),
            Files: files.Values.ToList(),
            FileUploads: fileUploads.Values.ToList());
    }

    public record BulkItemsContext(
        List<AuditLogDetails.FolderRef> Folders,
        List<AuditLogDetails.FileRef> Files,
        List<AuditLogDetails.FileUploadRef> FileUploads);

    public ItemsMovedContext GetItemsMovedContext(
        FolderExtId? destinationFolderExternalId,
        List<FolderExtId> folderExternalIds,
        List<FileExtId> fileExternalIds,
        List<FileUploadExtId> fileUploadExternalIds)
    {
        var destinationFolder = destinationFolderExternalId is not null
            ? getFolderAuditContextQuery.Execute(destinationFolderExternalId.Value)
            : null;

        var bulkItems = GetBulkItemsContext(
            folderExternalIds, 
            fileExternalIds, 
            fileUploadExternalIds);

        return new ItemsMovedContext(
            DestinationFolder: destinationFolder,
            Folders: bulkItems.Folders,
            Files: bulkItems.Files,
            FileUploads: bulkItems.FileUploads);
    }

    public record ItemsMovedContext(
        AuditLogDetails.FolderRef? DestinationFolder,
        List<AuditLogDetails.FolderRef> Folders,
        List<AuditLogDetails.FileRef> Files,
        List<AuditLogDetails.FileUploadRef> FileUploads);

    public async ValueTask Log(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await channel.WriteAsync(
                entry,
                cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            Logger.Warning(
                "Audit log channel is disposed, dropping entry: {EventType}",
                entry.EventType);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug(
                "Audit log write cancelled for entry: {EventType}",
                entry.EventType);
        }
        catch (ChannelClosedException)
        {
            Logger.Warning(
                "Audit log channel is closed, dropping entry: {EventType}",
                entry.EventType);
        }
    }
}
