using Serilog;

namespace PlikShare.Core.Encryption;

public static class MasterEncryptionStartupExtensions
{
    public static void UseMasterDataEncryption(this WebApplicationBuilder app)
    {
        var encryptionPasswords = app.Configuration.GetValue<string>(
            "EncryptionPasswords");

        if (encryptionPasswords is null)
            throw new InvalidOperationException("EncryptionPasswords app setting is required for application to run");
        
        var passwords = encryptionPasswords.Split(",");

        var encryptionKeyProvider = new MasterEncryptionKeyProvider(
            passwords);

        var masterDataEncryption = new AesGcmMasterDataEncryption(
            masterEncryptionKeyProvider: encryptionKeyProvider);

        var masterDataEncryptionBufferedFactory = new MasterDataEncryptionBufferedFactory(
            masterDataEncryption: masterDataEncryption,
            bufferSize: 15);

        app.Services.AddSingleton(encryptionKeyProvider);
        app.Services.AddSingleton<IMasterDataEncryption>(masterDataEncryption);
        app.Services.AddSingleton(masterDataEncryptionBufferedFactory);
        
        Log.Information("[SETUP] Data encryption setup finished.");
    }
}