using PlikShare.Core.Volumes;

namespace PlikShare.Storages.HardDrive.GetVolumes;

public class GetHardDriveVolumesOperation
{
    private readonly Volumes _volumes;

    public GetHardDriveVolumesOperation(Volumes volumes)
    {
        _volumes = volumes;
    }

    public Result Execute()
    {
        return new Result(
            Volumes:
            [
                new Volume(
                    Path: _volumes.Main.Location.LinuxFormatPath(),
                    RestrictedFolderPaths:
                    [
                        _volumes.Main.SQLite.LinuxFormatPath(),
                        _volumes.Main.Legal.LinuxFormatPath()
                    ]),
                
                .._volumes.Others.Select(other => new Volume(
                    Path: other.Location.LinuxFormatPath(),
                    RestrictedFolderPaths: []))
            ]);
    }

    public record Result(
        Volume[] Volumes);
    
    public record Volume(
        string Path,
        string[] RestrictedFolderPaths);
}