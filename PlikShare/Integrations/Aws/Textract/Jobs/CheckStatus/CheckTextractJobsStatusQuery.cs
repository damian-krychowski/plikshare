using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Aws.Textract.Id;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus.Contracts;

namespace PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus;

public class CheckTextractJobsStatusQuery(PlikShareDb plikShareDb)
{
    public CheckTextractJobsStatusResponseDto Execute(
        int workspaceId,
        CheckTextractJobsStatusRequestDto request)
    {
        using var connection = plikShareDb.OpenConnection();

        var pendingItems = connection
            .Cmd(
                sql: @"
                    SELECT 
                        itj_external_id,
                        itj_status
                    FROM itj_integrations_textract_jobs
                    WHERE
                        itj_original_workspace_id = $workspaceId
                        AND itj_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                ",
                readRowFunc: reader => new TextractJobStatusItemDto
                {
                    ExternalId = reader.GetExtId<TextractJobExtId>(0),
                    Status = reader.GetEnum<TextractJobStatus>(1)
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$externalIds", request.ExternalIds)
            .Execute();

        if (pendingItems.Count < request.ExternalIds.Count)
        {
            //if some items are missing we assume, that it was present there before
            //but once completed, they were removed from the list
            //so we return their status as completed.
            //if the external id never existed - why user would send it? too bad for him
            //we will tell him its completed anyway

            var missingExternalIds = request
                .ExternalIds
                .Except(pendingItems.Select(pi => pi.ExternalId));

            foreach (var missingExternalId in missingExternalIds)
            {
                pendingItems.Add(new TextractJobStatusItemDto
                {
                    ExternalId = missingExternalId,
                    Status = TextractJobStatus.Completed
                });
            }
        }

        return new CheckTextractJobsStatusResponseDto
        {
            Items = pendingItems
        };
    }
}