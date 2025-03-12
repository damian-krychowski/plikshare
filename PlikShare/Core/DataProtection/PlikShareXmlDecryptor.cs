using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using PlikShare.Core.Encryption;

namespace PlikShare.Core.DataProtection;

public class PlikShareXmlDecryptor(IServiceProvider serviceProvider) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var encryptedValue = encryptedElement.Element("encryptedValue")?.Value;
        
        if (string.IsNullOrEmpty(encryptedValue))
        {
            throw new InvalidOperationException("Encrypted value is missing or empty.");
        }

        var dataEncryption = serviceProvider.GetRequiredService<IMasterDataEncryption>();
        
        var decryptedString = dataEncryption.DecryptFromBase64(
            encryptedValue);
        
        return XElement.Parse(
            decryptedString);
    }
}