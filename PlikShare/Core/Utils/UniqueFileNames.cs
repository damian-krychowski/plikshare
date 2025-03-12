namespace PlikShare.Core.Utils;

public class UniqueFileNames(int capacity)
{
    private readonly HashSet<string> _processedFiles = new(capacity, StringComparer.OrdinalIgnoreCase);

    public (string Path, bool WasCollisionDetected) EnsureUniqueFileName(
        string fullFileName,
        string? folderPath = null)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fullFileName));

        FileNameParts? fileNameParts = null;
        
        var fullPath = string.IsNullOrEmpty(folderPath)
            ? fullFileName
            : $"{folderPath}/{fullFileName}";

        var counter = 1;
        var finalPath = fullPath;
        var wasCollisionDetected = false;

        while (_processedFiles.Contains(finalPath))
        {
            fileNameParts ??= FileNames.ToNameAndExtension(
                fullFileName: fullFileName);

            wasCollisionDetected = true;
            var newFileName = $"{fileNameParts.Name} ({counter}){fileNameParts.Extension}";

            finalPath = string.IsNullOrEmpty(folderPath)
                ? newFileName
                : $"{folderPath}/{newFileName}";

            counter++;
        }

        _processedFiles.Add(finalPath);

        return (finalPath, wasCollisionDetected);
    }
}