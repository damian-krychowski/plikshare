using PlikShare.Core.Volumes;
using Serilog;

namespace PlikShare.GeneralSettings.LegalFiles.DeleteLegalFile;

public class DeleteLegalFileOperation(
    Volumes volumes,
    AppSettings appSettings)
{
    public void ExecuteForTermsOfService()
    {
        DeleteFileIfExists(
            fileName: appSettings.TermsOfService.FileName);

        appSettings.SetTermsOfService(null);
    }

    public void ExecuteForPrivacyPolicy()
    {
        DeleteFileIfExists(
            fileName: appSettings.PrivacyPolicy.FileName);
        
        appSettings.SetPrivacyPolicy(null);
    }
    
    private void DeleteFileIfExists(string? fileName)
    {
        if (fileName is null) return;
        
        var fileDetails = LegalFileUtils.GetFileDetails(
            fileName,
            volumes.Main.Legal.FullPath);
            
        File.Delete(fileDetails.VolumeFilePath);

        Log.Information("Legal file was deleted: '{VolumeFilePath}'",
            fileDetails.VolumeFilePath);
    }
}