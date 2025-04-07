using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Workspaces.Members.CountAll;

public class CountWorkspaceTotalTeamMembersQuery(PlikShareDb plikShareDb)
{
    public Result Execute(
        int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .OneRowCmd(
                sql: """
                     SELECT 
                     (
                         SELECT COUNT(*)
                         FROM wm_workspace_membership
                         WHERE wm_workspace_id = $workspaceId
                     ) AS team_members_count,
                     (
                         SELECT COUNT(*)
                         FROM bm_box_membership
                         INNER JOIN bo_boxes
                             ON bo_id = bm_box_id
                         WHERE bo_workspace_id = $workspaceId
                     ) AS boxes_team_members_count
                     """,
                readRowFunc: reader => new Result(
                    TeamMembersCount: reader.GetInt32(0),
                    BoxesTeamMembersCount: reader.GetInt32(1)))
            .WithParameter("$workspaceId", workspaceId)
            .ExecuteOrThrow();
    }

    public readonly record struct Result(
        int TeamMembersCount,
        int BoxesTeamMembersCount)
    {
        public int TotalCount => TeamMembersCount + BoxesTeamMembersCount;
    };
}