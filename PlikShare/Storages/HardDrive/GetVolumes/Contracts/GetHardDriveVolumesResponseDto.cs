namespace PlikShare.Storages.HardDrive.GetVolumes.Contracts;

public record GetHardDriveVolumesResponseDto(
    HardDriveVolumeItemDto[] Items);
    
public record HardDriveVolumeItemDto(
    string Path,
    string[] RestrictedFolderPaths);