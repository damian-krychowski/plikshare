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
                 version: 0.1.0
               allowed-tools: list_workspaces create_folder rename_folder
               ---

               # PlikShare

               PlikShare organizes files into workspaces (private file trees). Folders live inside a
               workspace and can be nested. You act through the PlikShare agent tools and can only
               touch workspaces you have been granted access to.

               ## Finding a workspace

               You always need a `workspaceExternalId` (format `w_...`) before working with folders.
               Call the `list_workspaces` tool to see the workspaces you can access, then pick one. If
               the list is empty, ask an administrator to grant your agent access to a workspace.

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
