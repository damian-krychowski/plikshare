namespace PlikShare.Core.Volumes;

public record Volumes(
    Location Location,
    MainVolume Main,
    OtherVolume[] Others)
{
    public bool TryGetVolumeLocationByVolumePath(string volumePath, out Location location)
    {
        var volumeLocation = Location.Create(
            path: volumePath);

        if (volumeLocation == Main.Location)
        {
            location = Main.Location;
            return true;
        }

        foreach (var otherVolume in Others)
        {
            if (volumeLocation == otherVolume.Location)
            {
                location = otherVolume.Location;
                return true;
            }
        }

        location = null!;
        return false;
    }
}

public record MainVolume(
    Location Location,
    Location SQLite,
    Location Legal);

public record OtherVolume(
    Location Location);

public record Location(
    string Path,
    string FullPath)
{
    public string LinuxFormatPath()
    {
        return ToLinuxFormat(Path);
    }
    
    public static Location Create(string path)
    {
        var normalizedPath = NormalizePath(path);

        return new Location(
            Path: normalizedPath,
            FullPath: System.IO.Path.GetFullPath(normalizedPath));
    }

    public Location Combine(string path)
    {
        var normalizedPath = NormalizePath(path);

        return new Location(
            Path: System.IO.Path.Combine(Path, normalizedPath),
            FullPath: System.IO.Path.Combine(FullPath, normalizedPath));
    }

    public static string ToLinuxFormat(string path)
    {
        var segments = GetSegments(path);

        return string.Join("/", segments);
    }
    
    public static string Combine(string pathA, string pathB)
    {
        var normalizedA = NormalizePath(pathA);
        var normalizedB = NormalizePath(pathB);

        return System.IO.Path.Combine(
            normalizedA, 
            normalizedB);
    }
    
    public static string NormalizePath(string path)
    {
        var segments = GetSegments(path);
        return System.IO.Path.Combine(segments);
    }

    public static string[] GetSegments(string path)
    {
        return path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
    }
}
    
    
    