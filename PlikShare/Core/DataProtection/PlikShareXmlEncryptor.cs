using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using PlikShare.Core.Encryption;

#pragma warning disable CS8604 // Possible null reference argument.

namespace PlikShare.Core.DataProtection;

public class PlikShareXmlEncryptor(IMasterDataEncryption masterDataEncryption) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var elementString = plaintextElement.ToString(SaveOptions.DisableFormatting);
        var encryptedValue = masterDataEncryption.EncryptToBase64(elementString);

        var encryptedElement = new XElement("encryptedData",
            new XAttribute("decryptorType", typeof(PlikShareXmlEncryptor).AssemblyQualifiedName),
            new XElement("encryptedValue", encryptedValue)
        );

        return new EncryptedXmlInfo(encryptedElement, typeof(PlikShareXmlDecryptor));
    }
}