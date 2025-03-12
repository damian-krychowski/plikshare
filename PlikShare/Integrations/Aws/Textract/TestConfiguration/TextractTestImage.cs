using System.Reflection;

namespace PlikShare.Integrations.Aws.Textract.TestConfiguration
{
    public static class TextractTestImage
    {
        private static readonly byte[] ImageBytes;

        static TextractTestImage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "PlikShare.Integrations.Aws.Textract.TestConfiguration.plikshare_is_the_best.png";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
                throw new InvalidOperationException(
                    "Aws Textract test image embedded resource was not found");

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            ImageBytes = memoryStream.ToArray();
        }

        public static byte[] GetBytes() => ImageBytes;
    }
}
