using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate.Contracts;
using Serilog;

namespace PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate;

public class CreateOrUpdateSignUpCheckboxQuery(DbWriteQueue dbWriteQueue)
{
    public Task<CreateOrUpdateSignUpCheckboxResponseDto> Execute(
        CreateOrUpdateSignUpCheckboxRequestDto request,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                request: request),
            cancellationToken: cancellationToken);
    }

    private CreateOrUpdateSignUpCheckboxResponseDto ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        CreateOrUpdateSignUpCheckboxRequestDto request)
    {
        if (request.Id is null)
            return HandleNewCheckbox(
                dbWriteContext,
                request.Text,
                request.IsRequired);

        return HandleExistingCheckbox(
            dbWriteContext,
            request.Id.Value,
            request.Text,
            request.IsRequired);
    }

    private CreateOrUpdateSignUpCheckboxResponseDto HandleNewCheckbox(
        DbWriteQueue.Context dbWriteContext,
        string text,
        bool isRequired)
    {
        var id = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO suc_sign_up_checkboxes (
                         suc_text,
                         suc_is_required,
                         suc_is_deleted
                     ) 
                     VALUES (
                         $text,
                         $isRequired,
                         FALSE
                     )
                     RETURNING
                         suc_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$text", text)
            .WithParameter("$isRequired", isRequired)
            .ExecuteOrThrow();

        return new CreateOrUpdateSignUpCheckboxResponseDto
        {
            NewId = id
        };
    }

    private CreateOrUpdateSignUpCheckboxResponseDto HandleExistingCheckbox(
        DbWriteQueue.Context dbWriteContext,
        int id,
        string text,
        bool isRequired)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        var newId = -1;

        try
        {
            newId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO suc_sign_up_checkboxes (
                             suc_text,
                             suc_is_required,
                             suc_is_deleted
                         ) 
                         VALUES (
                             $text,
                             $isRequired,
                             FALSE
                         )
                         RETURNING
                             suc_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$text", text)
                .WithParameter("$isRequired", isRequired)
                .ExecuteOrThrow();

            dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM suc_sign_up_checkboxes
                         WHERE suc_id = $id
                         RETURNING suc_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$id", id)
                .Execute();

            transaction.Commit();

            return new CreateOrUpdateSignUpCheckboxResponseDto
            {
                NewId = newId
            };
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
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$id", id)
                .Execute();

            transaction.Commit();

            return new CreateOrUpdateSignUpCheckboxResponseDto
            {
                NewId = newId
            };
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating SingUpCheckbox#{SignUpCheckboxId}, Text: {SignUpCheckboxText}, IsRequired: {SignUpCheckboxIsRequired", 
                id,
                text,
                isRequired);

            throw;
        }
    }
}