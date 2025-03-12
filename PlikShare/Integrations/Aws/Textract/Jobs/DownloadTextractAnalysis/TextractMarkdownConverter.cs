using System.Text;

namespace PlikShare.Integrations.Aws.Textract.Jobs.DownloadTextractAnalysis;

public static class TextractMarkdownConverter
{
    public static string ToMarkdown(
        TextractAnalysisResult textractAnalysis,
        TextractAnalysisDerivedInfo textractAnalysisDerivedInfo)
    {
        var markdown = new StringBuilder();

        foreach (var page in textractAnalysisDerivedInfo.Pages)
        {
            // Add page header
            markdown.AppendLine($"# Page {page.PageNumber}");
            markdown.AppendLine();

            // Process forms (key-value pairs)
            if (page.Forms.Any())
            {
                markdown.AppendLine("## Form Fields");
                markdown.AppendLine();

                foreach (var form in page.Forms)
                {
                    markdown.AppendLine($"- **{form.Key}**: {form.Value ?? "_empty_"}");
                }
                markdown.AppendLine();
            }

            // Process tables
            foreach (var table in page.Tables)
            {
                markdown.AppendLine("## Table");
                markdown.AppendLine();

                // Create a matrix to represent the table
                var matrix = new string[table.RowCount, table.ColumnCount];
                var columnWidths = new int[table.ColumnCount];

                // Fill the matrix and calculate column widths
                foreach (var cell in table.Cells)
                {
                    var content = cell.Content ?? "";
                    matrix[cell.RowIndex, cell.ColumnIndex] = content;

                    // Update column width if this cell's content is longer
                    columnWidths[cell.ColumnIndex] = Math.Max(
                        columnWidths[cell.ColumnIndex],
                        content.Length);
                }

                // Generate table markdown
                // Header row
                markdown.Append("|");
                for (int col = 0; col < table.ColumnCount; col++)
                {
                    markdown.Append($" {new string(' ', columnWidths[col])} |");
                }
                markdown.AppendLine();

                // Separator row
                markdown.Append("|");
                for (int col = 0; col < table.ColumnCount; col++)
                {
                    markdown.Append($" {new string('-', columnWidths[col])} |");
                }
                markdown.AppendLine();

                // Data rows
                for (int row = 0; row < table.RowCount; row++)
                {
                    markdown.Append("|");
                    for (int col = 0; col < table.ColumnCount; col++)
                    {
                        var content = matrix[row, col] ?? "";
                        var padding = new string(' ', columnWidths[col] - content.Length);
                        markdown.Append($" {content}{padding} |");
                    }
                    markdown.AppendLine();
                }
                markdown.AppendLine();
            }

            // Process regular text content
            if (page.Lines.Any())
            {
                markdown.AppendLine();

                foreach (var line in page.Lines)
                {
                    markdown.AppendLine(line.Text);
                }

                markdown.AppendLine();
            }

            // Add page separator
            markdown.AppendLine();
        }

        // Add any warnings at the end
        if (textractAnalysis.Warnings.Any())
        {
            markdown.AppendLine("## Processing Warnings");
            markdown.AppendLine();

            foreach (var warning in textractAnalysis.Warnings)
            {
                markdown.AppendLine($"- Error {warning.ErrorCode} on pages: {string.Join(", ", warning.Pages)}");
            }
        }

        return markdown.ToString();
    }
}