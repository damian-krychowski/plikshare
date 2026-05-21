using Microsoft.Data.Sqlite;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.Trash;

/// <summary>
/// A trashed top-level file read for a purge operation (delete-forever / empty-trash): its db id
/// — handed to <c>PurgeFilesSubQuery</c> — and an audit <see cref="Audit.FileRef"/> describing it
/// so the deleted items can be recorded in the audit log before the rows are wiped.
///
/// The folder path comes from the trash snapshot (<c>fi_original_folder_path</c>) — a trashed
/// file has no live folder. <see cref="Read"/> expects this select column order:
/// fi_id, fi_external_id, fi_name, fi_extension, fi_size_in_bytes, fi_original_folder_path.
/// </summary>
internal readonly record struct TrashedFileRow(int Id, Audit.FileRef FileRef)
{
    public static TrashedFileRow Read(SqliteDataReader reader)
    {
        var pathSegments = reader.GetFromJsonOrNull<List<OriginalFolderPathSegment>>(5);

        return new TrashedFileRow(
            Id: reader.GetInt32(0),
            FileRef: new Audit.FileRef
            {
                ExternalId = new FileExtId(reader.GetString(1)),
                Name = reader.GetEncodedMetadata(2),
                Extension = reader.GetEncodedMetadata(3),
                SizeInBytes = reader.GetInt64(4),
                FolderPath = pathSegments is null or { Count: 0 }
                    ? null
                    : pathSegments.Select(s => new EncodedMetadataValue(s.Name)).ToList()
            });
    }
}
