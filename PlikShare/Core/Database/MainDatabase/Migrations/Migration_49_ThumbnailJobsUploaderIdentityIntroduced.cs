using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

// GenerateImageThumbnailsJobDefinition replaced TriggeredByUserExternalId (always a user) with a
// generic UploaderIdentityType/UploaderIdentity pair, so that thumbnails generated on upload can
// be attributed to any uploader identity (box visitors, integrations). This rewrites still-queued
// job definitions to the new shape.
public class Migration_49_ThumbnailJobsUploaderIdentityIntroduced : SQLiteMigrationBase
{
    public override string Name => "thumbnail_jobs_uploader_identity_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 10);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.49_thumbnail_jobs_uploader_identity_introduced.sql"
    ];
}
