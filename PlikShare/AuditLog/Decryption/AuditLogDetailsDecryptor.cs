using System.Text.Json.Nodes;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.AuditLog.Decryption;

public class AuditLogDetailsDecryptor
{
    public string Decrypt(
        string detailsJson,
        WorkspaceExtId entryWorkspaceExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(detailsJson);
        }
        catch (Exception e)
        {
            Log.Warning(e,
                "Failed to parse audit log details JSON for workspace '{WorkspaceExternalId}'; returning unchanged.",
                entryWorkspaceExternalId);
            return detailsJson;
        }

        if (root is null) return detailsJson;


        WalkAndDecrypt(root, workspaceEncryptionSession);

        return root.ToJsonString(Json.Options);
    }

    private static void WalkAndDecrypt(
        JsonNode node, 
        WorkspaceEncryptionSession? session)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    var child = obj[key];

                    if (child is JsonValue value && TryRewriteEncrypted(value, session, out var replacement))
                        obj[key] = replacement;
                    else if (child is not null)
                        WalkAndDecrypt(child, session);
                }
                break;

            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];

                    if (child is JsonValue value && TryRewriteEncrypted(value, session, out var replacement))
                        arr[i] = replacement;
                    else if (child is not null)
                        WalkAndDecrypt(child, session);
                }
                break;
        }
    }

    private static bool TryRewriteEncrypted(
        JsonValue value,
        WorkspaceEncryptionSession? session,
        out JsonValue replacement)
    {
        replacement = value;

        if (!value.TryGetValue<string>(out var s) || s is null)
            return false;

        if (!s.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, StringComparison.Ordinal))
            return false;

        string rewritten;
        if (session is null)
        {
            rewritten = "[encrypted]";
        }
        else
        {
            try
            {
                rewritten = session.DecodeEncryptableMetadata(new EncodedMetadataValue(s));
            }
            catch (Exception e)
            {
                Log.Warning(e,
                    "Failed to decrypt audit log metadata value for Workspace#{WorkspaceId}; replacing with [encrypted].",
                    session.WorkspaceId);
                rewritten = "[encrypted]";
            }
        }

        replacement = JsonValue.Create(rewritten)!;
        return true;
    }
}
