using System.Security.Cryptography;
using System.Text;

namespace PlikShare.AgentSkills;

public static class PlikShareAgentSkill
{
    public const string Name = "plikshare";

    public const string Description =
        "Manage files and folders in this PlikShare instance on behalf of users, " +
        "using the PlikShare agent tools.";

    public static string BuildSkillMarkdown()
    {
        return """
               ---
               name: plikshare
               description: Manage files and folders in this PlikShare instance on behalf of users, using the PlikShare agent tools.
               metadata:
                 version: 0.9.0
               allowed-tools: list_workspaces list_workspace_content search get_file read_file get_file_download_link get_bulk_download_link rename_file create_folder rename_folder bulk_delete create_share_link list_share_links get_share_link update_share_link delete_share_link
               ---

               # PlikShare

               PlikShare organizes files into workspaces (private file trees). Folders live inside a
               workspace and can be nested. You act through the PlikShare agent tools and can only
               touch workspaces you have been granted access to.

               ## Finding a workspace

               You always need a `workspaceExternalId` (format `w_...`) before working with folders.
               Call the `list_workspaces` tool to see the workspaces you can access, then pick one. If
               the list is empty, ask an administrator to grant your agent access to a workspace.

               ## Browsing a workspace

               Use the `list_workspace_content` tool to see what a workspace contains:

               - `workspaceExternalId` (required) — a workspace id from `list_workspaces`.
               - `folderExternalId` (optional) — a folder id `fo_...` to list that folder; omit it to
                 list the workspace root.
               - `type` (optional) — `all` (default), `folder` or `file` to filter the results.
               - `cursor` (optional) — the `nextCursor` from a previous call, to fetch the next page;
                 reuse it with the same `workspaceExternalId`, `folderExternalId` and `type`.
               - `limit` (optional) — page size, default 200, maximum 1000.

               The tool returns `path` (the folders from the top level down to the one you are listing;
               empty at the workspace root) and an `entries` list. Each entry has a `type` of `folder` or
               `file`; folders are listed before files. When `hasMore` is true, call again with the
               returned `nextCursor` to get the rest.

               ## Searching across workspaces

               Use the `search` tool to find files and folders by attributes, across one or many workspaces.
               One rule governs every list filter: **values inside a list are OR-ed, different filters are
               AND-ed, and an empty/omitted list disables that filter.** So
               `extensions: ["jpg","png"]` + `nameContains: ["invoice"]` finds items whose name contains
               "invoice" AND whose extension is jpg OR png.

               - `workspaceIds` / `folderIds` (optional) — scope; omit both to search every workspace you can
                 access. Folder scoping searches the folder's whole subtree. Ids you cannot access are
                 ignored.
               - `excludeWorkspaceIds` / `excludeFolderIds` (optional) — remove those workspaces, or those
                 folder subtrees, from the results. Useful to carve a folder out of a wider search
                 (e.g. search a workspace but `excludeFolderIds` an Archive folder).
               - `types` — `["file"]`, `["folder"]` or both/empty.
               - `nameContains` — substrings (OR), case-insensitive.
               - `extensions` / `contentTypes` — files only; `contentTypes` accepts exact (`image/png`) or a
                 prefix (`image/*`).
               - `createdAfter` / `createdBefore` (ISO 8601) and `sizeMin` / `sizeMax` — range bounds.

               `extensions`, `contentTypes` and size filters apply to files only; combining them with
               `types: ["folder"]` is rejected. Results are newest-first; when `hasMore` is true pass the
               returned `nextCursor` back as `cursor`. Each entry carries `workspaceExternalId` and its parent
               `folderExternalId` so you know where it lives.

               ## Looking up a file

               Use the `get_file` tool with just a `fileExternalId` (no workspace needed) to read a file's
               details: name, extension, content type, size, creation time and the folder path it lives in.
               The file is resolved across every workspace you can access; if you cannot access it, the tool
               reports it as not found.

               ## Reading a file's content

               Use the `read_file` tool with a `fileExternalId` (no workspace needed) to read a file's content as
               UTF-8 text. Only text files are returned; binary files (images, video, PDF, archives) are rejected
               with a clear error — use `get_file` for their metadata instead.

               - `offset` (optional) — byte position to start from; defaults to 0.
               - `maxBytes` (optional) — page size in bytes (default 65536, min 1024, max 262144).

               The tool returns `content`, `totalSizeInBytes`, `nextOffset` and `hasMore`. For a large file,
               keep calling with `offset` set to the previous `nextOffset` while `hasMore` is true to read the
               rest. The file is resolved across every workspace you can access; if you cannot access it, the
               tool reports it as not found.

               ## Getting a download link for a file

               Use the `get_file_download_link` tool with a `fileExternalId` (no workspace needed) to create a
               short-lived link a user can click to download the file. Optionally pass `expiresInMinutes`
               (default 15, max 1440). The tool returns the `url`, the `fileName` and the `expiresAt`.

               This link is a capability: anyone who has it can download the file without logging in until it
               expires, so keep the expiry short and only share it with the intended user. Use it when a user
               wants the actual file; use `read_file` when you only need to read text content yourself.

               ## Getting a download link for many files or folders

               Use the `get_bulk_download_link` tool to download several files and/or whole folders from one
               workspace as a single ZIP archive:

               - `workspaceExternalId` (required).
               - `fileExternalIds` / `folderExternalIds` — what to include; provide at least one. Folders are
                 included with all their contents.
               - `excludedFileExternalIds` / `excludedFolderExternalIds` — optional ids to carve out of the
                 included folders.
               - `expiresInMinutes` (optional, default 15, max 1440).

               The tool returns the `url` and `expiresAt`. Like `get_file_download_link`, the URL is a
               capability: anyone with it can download the ZIP without logging in until it expires.

               ## Renaming a file

               Use the `rename_file` tool with `workspaceExternalId`, the `fileExternalId` of the file to
               rename, and the new `name`. Provide the name only, without the extension — the extension is
               kept unchanged.

               ## Creating a folder

               Use the `create_folder` tool:

               - `workspaceExternalId` (required) — a workspace id from `list_workspaces`.
               - `name` (required) — the folder name.
               - `parentFolderExternalId` (optional) — a folder id `fo_...` to create a subfolder;
                 omit it to create a top-level folder.

               The tool returns the new folder's `folderExternalId`.

               ## Renaming a folder

               Use the `rename_folder` tool with `workspaceExternalId`, the `folderExternalId` of the
               folder to rename, and the new `name`.

               ## Deleting files and folders

               Use the `bulk_delete` tool with `workspaceExternalId` and any combination of:

               - `folderExternalIds` — folders to delete. Each folder is deleted **together with everything
                 inside it** (all subfolders and files), like `rm -rf`.
               - `fileExternalIds` — individual files to delete.

               Provide at least one id. The tool returns `deletedFileCount` and `deletedSizeInBytes` (the
               number and total size of files removed). If the workspace has a trash policy enabled the
               deleted files can be restored from trash; otherwise the deletion is permanent.

               ## Sharing files with a public link

               Use the `create_share_link` tool to turn files and/or folders into a public link anyone can
               open without logging in:

               - `workspaceExternalId` (required) and `name` (required).
               - `fileExternalIds` / `folderExternalIds` — what to share; provide at least one.
               - `expiresAt` (optional ISO 8601), `maxDownloads` (optional), `password` (optional).

               The tool returns the share's `externalId` and the public `url` to hand to the user.

               ## Managing share links

               - `list_share_links` (with `workspaceExternalId`) lists every share link in the workspace.
               - `get_share_link` (with `workspaceExternalId` and `shareLinkExternalId`) returns one link's
                 details, including which files and folders it shares and excludes.
               - `update_share_link` (with `workspaceExternalId` and `shareLinkExternalId`) changes a link's
                 settings. Only the fields you choose are touched — anything left out is kept. Pass `name` to
                 rename. For the nullable settings, set the matching flag and provide a value to set it, or set
                 the flag and leave the value empty to clear it:
                 `shouldUpdateExpiry` + `expiresAt` (ISO 8601; empty = no expiry),
                 `shouldUpdateMaxDownloads` + `maxDownloads` (empty = unlimited),
                 `shouldUpdatePassword` + `password` (empty = no password).
               - `delete_share_link` (with `workspaceExternalId` and `shareLinkExternalId`) deletes a link;
                 its public URL stops working but the shared files and folders are left intact.

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
