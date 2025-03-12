namespace PlikShare.Core.Utils;

public static class FileNames
{
    public static FileNameParts ToNameAndExtension(string fullFileName)
    {
        if (string.IsNullOrEmpty(fullFileName))
        {
            throw new ArgumentException("Filename cannot be empty", nameof(fullFileName));
        }

        if (fullFileName.StartsWith('.') && !fullFileName[1..].Contains('.'))
        {
            return new FileNameParts(fullFileName, string.Empty);
        }

        var lastDotIndex = fullFileName.LastIndexOf('.');

        if (lastDotIndex <= 0)
        {
            return new FileNameParts(fullFileName, string.Empty);
        }

        return new FileNameParts(
            fullFileName[..lastDotIndex],
            fullFileName[lastDotIndex..]
        );
    }

    public static FileNameParts? TryGetNameAndExtension(string fullFileName)
    {
        if (string.IsNullOrEmpty(fullFileName))
            return null;

        if (fullFileName.StartsWith('.') && !fullFileName[1..].Contains('.'))
        {
            return new FileNameParts(fullFileName, string.Empty);
        }

        var lastDotIndex = fullFileName.LastIndexOf('.');

        if (lastDotIndex <= 0)
        {
            return new FileNameParts(fullFileName, string.Empty);
        }

        return new FileNameParts(
            fullFileName[..lastDotIndex],
            fullFileName[lastDotIndex..]
        );
    }
}

public record FileNameParts(
    string Name, 
    string Extension);