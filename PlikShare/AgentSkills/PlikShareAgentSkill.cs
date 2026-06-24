using System.Security.Cryptography;
using System.Text;
using PlikShare.Agents.Tools;

namespace PlikShare.AgentSkills;

public static class PlikShareAgentSkill
{
    public const string Name = "plikshare";

    public const string Description =
        "Manage files and folders in this PlikShare instance on behalf of users, " +
        "using the PlikShare agent tools.";

    public static string BuildSkillMarkdown()
    {
        // The allowed-tools list is generated from the catalog (plus the approval-protocol tools)
        // so it can never drift from the tools actually registered. Catalog order is stable, which
        // keeps the rendered markdown — and its digest — deterministic.
        var allowedTools = string.Join(
            " ",
            AgentToolCatalog.All
                .Select(tool => tool.Name)
                .Append(AgentToolNames.CheckApprovals)
                .Append(AgentToolNames.ExecuteOperation));

        return $$"""
               ---
               name: plikshare
               description: Manage files and folders in this PlikShare instance on behalf of users, using the PlikShare agent tools.
               metadata:
                 version: 0.16.0
               allowed-tools: {{allowedTools}}
               ---

               # PlikShare

               PlikShare organizes files into workspaces (private file trees); folders live inside a
               workspace and can be nested. You act through the PlikShare agent tools and can only touch
               what you have been granted access to.

               You can reach files through two independent access surfaces, and you may have either or both:

               - **Workspaces** you are a member of - start with `{{AgentToolNames.ListWorkspaces}}`, then use
                 the workspace tools below.
               - **Boxes** shared directly with you - start with `{{AgentToolNames.ListBoxes}}`, then use the
                 box-access tools (see **Box access** below).

               At the start of a task, check both. An empty result from one does **not** mean you have no
               access - the two surfaces are separate, so always also try the other.

               ## Finding a workspace

               You always need a `workspaceExternalId` (format `w_...`) before working with folders.
               Call the `{{AgentToolNames.ListWorkspaces}}` tool to see the workspaces you can access, then pick one. If
               the list is empty you may still have direct access to individual boxes (call
               `{{AgentToolNames.ListBoxes}}`, see **Box access**); otherwise create a workspace (see below) or
               ask an administrator to grant your agent access to a workspace.

               ## Listing storages

               Use the `{{AgentToolNames.ListStorages}}` tool to see the storages you can create workspaces on. Each entry has a
               `storageExternalId` (pass it to `{{AgentToolNames.CreateWorkspace}}`), a `name` and an `encryptionType`. Only
               storages you have access to are listed; storages with full client-side encryption are omitted
               because you cannot use them.

               ## Creating a workspace

               Use the `{{AgentToolNames.CreateWorkspace}}` tool to create a new workspace you will own:

               - `name` (required) - a name for the workspace.
               - `storageExternalId` (required) - the storage to create it on. Call `{{AgentToolNames.ListStorages}}` first to
                 see the storages you can use and pick one's `storageExternalId`.

               The tool returns the new workspace's `workspaceExternalId`, which you can immediately use with
               the other tools. Creating a workspace requires the "add workspace" permission; storages that
               use full client-side encryption cannot be used. If you have run out of your workspace
               allowance the tool reports it.

               ## Renaming a workspace

               Use the `{{AgentToolNames.RenameWorkspace}}` tool with the `workspaceExternalId` of a workspace you can access and
               the new `name` to rename it.

               ## Browsing a workspace

               Use the `{{AgentToolNames.ListWorkspaceContent}}` tool to see what a workspace contains:

               - `workspaceExternalId` (required) - a workspace id from `{{AgentToolNames.ListWorkspaces}}`.
               - `folderExternalId` (optional) - a folder id `fo_...` to list that folder; omit it to
                 list the workspace root.
               - `type` (optional) - `all` (default), `folder` or `file` to filter the results.
               - `cursor` (optional) - the `nextCursor` from a previous call, to fetch the next page;
                 reuse it with the same `workspaceExternalId`, `folderExternalId` and `type`.
               - `limit` (optional) - page size, default 200, maximum 1000.

               The tool returns `path` (the folders from the top level down to the one you are listing;
               empty at the workspace root) and an `entries` list. Each entry has a `type` of `folder` or
               `file`; folders are listed before files. When `hasMore` is true, call again with the
               returned `nextCursor` to get the rest.

               ## Searching across workspaces

               Use the `{{AgentToolNames.Search}}` tool to find files and folders by attributes, across one or many workspaces.
               One rule governs every list filter: **values inside a list are OR-ed, different filters are
               AND-ed, and an empty/omitted list disables that filter.** So
               `extensions: ["jpg","png"]` + `nameContains: ["invoice"]` finds items whose name contains
               "invoice" AND whose extension is jpg OR png.

               - `workspaceIds` / `folderIds` (optional) - scope; omit both to search every workspace you can
                 access. Folder scoping searches the folder's whole subtree. Ids you cannot access are
                 ignored.
               - `excludeWorkspaceIds` / `excludeFolderIds` (optional) - remove those workspaces, or those
                 folder subtrees, from the results. Useful to carve a folder out of a wider search
                 (e.g. search a workspace but `excludeFolderIds` an Archive folder).
               - `types` - `["file"]`, `["folder"]` or both/empty.
               - `nameContains` - substrings (OR), case-insensitive.
               - `extensions` / `contentTypes` - files only; `contentTypes` accepts exact (`image/png`) or a
                 prefix (`image/*`).
               - `createdAfter` / `createdBefore` (ISO 8601) and `sizeMin` / `sizeMax` - range bounds.

               `extensions`, `contentTypes` and size filters apply to files only; combining them with
               `types: ["folder"]` is rejected. Results are newest-first; when `hasMore` is true pass the
               returned `nextCursor` back as `cursor`. Each entry carries `workspaceExternalId` and its parent
               `folderExternalId` so you know where it lives.

               ## Looking up a file

               Use the `{{AgentToolNames.GetFile}}` tool with just a `fileExternalId` (no workspace needed) to read a file's
               details: name, extension, content type, size, creation time and the folder path it lives in.
               The file is resolved across every workspace you can access; if you cannot access it, the tool
               reports it as not found.

               ## Reading a file's content

               Use the `{{AgentToolNames.ReadFile}}` tool with a `fileExternalId` (no workspace needed) to read a file's content as
               UTF-8 text. Only text files are returned; binary files (images, video, PDF, archives) are rejected
               with a clear error - use `{{AgentToolNames.GetFile}}` for their metadata instead.

               - `offset` (optional) - byte position to start from; defaults to 0.
               - `maxBytes` (optional) - page size in bytes (default 65536, min 1024, max 262144).

               The tool returns `content`, `totalSizeInBytes`, `nextOffset` and `hasMore`. For a large file,
               keep calling with `offset` set to the previous `nextOffset` while `hasMore` is true to read the
               rest. The file is resolved across every workspace you can access; if you cannot access it, the
               tool reports it as not found.

               ## Getting a download link for a file

               Use the `{{AgentToolNames.GetFileDownloadLink}}` tool with a `fileExternalId` (no workspace needed) to create a
               short-lived link a user can click to download the file. Optionally pass `expiresInMinutes`
               (default 15, max 1440). The tool returns the `url`, the `fileName` and the `expiresAt`.

               This link is a capability: anyone who has it can download the file without logging in until it
               expires, so keep the expiry short and only share it with the intended user. Use it when a user
               wants the actual file; use `{{AgentToolNames.ReadFile}}` when you only need to read text content yourself.

               ## Getting a download link for many files or folders

               Use the `{{AgentToolNames.GetBulkDownloadLink}}` tool to download several files and/or whole folders from one
               workspace as a single ZIP archive:

               - `workspaceExternalId` (required).
               - `fileExternalIds` / `folderExternalIds` - what to include; provide at least one. Folders are
                 included with all their contents.
               - `excludedFileExternalIds` / `excludedFolderExternalIds` - optional ids to carve out of the
                 included folders.
               - `expiresInMinutes` (optional, default 15, max 1440).

               The tool returns the `url` and `expiresAt`. Like `{{AgentToolNames.GetFileDownloadLink}}`, the URL is a
               capability: anyone with it can download the ZIP without logging in until it expires.

               ## Creating a file

               Use the `{{AgentToolNames.CreateFile}}` tool to save text content as a new file:

               - `workspaceExternalId` (required) and `name` (required, including the extension, e.g.
                 `report.md`).
               - `content` (required) - the file content as UTF-8 text, at most 10 MB.
               - `folderExternalId` (optional) - the folder to create it in; omit for the workspace root.
               - `contentType` (optional) - derived from the extension if omitted.

               The tool returns the new file's `fileExternalId`. It is for text content; binary files are not
               supported.

               ## Renaming a file

               Use the `{{AgentToolNames.RenameFile}}` tool with `workspaceExternalId`, the `fileExternalId` of the file to
               rename, and the new `name`. Provide the name only, without the extension - the extension is
               kept unchanged.

               ## Creating a folder

               Use the `{{AgentToolNames.CreateFolder}}` tool:

               - `workspaceExternalId` (required) - a workspace id from `{{AgentToolNames.ListWorkspaces}}`.
               - `name` (required) - the folder name.
               - `parentFolderExternalId` (optional) - a folder id `fo_...` to create a subfolder;
                 omit it to create a top-level folder.

               The tool returns the new folder's `folderExternalId`.

               ## Renaming a folder

               Use the `{{AgentToolNames.RenameFolder}}` tool with `workspaceExternalId`, the `folderExternalId` of the
               folder to rename, and the new `name`.

               ## Deleting files and folders

               Use the `{{AgentToolNames.BulkDelete}}` tool with `workspaceExternalId` and any combination of:

               - `folderExternalIds` - folders to delete. Each folder is deleted **together with everything
                 inside it** (all subfolders and files), like `rm -rf`.
               - `fileExternalIds` - individual files to delete.

               Provide at least one id. The response is wrapped: on success it has `status: "executed"`
               with a `result` holding `deletedFileCount` and `deletedSizeInBytes` (the number and total
               size of files removed). Because deleting is destructive an administrator may require human
               approval first - then the tool instead returns `status: "waits_for_approval"`; see
               **Approvals** below. If the workspace has a trash policy enabled the deleted files can be
               restored from trash; otherwise the deletion is permanent.

               ## Moving files and folders

               Use the `{{AgentToolNames.MoveItems}}` tool to move files and/or folders into another folder within the same
               workspace:

               - `workspaceExternalId` (required) - the workspace that holds the items and the destination.
               - `folderExternalIds` / `fileExternalIds` - what to move; provide at least one. Each folder is
                 moved together with everything inside it.
               - `destinationFolderExternalId` (optional) - where to move them; omit to move to the workspace
                 root.

               All items and the destination must live in the same workspace - moving items between workspaces
               is not supported. A folder cannot be moved into itself or one of its own subfolders.

               ## Sharing files with a public link

               Use the `{{AgentToolNames.CreateShareLink}}` tool to turn files and/or folders into a public link anyone can
               open without logging in:

               - `workspaceExternalId` (required) and `name` (required).
               - `fileExternalIds` / `folderExternalIds` - what to share; provide at least one.
               - `expiresAt` (optional ISO 8601), `maxDownloads` (optional), `password` (optional).

               The tool returns the share's `externalId` and the public `url` to hand to the user.

               ## Managing share links

               - `{{AgentToolNames.ListShareLinks}}` (with `workspaceExternalId`) lists every share link in the workspace.
               - `{{AgentToolNames.GetShareLink}}` (with `workspaceExternalId` and `shareLinkExternalId`) returns one link's
                 details, including which files and folders it shares and excludes.
               - `{{AgentToolNames.UpdateShareLink}}` (with `workspaceExternalId` and `shareLinkExternalId`) changes a link's
                 settings. Only the fields you choose are touched - anything left out is kept. Pass `name` to
                 rename. For the nullable settings, set the matching flag and provide a value to set it, or set
                 the flag and leave the value empty to clear it:
                 `shouldUpdateExpiry` + `expiresAt` (ISO 8601; empty = no expiry),
                 `shouldUpdateMaxDownloads` + `maxDownloads` (empty = unlimited),
                 `shouldUpdatePassword` + `password` (empty = no password).
               - `{{AgentToolNames.DeleteShareLink}}` (with `workspaceExternalId` and `shareLinkExternalId`) deletes a link;
                 its public URL stops working but the shared files and folders are left intact.

               ## Workspace members

               A workspace can be shared with people (identified by email). Manage its members with:

               - `{{AgentToolNames.ListWorkspaceMembers}}` (with `workspaceExternalId`) lists the members, each with a
                 `memberExternalId`, `email`, `inviterEmail`, whether they have accepted (`invitationAccepted`)
                 and whether they may share the workspace (`allowShare`).
               - `{{AgentToolNames.InviteWorkspaceMembers}}` (with `workspaceExternalId`, `memberEmails` and optional
                 `allowShare`, default false) invites one or more people by email; each receives an invitation
                 and gains access once they accept. `allowShare` lets them invite further members.
               - `{{AgentToolNames.UpdateWorkspaceMemberPermissions}}` (with `workspaceExternalId`, `memberExternalId`,
                 `allowShare`) changes a member's permissions.
               - `{{AgentToolNames.RevokeWorkspaceMember}}` (with `workspaceExternalId`, `memberExternalId`) removes a
                 member (or a pending invitation), revoking their access.

               ## Boxes

               A box is a curated, shareable view of one folder of a workspace. You expose a box to people
               (box members) or anonymously through public box links, each with their own permissions. Manage
               boxes with:

               - `{{AgentToolNames.ListWorkspaceBoxes}}` (with `workspaceExternalId`) lists the boxes, each with a `boxExternalId`,
                 `name`, `isEnabled` and the folder path it exposes.
               - `{{AgentToolNames.GetBox}}` (with `workspaceExternalId`, `boxExternalId`) returns a box's details, how many
                 members and links it has, and its immediate subfolders and files.
               - `{{AgentToolNames.CreateBox}}` (with `workspaceExternalId`, `name`, `folderExternalId`) creates a box that
                 exposes the given folder; returns the new `boxExternalId`.
               - `{{AgentToolNames.UpdateBox}}` (with `workspaceExternalId`, `boxExternalId` and any of `name`, `isEnabled`,
                 `folderExternalId`) updates a box; anything you leave out is kept.
               - `{{AgentToolNames.DeleteBox}}` (with `workspaceExternalId`, `boxExternalId`) deletes a box together with its
                 links and members; the underlying folder and files stay intact.

               ## Box links

               A box link is a public URL (plus an access code) that lets anyone interact with a box according
               to the link's permissions. The permission flags are `allowDownload`, `allowUpload`, `allowList`,
               `allowDeleteFile`, `allowRenameFile`, `allowMoveItems`, `allowCreateFolder`, `allowRenameFolder`
               and `allowDeleteFolder`.

               - `{{AgentToolNames.ListBoxLinks}}` (with `workspaceExternalId`, `boxExternalId`) lists a box's links with
                 their external ids, names, enabled state, access codes, permissions and widget origins.
               - `{{AgentToolNames.CreateBoxLink}}` (with `workspaceExternalId`, `boxExternalId`, `name`) creates a link;
                 it starts with list-only permission. Returns the link's `externalId` and `accessCode`.
               - `{{AgentToolNames.UpdateBoxLink}}` (with `workspaceExternalId`, `boxLinkExternalId` and any of `name`,
                 `isEnabled`, the permission flags above, `widgetOrigins`) updates a link. Any permission flag
                 you omit keeps its current value; pass an empty `widgetOrigins` list to clear it.
               - `{{AgentToolNames.DeleteBoxLink}}` (with `workspaceExternalId`, `boxLinkExternalId`) deletes a link,
                 immediately invalidating its URL. The box and its content stay intact.
               - `{{AgentToolNames.RegenerateBoxLinkAccessCode}}` (with `workspaceExternalId`, `boxLinkExternalId`) issues a
                 new access code, immediately invalidating the link's current URL. Returns the new `accessCode`.

               ## Box members

               A box can also be shared with named people (by email), each with their own box permissions (the
               same nine flags listed under **Box links**).

               - `{{AgentToolNames.ListBoxMembers}}` (with `workspaceExternalId`, `boxExternalId`) lists the members, each
                 with a `memberExternalId`, `email`, `inviterEmail`, whether they have accepted and their
                 permissions.
               - `{{AgentToolNames.InviteBoxMembers}}` (with `workspaceExternalId`, `boxExternalId`, `memberEmails`) invites
                 people by email; each gains list-only access once they accept. Use
                 `{{AgentToolNames.UpdateBoxMemberPermissions}}` afterwards to grant more.
               - `{{AgentToolNames.UpdateBoxMemberPermissions}}` (with `workspaceExternalId`, `boxExternalId`,
                 `memberExternalId` and any of the permission flags) updates a member's permissions; any flag you
                 omit keeps its current value.
               - `{{AgentToolNames.RevokeBoxMember}}` (with `workspaceExternalId`, `boxExternalId`, `memberExternalId`)
                 removes a member (or a pending invitation) from the box.

               ## Box access

               Separately from managing a workspace, you can be granted direct access to an individual box -
               the same way a person is. These boxes are not tied to a workspace you belong to; an
               administrator shares them with you one by one. When you work inside such a box you act as its
               consumer: everything is scoped to the folder the box exposes, and you only need the box's
               `boxExternalId` (never a `workspaceExternalId`).

               - `{{AgentToolNames.ListBoxes}}` (no arguments) lists the boxes shared directly with you, each with a
                 `externalId`, `name`, `isEnabled` and the `workspaceName` it belongs to. This mirrors
                 `{{AgentToolNames.ListWorkspaces}}` - it is your entry point into box access.
               - `{{AgentToolNames.GetBoxDetails}}` (with `boxExternalId`) returns the box's name, whether it is enabled and
                 the `rootFolderExternalId` it exposes.
               - `{{AgentToolNames.ListBoxContent}}` (with `boxExternalId`, optional `folderExternalId`) lists the folders and
                 files in the box's root, or in one of its folders.
               - `{{AgentToolNames.ReadBoxFile}}` (with `boxExternalId`, `fileExternalId`, optional `offset`, `maxBytes`) reads a
                 text file's content, paging through larger files with the returned `nextOffset` and `hasMore`.
               - `{{AgentToolNames.GetBoxFileDownloadLink}}` (with `boxExternalId`, `fileExternalId`, optional `expiresInMinutes`)
                 creates a short-lived link to download one file.
               - `{{AgentToolNames.GetBoxBulkDownloadLink}}` (with `boxExternalId`, `fileExternalIds`, `folderExternalIds`) creates a
                 link to download several items as one ZIP archive.
               - `{{AgentToolNames.SearchBox}}` (with `boxExternalId`, `phrase`, optional `folderExternalId`) finds files by name
                 inside the box.
               - `{{AgentToolNames.CreateBoxFolder}}` (with `boxExternalId`, `name`, optional `parentFolderExternalId`) creates a
                 folder; omit the parent for the box root.
               - `{{AgentToolNames.CreateBoxFile}}` (with `boxExternalId`, `name`, `content`, optional `folderExternalId`,
                 `contentType`) creates a text file.
               - `{{AgentToolNames.RenameBoxFile}}` / `{{AgentToolNames.RenameBoxFolder}}` (with `boxExternalId`, the item's id and a new
                 `name`) rename a file or folder.
               - `{{AgentToolNames.MoveBoxItems}}` (with `boxExternalId`, `folderExternalIds`, `fileExternalIds`, optional
                 `destinationFolderExternalId`) moves items into another folder; omit the destination for the box root.
               - `{{AgentToolNames.DeleteBoxItems}}` (with `boxExternalId`, `fileExternalIds`, `folderExternalIds`) deletes files
                 and/or folders, including whole trees. This is destructive and usually requires approval.

               Every id you pass must live inside the box - files and folders elsewhere are not reachable
               through these tools, even when they share the underlying workspace.

               ## Approvals

               Some operations can be configured to require a human's approval before they run - typically
               destructive ones such as `{{AgentToolNames.BulkDelete}}` and `{{AgentToolNames.DeleteShareLink}}`, or ones that grant
               people access such as `{{AgentToolNames.InviteWorkspaceMembers}}` and `{{AgentToolNames.InviteBoxMembers}}`. Whether a given tool needs
               approval is decided per agent by an administrator, so the same tool may run immediately for
               one agent and need approval for another. You cannot tell in advance - react to what the
               tool returns.

               A tool that needs approval does **not** act when you call it. Instead it returns
               `status: "waits_for_approval"` with an `approvalRequestId` and an `expiresAt`. When you get
               this:

               1. Tell the user the operation is waiting for their approval in PlikShare, under **Agent
                  requests**, where they can see exactly what it will affect and approve or deny it. Nothing
                  happens until they act.
               2. Poll the `{{AgentToolNames.CheckApprovals}}` tool to follow your outstanding operations. Each entry has its
                  `approvalRequestId`, `toolName` and a `status`: `pending` (still waiting), `approved`
                  (ready to run), `denied` or `expired`.
               3. Once an operation shows `approved`, call the `{{AgentToolNames.ExecuteOperation}}` tool with its
                  `approvalRequestId` to actually run it. It returns `status: "executed"` with the tool's
                  normal result under `result`.

               If the user denies the request, or it expires before they act, `{{AgentToolNames.ExecuteOperation}}` returns
               `status: "rejected"` with the reason - do not retry, the operation will not run.
               `{{AgentToolNames.ExecuteOperation}}` is safe to call more than once for the same `approvalRequestId`: an
               already-executed operation returns its stored result without running again.

               A tool that does **not** require approval simply returns `status: "executed"` with its
               `result` right away - there is nothing to confirm.

               ## Notes

               - Workspaces that use full client-side encryption are not accessible to agents.
               - Every action is recorded in PlikShare's audit log under your agent identity.
               """;
    }

    public static byte[] GetSkillMarkdownBytes()
    {
        return Encoding.UTF8.GetBytes(
            BuildSkillMarkdown());
    }

    public static string ComputeDigest(byte[] skillMarkdownBytes)
    {
        var hash = SHA256.HashData(skillMarkdownBytes);

        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
