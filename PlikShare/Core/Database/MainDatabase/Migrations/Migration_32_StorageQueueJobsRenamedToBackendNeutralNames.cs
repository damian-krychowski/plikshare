using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Storages.Entities;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Renames the four storage-touching queue job types from S3-flavoured names to
/// backend-neutral ones, reshaping payloads where the new contract differs:
/// <list type="bullet">
///   <item><c>abort-s3-upload</c> → <c>abort-multipart-upload</c> (payload rebuilt
///   into a polymorphic <see cref="MultipartUploadAbortHandle"/> per backend)</item>
///   <item><c>complete-s3-upload</c> → <c>complete-multipart-upload</c></item>
///   <item><c>delete-s3-file</c> → <c>delete-file</c> (field <c>s3KeySecretPart</c>
///   → <c>keySecretPart</c>)</item>
///   <item><c>bulk-delete-s3-files</c> → <c>bulk-delete-files</c> (field
///   <c>s3FileKeys</c> → <c>fileKeys</c>)</item>
/// </list>
/// </summary>
public class Migration_32_StorageQueueJobsRenamedToBackendNeutralNames : ISQLiteMigration
{
    public string Name => "storage_queue_jobs_renamed_to_backend_neutral_names";
    public DateOnly Date { get; } = new(2026, 5, 3);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        MigrateAbortJobs(connection, transaction);
        RenameCompleteJobs(connection, transaction);
        MigrateDeleteFileJobs(connection, transaction);
        MigrateBulkDeleteFileJobs(connection, transaction);
    }

    private static void MigrateAbortJobs(SqliteConnection connection, SqliteTransaction transaction)
    {
        var pending = connection
            .Cmd(
                sql: """
                     SELECT q_id, q_definition
                     FROM q_queue
                     WHERE q_job_type = 'abort-s3-upload'
                     """,
                readRowFunc: reader => (Id: reader.GetInt64(0), Json: reader.GetString(1)),
                transaction: transaction)
            .Execute();

        foreach (var row in pending)
        {
            var legacy = Json.Deserialize<LegacyAbortDef>(row.Json)
                ?? throw new InvalidOperationException(
                    $"Could not deserialize legacy abort-s3-upload job q_id={row.Id}");

            var storageType = LookupStorageType(connection, transaction, legacy.StorageId);

            if (storageType is null)
            {
                connection
                    .NonQueryCmd(
                        sql: "DELETE FROM q_queue WHERE q_id = $id",
                        transaction: transaction)
                    .WithParameter("$id", row.Id)
                    .Execute();

                continue;
            }

            var handle = BuildHandle(storageType.Value, legacy.S3UploadId, legacy.PartETags);

            var migrated = new MigratedAbortDef(
                StorageId: legacy.StorageId,
                BucketName: legacy.BucketName,
                FileExternalId: legacy.FileExternalId,
                KeySecretPart: legacy.S3KeySecretPart,
                AbortHandle: handle);

            connection
                .NonQueryCmd(
                    sql: """
                         UPDATE q_queue
                         SET q_job_type = 'abort-multipart-upload',
                             q_definition = $def
                         WHERE q_id = $id
                         """,
                    transaction: transaction)
                .WithParameter("$def", Json.Serialize(migrated))
                .WithParameter("$id", row.Id)
                .Execute();
        }
    }

    private static void RenameCompleteJobs(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: """
                     UPDATE q_queue
                     SET q_job_type = 'complete-multipart-upload'
                     WHERE q_job_type = 'complete-s3-upload'
                     """,
                transaction: transaction)
            .Execute();
    }

    private static void MigrateDeleteFileJobs(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: """
                     UPDATE q_queue
                     SET q_job_type = 'delete-file',
                         q_definition = json_set(
                             json_remove(q_definition, '$.s3KeySecretPart'),
                             '$.keySecretPart', json_extract(q_definition, '$.s3KeySecretPart')
                         )
                     WHERE q_job_type = 'delete-s3-file'
                     """,
                transaction: transaction)
            .Execute();
    }

    private static void MigrateBulkDeleteFileJobs(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: """
                     UPDATE q_queue
                     SET q_job_type = 'bulk-delete-files',
                         q_definition = json_set(
                             json_remove(q_definition, '$.s3FileKeys'),
                             '$.fileKeys', json_extract(q_definition, '$.s3FileKeys')
                         )
                     WHERE q_job_type = 'bulk-delete-s3-files'
                     """,
                transaction: transaction)
            .Execute();
    }

    private static StorageType? LookupStorageType(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int storageId)
    {
        var rows = connection
            .Cmd(
                sql: "SELECT s_type FROM s_storages WHERE s_id = $id LIMIT 1",
                readRowFunc: reader => reader.GetString(0),
                transaction: transaction)
            .WithParameter("$id", storageId)
            .Execute();

        if (rows.Count == 0)
            return null;

        return rows[0] switch
        {
            "hard-drive" => StorageType.HardDrive,
            "aws-s3" => StorageType.AwsS3,
            "cloudflare-r2" => StorageType.CloudflareR2,
            "digital-ocean-spaces" => StorageType.DigitalOceanSpaces,
            "backblaze-b2" => StorageType.BackblazeB2,
            "azure-blob" => StorageType.AzureBlob,
            _ => throw new InvalidOperationException(
                $"Unknown s_type '{rows[0]}' for s_id={storageId}; cannot pick abort handle variant.")
        };
    }

    private static MultipartUploadAbortHandle BuildHandle(
        StorageType storageType,
        string uploadId,
        List<string> partETags) =>
        storageType switch
        {
            StorageType.HardDrive => new HardDriveMultipartUploadAbortHandle(partETags),
            StorageType.AzureBlob => new AzureMultipartUploadAbortHandle(),
            StorageType.AwsS3
                or StorageType.CloudflareR2
                or StorageType.DigitalOceanSpaces
                or StorageType.BackblazeB2 => new S3MultipartUploadAbortHandle(uploadId),
            _ => throw new InvalidOperationException(
                $"No abort handle mapping for StorageType '{storageType}'.")
        };

    private sealed record LegacyAbortDef(
        int StorageId,
        string BucketName,
        FileExtId FileExternalId,
        string S3KeySecretPart,
        string S3UploadId,
        long FileSizeInBytes,
        List<string> PartETags);

    private sealed record MigratedAbortDef(
        int StorageId,
        string BucketName,
        FileExtId FileExternalId,
        string KeySecretPart,
        MultipartUploadAbortHandle AbortHandle);
}
