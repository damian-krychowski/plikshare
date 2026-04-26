using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_change_owner_tests : TestFixture
{
    public full_encryption_change_owner_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
    }

    private async Task<AppSignedInUser> SignInWithEncryption(User user)
    {
        var signedIn = await SignIn(user);
        var setup = await Api.UserEncryptionPassword.Setup(
            userExternalId: signedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: signedIn.Cookie,
            antiforgery: signedIn.Antiforgery);

        return signedIn with { EncryptionCookie = setup.EncryptionCookie };
    }

    [Fact]
    public async Task admin_with_sek_can_transfer_full_encrypted_workspace_to_member_with_encryption_set_up()
    {
        //given
        var firstOwner = await SignInWithEncryption(Users.AppOwner);
        var secondOwner = await SignInWithEncryption(Users.SecondAppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        // Sanity: second owner picked up a sek wrap because they had encryption set up
        // before the storage was created — that is the path our admin uses to derive
        // the workspace DEK at transfer time.
        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().Contain(secondOwner.Email);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse(
            "second owner has no wek wrap before the transfer — only sek");

        //when — second owner (also an app owner, hence admin) transfers ownership to
        // themselves; uses their own sek wrap to derive the workspace DEK and seal it
        // for themselves as the new owner.
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: workspace.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(
                NewOwnerExternalId: secondOwner.ExternalId),
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery,
            userEncryptionSession: secondOwner.EncryptionCookie);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeTrue(
            "the transfer must provision a wek wrap for the new owner");

        HasWorkspaceEncryptionKey(workspace.ExternalId, firstOwner.Email).Should().BeFalse(
            "the previous owner's wek must be wiped on transfer to drop their decrypt access");
    }

    [Fact]
    public async Task transfer_to_user_without_encryption_set_up_returns_member_encryption_not_set_up()
    {
        //given
        var firstOwner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        // Plain registration, no encryption setup.
        var newUser = await InviteAndRegisterUser(firstOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.UpdateOwner(
                externalId: workspace.ExternalId,
                request: new ChangeWorkspaceOwnerRequestDto(
                    NewOwnerExternalId: newUser.ExternalId),
                cookie: firstOwner.Cookie,
                antiforgery: firstOwner.Antiforgery,
                userEncryptionSession: firstOwner.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("member-encryption-not-set-up");

        HasWorkspaceEncryptionKey(workspace.ExternalId, firstOwner.Email).Should().BeTrue(
            "transfer rolled back — previous owner's wek must remain intact");
    }

    [Fact]
    public async Task transfer_without_actor_encryption_session_returns_user_encryption_session_required()
    {
        //given
        var firstOwner = await SignInWithEncryption(Users.AppOwner);
        var secondOwner = await SignInWithEncryption(Users.SecondAppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        //when — actor sends the transfer request without their encryption cookie. The
        // server has no way to unwrap any DEK on their behalf, so it must short-circuit.
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.UpdateOwner(
                externalId: workspace.ExternalId,
                request: new ChangeWorkspaceOwnerRequestDto(
                    NewOwnerExternalId: secondOwner.ExternalId),
                cookie: firstOwner.Cookie,
                antiforgery: firstOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task transfer_by_admin_without_sek_or_wek_returns_not_a_storage_admin()
    {
        //given — storage created BEFORE the second owner sets up encryption, so they
        // hold neither sek nor wek for this storage. Even though they pass the admin
        // role check, they have no key path and cannot re-wrap the workspace DEK.
        var firstOwner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var secondOwner = await SignInWithEncryption(Users.SecondAppOwner);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        // Sanity: second owner has no sek (storage existed before they set up encryption)
        // and is not a workspace member.
        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().NotContain(secondOwner.Email);

        // Pick a user that has encryption set up so the only failing precondition is
        // the actor's own missing key path.
        var thirdUser = await InviteAndRegisterUser(firstOwner);
        var thirdUserSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: thirdUser.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: thirdUser.Cookie,
            antiforgery: thirdUser.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.UpdateOwner(
                externalId: workspace.ExternalId,
                request: new ChangeWorkspaceOwnerRequestDto(
                    NewOwnerExternalId: thirdUser.ExternalId),
                cookie: secondOwner.Cookie,
                antiforgery: secondOwner.Antiforgery,
                userEncryptionSession: secondOwner.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("not-a-storage-admin");

        // make analyzer happy — variable is meaningful in setup, not in assertion
        thirdUserSetup.Should().NotBeNull();
    }

    [Fact]
    public async Task transfer_on_unencrypted_workspace_does_not_require_encryption_session()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var newOwner = await InviteAndRegisterUser(owner);

        //when — None storage; no encryption cookie attached. Existing semantics must hold.
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: workspace.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(
                NewOwnerExternalId: newOwner.ExternalId),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then — query did not throw; ownership flipped on a non-Full storage with zero
        // crypto preconditions, mirroring the pre-change behavior.
    }

    [Fact]
    public async Task user_list_exposes_is_encryption_configured_flag()
    {
        //given
        var owner = await SignInWithEncryption(Users.AppOwner);

        var newUser = await InviteAndRegisterUser(owner);

        //when
        var users = await Api.Users.Get(cookie: owner.Cookie);

        //then
        var ownerRow = users.Items.Single(u => u.ExternalId == owner.ExternalId);
        ownerRow.IsEncryptionConfigured.Should().BeTrue();

        var newUserRow = users.Items.Single(u => u.ExternalId == newUser.ExternalId);
        newUserRow.IsEncryptionConfigured.Should().BeFalse();
    }
}
