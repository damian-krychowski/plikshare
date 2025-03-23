using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.GeneralSettings.SignUpCheckboxes.Delete;

public class DeleteSignUpCheckboxQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        int signUpCheckboxId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                signUpCheckboxId: signUpCheckboxId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int signUpCheckboxId)
    {
        try
        {
            dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM suc_sign_up_checkboxes
                         WHERE suc_id = $id
                         RETURNING suc_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$id", signUpCheckboxId)
                .Execute();
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            // Motivation:
            // If delete failed it means there are already some records in usuc_user_sign_up_checkboxes table
            // it means someone has already registered accepting given checkbox - and we do not want to lose the shape
            // of that checkbox at that particular moment - so that we know to each checkbox that user agreed to.
            // So we leave the checkbox in the database and we only mark it as deleted
            dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE suc_sign_up_checkboxes
                         SET suc_is_deleted = TRUE
                         WHERE suc_id = $id
                         RETURNING suc_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$id", signUpCheckboxId)
                .Execute();
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while deleting SingUpCheckbox#{SignUpCheckboxId}",
                signUpCheckboxId);

            throw;
        }
    }
}