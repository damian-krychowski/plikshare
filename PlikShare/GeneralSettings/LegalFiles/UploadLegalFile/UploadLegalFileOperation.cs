using PlikShare.Core.Volumes;
using Serilog;

namespace PlikShare.GeneralSettings.LegalFiles.UploadLegalFile;

public class UploadLegalFileOperation
{
    private readonly Volumes _volumes;
    private readonly AppSettings _appSettings;

    public UploadLegalFileOperation(
        Volumes volumes,
        AppSettings appSettings)
    {
        _volumes = volumes;
        _appSettings = appSettings;
    }

    public async Task ExecuteForTermsOfService(
        IFormFile file)
    {
        var fileName = await ReplaceFile(
            currentFileName: _appSettings.TermsOfService.FileName,
            newFile: file);

        _appSettings.SetTermsOfService(
            fileName);
    }

    public async Task ExecuteForPrivacyPolicy(
        IFormFile file)
    {
        var fileName = await ReplaceFile(
            currentFileName: _appSettings.PrivacyPolicy.FileName,
            newFile: file);
        
        _appSettings.SetPrivacyPolicy(
            fileName);
    }
    
    private async Task<string> ReplaceFile(
        string? currentFileName,
        IFormFile newFile)
    {
        if (currentFileName is not null)
        {
            var currentFileDetails = LegalFileUtils.GetFileDetails(
                currentFileName,
                _volumes.Main.Legal.FullPath);
            
            File.Delete(currentFileDetails.VolumeFilePath);
            
            Log.Information("Current legal file was deleted: '{VolumeFilePath}'",
                currentFileDetails.VolumeFilePath);
        }
        
        var fileName = LegalFileUtils.GetFileName(
            newFile);
        
        var fileDetails = LegalFileUtils.GetFileDetails(
            fileName,
            _volumes.Main.Legal.FullPath);

        await LegalFileUtils.CreateFile(
            newFile,
            fileDetails.VolumeFilePath);
        
        Log.Information("New legal file was created: '{VolumeFilePath}'",
            fileDetails.VolumeFilePath);

        return fileName;
    }
}