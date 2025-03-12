using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.ArtificialIntelligence.SendFileMessage;

public class GetFilesToIncludeDetailsQuery(PlikShareDb plikShareDb)
{
    public List<FileToInclude> GetFilesToInclude(
        List<FileExtId> externalIds)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                        fi_external_id,
                        fi_name,
                        fi_extension,
                        fi_size_in_bytes,
                        fi_s3_key_secret_part,
                        w_storage_id,
                        w_bucket_name
                     FROM fi_files
                     INNER JOIN w_workspaces
                        ON w_id = fi_workspace_id
                     WHERE fi_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                     """,
                readRowFunc: reader => new FileToInclude
                {
                    ExternalId = reader.GetExtId<FileExtId>(0),
                    Name = reader.GetString(1),
                    Extension = reader.GetString(2),
                    SizeInBytes = reader.GetInt64(3),
                    S3KeySecretPart = reader.GetString(4),
                    StorageId = reader.GetInt32(5),
                    BucketName = reader.GetString(6)
                })
            .WithJsonParameter("$externalIds", externalIds)
            .Execute();
    }
}

public class FileToInclude
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required string S3KeySecretPart { get; init; }
    public required int StorageId { get; init; }
    public required string BucketName { get; init; }
}