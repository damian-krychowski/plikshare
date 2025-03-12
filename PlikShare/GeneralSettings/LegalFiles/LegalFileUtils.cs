namespace PlikShare.GeneralSettings.LegalFiles;

public static class LegalFileUtils
{
    public static FileDetails GetFileDetails(
        string fileName,
        string legalVolumeDirectoryPath)
    {
        return new FileDetails(
            VolumeFilePath: Path.Combine(legalVolumeDirectoryPath, fileName));
    }
    
    public static string GetFileName(
        IFormFile file)
    {
        return Path.GetFileNameWithoutExtension(file.FileName) +
               Path.GetExtension(file.FileName);
    }
    
    public static async Task CreateFile(
        IFormFile file, 
        string path)
    {
        await using var wwwStream = new FileStream(
            path, 
            FileMode.Create);

        await file.CopyToAsync(wwwStream);
    }
    
    public readonly record struct FileDetails(
        string VolumeFilePath);
}