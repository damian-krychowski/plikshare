using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

public class UserWorkspaceEncryptionSessionsLoader(
    GetUserWrappedWorkspaceDeksQuery getUserWrappedWorkspaceDeksQuery)
{
    public AllUserWorkspaceEncryptionSessions LoadForUser(
        UserContext user,
        SecureBytes privateKey)
    {
        var rows = getUserWrappedWorkspaceDeksQuery.GetAllWrappedDeksForUser(user.Id);

        if (rows.Count == 0)
            return AllUserWorkspaceEncryptionSessions.Empty;

        var sessionsByInternalId = new Dictionary<int, WorkspaceEncryptionSession>();

        var workspaceGroups = rows.GroupBy(r => r.WorkspaceId);

        foreach (var group in workspaceGroups)
        {
            var entries = new List<WorkspaceDekEntry>(capacity: 4);
            var failed = false;

            foreach (var row in group)
            {
                try
                {
                    var dek = UserKeyPair.OpenSealed(
                        recipientPrivateKey: privateKey,
                        @sealed: row.WrappedDek);

                    entries.Add(new WorkspaceDekEntry
                    {
                        StorageDekVersion = row.StorageDekVersion,
                        Dek = dek
                    });
                }
                catch (Exception e)
                {
                    Log.Warning(e,
                        "Failed to unseal Workspace DEK v{Version} for User#{UserId} on Workspace#{WorkspaceId}; skipping workspace.",
                        row.StorageDekVersion, user.Id, row.WorkspaceId);

                    failed = true;
                    break;
                }
            }

            if (failed || entries.Count == 0)
            {
                foreach (var entry in entries)
                    entry.Dek.Dispose();
                continue;
            }

            var session = new WorkspaceEncryptionSession(
                workspaceId: group.Key,
                entries: entries.ToArray());

            sessionsByInternalId[group.Key] = session;
        }

        return new AllUserWorkspaceEncryptionSessions(
            sessionsByInternalId: sessionsByInternalId);
    }
}
