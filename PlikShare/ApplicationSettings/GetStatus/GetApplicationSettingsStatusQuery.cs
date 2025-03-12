using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.ApplicationSettings.GetStatus;

public class GetApplicationSettingsStatusQuery
{
    private readonly PlikShareDb _plikShareDb;

    public GetApplicationSettingsStatusQuery(
        PlikShareDb plikShareDb)
    {
        _plikShareDb = plikShareDb;
    }

    public Result Execute()
    {
        using var connection = _plikShareDb.OpenConnection();

        return connection
            .OneRowCmd(
                sql: @"
                    SELECT 
                        (
                            SELECT EXISTS(
                                SELECT ep_id
                                FROM ep_email_providers
                                WHERE ep_is_active = TRUE )
                        ) AS is_email_provider_configured,
                        (
                            SELECT EXISTS(
                                SELECT s_id
                                FROM s_storages
                            )
                        ) AS is_storage_configured
                ",
                readRowFunc: reader => new Result(
                    IsEmailProviderConfigured: reader.GetBoolean(0),
                    IsStorageConfigured: reader.GetBoolean(1)))
            .ExecuteOrThrow();
    }

    public readonly record struct Result(
        bool IsEmailProviderConfigured,
        bool IsStorageConfigured);
}