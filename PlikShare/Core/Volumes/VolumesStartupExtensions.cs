using Serilog;

namespace PlikShare.Core.Volumes;

public static class VolumesStartupExtensions
{
    public static void UseVolumes(this WebApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var volumesPath = GetConfigOrThrow(app, "Volumes:Path");
        var mainVolumePath = GetConfigOrThrow(app, "Volumes:Main:Path");
        var sqlitePathInVolume = GetConfigOrThrow(app, "Volumes:Main:SQLitePath");
        var legalPathInVolume = GetConfigOrThrow(app, "Volumes:Main:LegalPath");
        var otherVolumePaths = GetOtherVolumesFromConfig(app);

        Log.Information("[SETUP] Volumes initialization started. Original Volume paths: " +
                        "volumes: '{VolumesOriginalPath}' " +
                        "main: '{MainVolumePath}' " +
                        "sqlite: '{SQLiteOriginalPath}' " +
                        "legal: '{LegalOriginalPath}' " +
                        "other volumes: '{@OtherVolumes}'",
            volumesPath, mainVolumePath, sqlitePathInVolume, legalPathInVolume, otherVolumePaths);
        
        try
        {
            var volumesLocation = CreateLocation(
                volumesPath);

            var mainVolumeLocation = CreateLocation(
                baseLocation: volumesLocation,
                path: mainVolumePath);
            
            var sqliteLocation = CreateLocation(
                baseLocation: mainVolumeLocation,
                path: sqlitePathInVolume);
            
            var legalLocation = CreateLocation(
                baseLocation: mainVolumeLocation,
                path: legalPathInVolume);

            var otherVolumes = CreateOtherVolumes(
                baseLocation: volumesLocation,
                paths: otherVolumePaths);

            Log.Information("[SETUP] Volume paths setup finished. Original paths: " +
                            "volume: '{VolumeOriginalPath}' " +
                            "sqlite: '{SQLiteOriginalPath}' " +
                            "legal: '{LegalOriginalPath}'. Normalized paths: " +
                            "sqlite: '{SQLiteLocation}' " +
                            "legal: '{LegalLocation}' " +
                            "other volumes: '{@OtherVolumes}'",
                volumesPath, sqlitePathInVolume, legalPathInVolume, sqliteLocation, legalLocation, otherVolumes);
            
            app.Services.AddSingleton(new Volumes(
                Location: volumesLocation,
                Main: new MainVolume(
                    Location: mainVolumeLocation,
                    SQLite: sqliteLocation,
                    Legal: legalLocation),
                Others: otherVolumes));
        }
        catch (Exception e)
        {
            Log.Error(e, "[SETUP] Volume paths setup failed. Original paths: " +
                         "volume: '{VolumeOriginalPath}' " +
                         "sqlite: '{SQLiteOriginalPath}' " +
                         "legal: '{LegalOriginalPath}'",
                volumesPath, sqlitePathInVolume, legalPathInVolume);

            throw;
        }
    }

    private static OtherVolume[] CreateOtherVolumes(
        Location baseLocation,
        IEnumerable<string> paths)
    {
        var otherVolumes = new List<OtherVolume>();

        foreach (var path in paths)
        {
            var location = CreateLocation(
                baseLocation: baseLocation,
                path: path);
            
            otherVolumes.Add(new OtherVolume(
                Location: location));
        }

        return otherVolumes.ToArray();
    }
    
    private static Location CreateLocation(Location baseLocation, string path)
    {
        var location = baseLocation.Combine(
            path: path);
            
        Directory.CreateDirectory(
            location.FullPath);
        
        return location;
    }
    
    private static Location CreateLocation(string path)
    {
        var location = Location.Create(
            path: path);

        Directory.CreateDirectory(
            location.FullPath);
        
        return location;
    }

    private static string GetConfigOrThrow(WebApplicationBuilder app, string configPath)
    {
        return app.Configuration[configPath] ?? throw new ArgumentNullException(
            $"'{configPath}' is not defined in appsettings. Please make sure to provide needed configuration.");
    }

    private static List<string> GetOtherVolumesFromConfig(WebApplicationBuilder app)
    {
        var otherVolumes = new List<string>();
        var index = 0;

        while (true)
        {
            var otherPath = app.Configuration[$"Volumes:Other:{index}:Path"];
            if (string.IsNullOrEmpty(otherPath))
                break;

            index++;

            otherVolumes.Add(otherPath);
        }

        return otherVolumes;
    }
}