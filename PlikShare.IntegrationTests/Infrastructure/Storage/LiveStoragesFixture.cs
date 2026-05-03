using PlikShare.Core.SQLite;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Serilog;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Collection-scoped fixture that owns one shared <see cref="LiveStorageSetup"/> per
/// <c>(provider, encryption)</c> combination. Tests fetch their setup via
/// <see cref="GetOrCreate"/>; the first call materialises the storage + workspace,
/// subsequent calls reuse it. All setups are cleaned up in <see cref="DisposeAsync"/>
/// once the test collection finishes — far cheaper than per-test create/delete
/// against live S3 endpoints.
/// </summary>
public sealed class LiveStoragesFixture : IAsyncLifetime
{
    private readonly List<LiveStorageSetup> _setups = [];
    private readonly SemaphoreSlim _lock = new(initialCount: 1, maxCount: 1);
    private HostFixture? _hostFixture;
    private Api? _api;
    private Cookie? _fullEncryptionCookie;

    private Api Api => _api ??= new Api(
        _hostFixture!.FlurlClient,
        _hostFixture!.AppUrl);

    public async Task<LiveStorageSetup> GetOrCreate(
        TestFixture testFixture,
        StorageType provider,
        StorageEncryptionType encryption,
        TestFixture.AppSignedInUser user)
    {
        await _lock.WaitAsync();
        try
        {
            // xUnit v2 collection fixtures don't support constructor DI of other
            // fixtures, so we lazy-capture HostFixture from the first test that
            // calls in. It's the same instance across the whole collection.
            _hostFixture ??= testFixture.HostFixture;

            var existing = _setups.FirstOrDefault(s =>
                s.Provider == provider && s.Encryption == encryption);

            if (existing is not null)
                return existing;

            // For Full encryption: the first setup runs Reset+Setup of the user encryption
            // password (inside CreateStorage → SetupUserEncryptionPassword). Reset wipes
            // ALL storage/workspace encryption keys in the DB, so without caching the
            // resulting cookie and reusing it for subsequent Full setups, every new (provider,
            // Full) combination would wipe the keys established for the previous one — leaving
            // earlier Full-encryption workspaces in a "no workspace wraps" broken state.
            var effectiveUser = encryption == StorageEncryptionType.Full && _fullEncryptionCookie is not null
                ? user with { EncryptionCookie = _fullEncryptionCookie }
                : user;

            var storage = await testFixture.CreateStorage(
                effectiveUser,
                provider, 
                encryption);

            if (encryption == StorageEncryptionType.Full && _fullEncryptionCookie is null)
                _fullEncryptionCookie = storage.WorkspaceEncryptionSession;

            var workspace = await testFixture.CreateWorkspace(
                storage, 
                effectiveUser);
            
            await testFixture.WaitForBucketReady(
                workspace, 
                user);

            var bucketName = testFixture.GetWorkspaceBucketName(workspace.ExternalId);

            var setup = new LiveStorageSetup(
                Provider: provider,
                Encryption: encryption,
                Storage: storage,
                Workspace: workspace,
                BucketName: bucketName,
                User: user);

            _setups.Add(setup);

            Log.Information("[LiveStoragesFixture] Created setup for {Provider}/{Encryption}: storage={StorageExtId}, workspace={WorkspaceExtId}, bucket={BucketName}",
                provider, encryption, storage.ExternalId, workspace.ExternalId, bucketName);

            return setup;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // _hostFixture is guaranteed non-null when _setups has any entries —
        // GetOrCreate sets it before appending. If no test ever called
        // GetOrCreate, _setups stays empty and the loop is a no-op.
        foreach (var setup in _setups)
        {
            try
            {
                await CleanupSetup(setup);
            }
            catch (Exception e)
            {
                Log.Error(e, "[LiveStoragesFixture] Error cleaning up setup {Provider}/{Encryption}",
                    setup.Provider, setup.Encryption);
            }
        }
    }

    private async Task CleanupSetup(LiveStorageSetup setup)
    {
        var storageInternalId = GetStorageInternalId(setup.Storage.ExternalId);

        await Api.Workspaces.Delete(
            externalId: setup.Workspace.ExternalId,
            cookie: setup.User.Cookie,
            antiforgery: setup.User.Antiforgery);

        var queueJobCompleted = await WaitForDeleteBucketJobCompleted(
            storageInternalId: storageInternalId,
            timeout: TimeSpan.FromSeconds(10));

        if (!queueJobCompleted)
        {
            Log.Warning("[LiveStoragesFixture] delete-bucket queue job for storage#{StorageId} did not complete in time — falling back to S3HardPurge for bucket '{BucketName}' on {Provider}.",
                storageInternalId, setup.BucketName, setup.Provider);

            await S3HardPurge.PurgeAndDeleteBucket(
                provider: setup.Provider,
                bucketName: setup.BucketName);
        }

        await Api.Storages.DeleteStorage(
            externalId: setup.Storage.ExternalId,
            cookie: setup.User.Cookie,
            antiforgery: setup.User.Antiforgery);
    }

    private int GetStorageInternalId(global::PlikShare.Storages.Id.StorageExtId externalId)
    {
        using var connection = _hostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT s_id
                     FROM s_storages
                     WHERE s_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"Storage '{externalId}' was not found in the database.");

        return rows[0];
    }

    private async Task<bool> WaitForDeleteBucketJobCompleted(
        int storageInternalId,
        TimeSpan timeout)
    {
        var storageIdMarker = $"\"storageId\":{storageInternalId}";
        var pollInterval = TimeSpan.FromMilliseconds(100);
        var attempts = (int)(timeout.TotalMilliseconds / pollInterval.TotalMilliseconds);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using (var connection = _hostFixture.Db.OpenConnection())
            {
                var found = connection
                    .Cmd(
                        sql: """
                             SELECT 1
                             FROM qc_queue_completed
                             WHERE qc_job_type = 'delete-bucket'
                               AND qc_definition LIKE $marker
                             LIMIT 1
                             """,
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithParameter("$marker", $"%{storageIdMarker}%")
                    .Execute();

                if (found.Count > 0)
                    return true;
            }

            await Task.Delay(pollInterval);
        }

        return false;
    }
}
