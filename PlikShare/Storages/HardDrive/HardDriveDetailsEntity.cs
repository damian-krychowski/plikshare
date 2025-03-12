namespace PlikShare.Storages.HardDrive;

public record HardDriveDetailsEntity(
    string VolumePath,
    string FolderPath,
    string FullPath);