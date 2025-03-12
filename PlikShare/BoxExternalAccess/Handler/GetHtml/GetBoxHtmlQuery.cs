using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.BoxExternalAccess.Handler.GetHtml;

public class GetBoxHtmlQuery(PlikShareDb plikShareDb)
{
    public Result Execute(
        BoxContext box)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                    SELECT
                        CASE 
                            WHEN bo_header_is_enabled = TRUE THEN bo_header_html
                        END AS bo_header_html,
                        CASE 
                            WHEN bo_footer_is_enabled = TRUE THEN bo_footer_html
                        END AS bo_footer_html
                    FROM bo_boxes
                    WHERE bo_id = $boxId
                    LIMIT 1
                ",
                readRowFunc: reader => new BoxHtml(
                    Header: reader.GetStringOrNull(0),
                    Footer: reader.GetStringOrNull(1)))
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
            return new Result(Code: ResultCode.BoxNotFound);

        return new Result(
            Code: ResultCode.Ok,
            Html: result.Value);
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        BoxHtml? Html = default);
    
    public record BoxHtml(
        string? Header,
        string? Footer);
}