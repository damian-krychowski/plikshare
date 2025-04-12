using System.Text;
using PlikShare.Core.Configuration;
namespace PlikShare.Widgets;
public static class WidgetEndpoints
{
    public static void MapWidgetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/widgets")
            .WithTags("Widgets")
            .AllowAnonymous()
            .RequireCors("AllowAll");

        group.MapGet("/scripts", GetWidgetScripts)
            .WithName("GetWidgetScripts");
    }

    private static IResult GetWidgetScripts(
        HttpContext httpContext,
        IWebHostEnvironment webHostEnvironment,
        IConfig config)
    {
        try
        {
            var elementsPath = Path.Combine(webHostEnvironment.WebRootPath, "elements");
            if (!Directory.Exists(elementsPath))
            {
                return Results.NotFound($"Directory not found: {elementsPath}");
            }

            // Get all files from the directory
            var files = Directory.GetFiles(elementsPath);

            // Create base URL for resources
            var baseUrl = $"{config.AppUrl}/elements";

            // Get filenames
            var fileNames = files.Select(Path.GetFileName).ToList();

            // Look for the merged elements file (starts with "elements" and ends with ".js")
            var elementsFile = fileNames.FirstOrDefault(f => f.StartsWith("elements") && f.EndsWith(".js"));

            // Look for stylesheet
            var stylesFile = fileNames.FirstOrDefault(f => f.EndsWith(".css"));

            // Build the HTML string
            var stringBuilder = new StringBuilder();

            // Add stylesheet if exists
            if (stylesFile != null)
            {
                stringBuilder.AppendLine($"<link rel=\"stylesheet\" href=\"{baseUrl}/{stylesFile}\">");
            }

            // Add the merged elements script
            if (elementsFile != null)
            {
                stringBuilder.AppendLine($"<script src=\"{baseUrl}/{elementsFile}\"></script>");
            }

            return Results.Content(stringBuilder.ToString(), "text/plain");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error getting element files: {ex.Message}");
        }
    }
}