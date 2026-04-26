using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Contracts;
using PlikShare.AuditLog.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Users.PermissionsAndRoles;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AuditLog;

/// <summary>
/// End-to-end coverage for the decrypt-on-read path added to
/// <c>AuditLogEndpoints.GetAuditLogEntryDetails</c>:
///
/// Audit log entries about content in a full-encrypted workspace persist
/// <see cref="PlikShare.Core.Encryption.EncodedMetadataValue"/> fields (folder names,
/// file names, …) verbatim — i.e. the at-rest <c>pse:</c>-prefixed envelope. When an
/// admin pulls a single entry's details, the endpoint opens a per-request workspace
/// encryption session for the caller and walks the details JSON, replacing each
/// encrypted leaf with either:
///   - the AES-GCM plaintext, when the caller can produce a Workspace DEK (wek wrap or
///     sek-derived fallback), or
///   - the literal string <c>[encrypted]</c>, when no Workspace DEK is available.
///
/// The list endpoint (<c>POST /api/audit-log</c>) returns the encoded form unchanged —
/// list rows do not embed the details JSON. These tests therefore only target the
/// per-entry details endpoint, which is the only place decryption fires.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class audit_log_decryption_tests : TestFixture
{
    /// <summary>
    /// Marker the decryptor writes when it encounters a <c>pse:</c> envelope it cannot
    /// decode (no session, or session has no matching DEK). Hard-coded here so the test
    /// fails loudly if the production constant ever drifts.
    /// </summary>
    private const string EncryptedPlaceholder = "[encrypted]";

    public audit_log_decryption_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        ClearAuditLog();
    }

    [Fact]
    public async Task admin_with_encryption_session_sees_decrypted_folder_name_in_audit_log_details()
    {
        // Happy path. Folder names persist as `pse:…` envelopes in audit-log details JSON
        // (encrypted at write time by the workspace encryption session of the actor). On
        // read, the admin's own session must be able to unwrap their wek and decrypt the
        // envelope back to the original plaintext name.
        //given
        var (owner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var folderName = $"top-secret-{Guid.NewGuid():N}";
        var folder = await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: owner);

        var entry = await FindAuditLogEntry(
            user: owner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        //when — owner pulls the entry with their unlocked encryption session.
        var details = await Api.AuditLog.GetEntryDetails(
            externalId: AuditLogExtId.Parse(entry.ExternalId),
            cookie: owner.Cookie,
            userEncryptionSession: owner.EncryptionCookie);

        //then
        details.WorkspaceExternalId.Should().Be(workspace.ExternalId.Value);
        details.Details.Should().NotBeNull();
        details.Details.Should().Contain(folderName,
            "the encrypted folder-name envelope must be unwrapped to plaintext for an admin " +
            "who holds a valid Workspace DEK");
        details.Details.Should().NotContain("pse:",
            "no `pse:` envelope must leak past the decryption pass when the session can decrypt it");
        details.Details.Should().NotContain(EncryptedPlaceholder,
            "the [encrypted] fallback must NOT fire when the session has the right DEKs");
    }

    [Fact]
    public async Task admin_with_storage_dek_only_sees_decrypted_folder_name_via_storage_dek_path()
    {
        // Tie-in to the storage-DEK fallback added to UnsealWorkspaceDeks: a second app
        // owner who only has a sek (and no wek) for this workspace must still be able to
        // read decrypted audit-log details. The decryption path is identical; only the
        // way the Workspace DEK is obtained differs.
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folderName = $"derived-dek-{Guid.NewGuid():N}";
        var folder = await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: firstOwner);

        // Sanity — the second owner is intentionally NOT a wek holder; the only path to
        // a Workspace DEK for them is through the storage-DEK derivation.
        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse(
            "second app owner has no wek for this workspace — that's the path under test");

        var entry = await FindAuditLogEntry(
            user: secondOwner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        //when
        var details = await Api.AuditLog.GetEntryDetails(
            externalId: AuditLogExtId.Parse(entry.ExternalId),
            cookie: secondOwner.Cookie,
            userEncryptionSession: secondOwner.EncryptionCookie);

        //then
        details.Details.Should().NotBeNull();
        details.Details.Should().Contain(folderName,
            "the Workspace DEK derived from (Storage DEK, workspace salt) must decrypt the same " +
            "envelope produced by the workspace creator");
        details.Details.Should().NotContain(EncryptedPlaceholder);
    }

    [Fact]
    public async Task admin_with_locked_encryption_session_gets_423_user_encryption_session_required()
    {
        // The endpoint short-circuits with 423 only when the caller's encryption metadata
        // is set up but the session cookie is missing — the UI uses that distinct status
        // to open the unlock dialog instead of the setup dialog or a permanent error.
        //given
        var (owner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: owner);

        var entry = await FindAuditLogEntry(
            user: owner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        await Api.UserEncryptionPassword.Lock(
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(async () =>
            await Api.AuditLog.GetEntryDetails(
                externalId: AuditLogExtId.Parse(entry.ExternalId),
                cookie: owner.Cookie,
                userEncryptionSession: null));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task admin_with_session_but_no_workspace_keys_sees_encrypted_placeholder_instead_of_403()
    {
        // The endpoint deliberately does NOT short-circuit with 403 pending-key-grant for
        // audit reads — admins with a valid encryption session but no wek/sek for this
        // workspace still get the entry back, with encrypted fields rewritten as
        // `[encrypted]`. This keeps surrounding plaintext (event type, actor email, time)
        // visible for forensic review even when the encrypted body is not unsealable.
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folderName = $"unreadable-{Guid.NewGuid():N}";
        var folder = await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: firstOwner);

        // Admin without a sek for this storage and without a wek for this workspace — see
        // the non-admin-gets-pending-key-grant flow used by the matching access tests.
        // The user starts as a regular invitee, gets promoted to admin (so the audit-log
        // policy lets them in), and only then sets up encryption — so they end up with a
        // public key but no sek (storage was created earlier) and no wek for this workspace.
        var blindAdmin = await InviteAndRegisterUser(user: firstOwner);
        await PromoteToAdminWithAuditLogAccess(
            actor: firstOwner,
            targetUser: blindAdmin);

        var blindAdminSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: blindAdmin.ExternalId,
            encryptionPassword: "Blind-Admin-Pass-1!",
            cookie: blindAdmin.Cookie,
            antiforgery: blindAdmin.Antiforgery);

        var entry = await FindAuditLogEntry(
            user: firstOwner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        //when
        var details = await Api.AuditLog.GetEntryDetails(
            externalId: AuditLogExtId.Parse(entry.ExternalId),
            cookie: blindAdmin.Cookie,
            userEncryptionSession: blindAdminSetup.EncryptionCookie);

        //then — entry returned successfully, plaintext fields intact, encrypted fields
        // collapsed to the [encrypted] placeholder (NOT 403, NOT raw `pse:`).
        details.WorkspaceExternalId.Should().Be(workspace.ExternalId.Value);
        details.Details.Should().NotBeNull();
        details.Details.Should().Contain(EncryptedPlaceholder,
            "encrypted fields must be replaced with the literal placeholder when the caller " +
            "has no Workspace DEK");
        details.Details.Should().NotContain(folderName,
            "plaintext folder name must NOT leak when the caller has no Workspace DEK");
        details.Details.Should().NotContain("pse:",
            "the raw at-rest envelope must NOT leak — the decryptor must rewrite every " +
            "`pse:`-prefixed leaf, even when it cannot decrypt it");
    }

    [Fact]
    public async Task admin_without_encryption_metadata_sees_encrypted_placeholder_not_session_required()
    {
        // The endpoint distinguishes 'setup-required' (encryption never configured) from
        // 'session-required' (configured but locked). Setup-required admins do NOT get a
        // 423 — they fall through to the [encrypted] placeholder so they can still read
        // the rest of the audit entry. Only session-required short-circuits with 423.
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folderName = $"setup-required-{Guid.NewGuid():N}";
        await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: firstOwner);

        // Admin who has never set up encryption — promoted to admin before any setup runs.
        var unsetupAdmin = await InviteAndRegisterUser(user: firstOwner);
        await PromoteToAdminWithAuditLogAccess(
            actor: firstOwner,
            targetUser: unsetupAdmin);

        var entry = await FindAuditLogEntry(
            user: firstOwner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        //when
        var details = await Api.AuditLog.GetEntryDetails(
            externalId: AuditLogExtId.Parse(entry.ExternalId),
            cookie: unsetupAdmin.Cookie,
            userEncryptionSession: null);

        //then
        details.Details.Should().Contain(EncryptedPlaceholder);
        details.Details.Should().NotContain(folderName);
    }

    [Fact]
    public async Task audit_log_entry_for_non_full_encrypted_workspace_returns_plaintext_unchanged()
    {
        // Regression guard: the decrypt-on-read pass MUST be a no-op for audit entries
        // tied to a None / Managed-encrypted workspace. Folder names land in the details
        // JSON as plain strings (no `pse:` envelope), and the response must echo them
        // verbatim regardless of whether the caller has an encryption session.
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var folderName = $"plaintext-folder-{Guid.NewGuid():N}";
        await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: owner);

        var entry = await FindAuditLogEntry(
            user: owner,
            eventType: AuditLogEventTypes.Folder.Created,
            workspaceExternalId: workspace.ExternalId.Value);

        //when — no encryption session passed; the endpoint must still echo the plaintext
        // because the workspace's storage is not full-encrypted.
        var details = await Api.AuditLog.GetEntryDetails(
            externalId: AuditLogExtId.Parse(entry.ExternalId),
            cookie: owner.Cookie,
            userEncryptionSession: null);

        //then
        details.Details.Should().NotBeNull();
        details.Details.Should().Contain(folderName);
        details.Details.Should().NotContain(EncryptedPlaceholder);
        details.Details.Should().NotContain("pse:");
    }

    /// <summary>
    /// Same pattern as <c>storage_dek_workspace_dek_derivation_tests</c>: setup encryption
    /// for both app owners and create a full-encryption storage so both pick up a sek row.
    /// </summary>
    private async Task<(AppSignedInUser FirstOwner, AppSignedInUser SecondOwner, AppStorage Storage)>
        SetupTwoAppOwnersAndFullEncryptedStorage()
    {
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        var secondOwnerSignedIn = await SignIn(user: Users.SecondAppOwner);
        var secondOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwnerSignedIn.Cookie,
            antiforgery: secondOwnerSignedIn.Antiforgery);

        var secondOwner = secondOwnerSignedIn with { EncryptionCookie = secondOwnerSetup.EncryptionCookie };

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        return (firstOwner, secondOwner, storage);
    }

    /// <summary>
    /// Polls the audit-log list endpoint until the expected (workspace, event type)
    /// entry shows up — audit-log writes are asynchronous so freshly produced entries
    /// are not always visible on the first read.
    /// </summary>
    private async Task<AuditLogItemDto> FindAuditLogEntry(
        AppSignedInUser user,
        string eventType,
        string workspaceExternalId)
    {
        for (var i = 0; i < 50; i++)
        {
            var page = await Api.AuditLog.GetLogs(
                request: new GetAuditLogRequestDto
                {
                    PageSize = 50,
                    EventTypes = [eventType],
                    WorkspaceExternalId = workspaceExternalId
                },
                cookie: user.Cookie,
                antiforgery: user.Antiforgery);

            if (page.Items.Count > 0)
                return page.Items[0];

            await Task.Delay(50);
        }

        throw new InvalidOperationException(
            $"Could not find an audit-log entry of type '{eventType}' tied to workspace '{workspaceExternalId}'.");
    }

    /// <summary>
    /// Grants the target user the admin role plus the audit-log permission via the public
    /// permissions-and-roles endpoint. The audit-log endpoint policy requires both — the
    /// role for authorization and the permission for the dedicated filter.
    /// </summary>
    private async Task PromoteToAdminWithAuditLogAccess(
        AppSignedInUser actor,
        AppSignedInUser targetUser)
    {
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: targetUser.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true,
                CanAddWorkspace = false,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = true
            },
            cookie: actor.Cookie,
            antiforgery: actor.Antiforgery);
    }
}
