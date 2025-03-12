using System.Security.Cryptography;
using System.Web;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8601 // Possible null reference assignment.

namespace PlikShare.IntegrationTests.Infrastructure;

public class TotpCodes
{
    public static string Generate(string uri, DateTime? time = null)
    {
        // Decode the Base32 secret
        var uriParsed = TotpUriParser.ParseUri(uri);
        var key = Base32Decode(uriParsed.Secret);

        // Get the timestamp
        var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var counterTime = time ?? DateTime.UtcNow;
        var counter = (long)Math.Floor((counterTime - epochStart).TotalSeconds / 30);

        // Create counter bytes
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        // Calculate HMAC-SHA1
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        // Get offset
        var offset = hash[hash.Length - 1] & 0xf;

        // Generate 4-byte code
        var binary =
            (hash[offset] & 0x7f) << 24 |
            (hash[offset + 1] & 0xff) << 16 |
            (hash[offset + 2] & 0xff) << 8 |
            hash[offset + 3] & 0xff;

        // Get 6 digits
        var password = binary % 1000000;
        return password.ToString("D6");
    }

    private static byte[] Base32Decode(string base32)
    {
        // Remove padding if any
        base32 = base32.TrimEnd('=');

        // Base32 alphabet (RFC 4648)
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // Convert to uppercase for consistency
        base32 = base32.ToUpperInvariant();

        // Calculate output length
        var numBytes = base32.Length * 5 / 8;
        var result = new byte[numBytes];

        // Process each 8 characters (5 bytes)
        var bitBuffer = 0;
        var bitsRemaining = 0;
        var index = 0;

        foreach (var c in base32)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
                throw new ArgumentException("Invalid Base32 character.");

            bitBuffer = bitBuffer << 5 | value;
            bitsRemaining += 5;

            if (bitsRemaining >= 8)
            {
                result[index++] = (byte)(bitBuffer >> bitsRemaining - 8);
                bitsRemaining -= 8;
            }
        }

        return result;
    }

    public class TotpUriParser
    {
        public class TotpParameters
        {
            public string Issuer { get; set; }
            public string Account { get; set; }
            public string Secret { get; set; }
            public int Digits { get; set; }
            public string Algorithm { get; set; }
            public int Period { get; set; }
        }

        public static TotpParameters ParseUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("URI cannot be empty", nameof(uri));

            if (!uri.StartsWith("otpauth://totp/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid TOTP URI format", nameof(uri));

            try
            {
                // Remove the prefix to get the path and query
                var remaining = uri.Substring("otpauth://totp/".Length);

                // Split into path and query
                var queryIndex = remaining.IndexOf('?');
                var path = queryIndex >= 0 ? remaining.Substring(0, queryIndex) : remaining;
                var query = queryIndex >= 0 ? remaining.Substring(queryIndex + 1) : string.Empty;

                // Parse the label (issuer:account or just account)
                var label = Uri.UnescapeDataString(path);
                string issuer = null!;
                var account = label;

                var colonIndex = label.IndexOf(':');
                if (colonIndex >= 0)
                {
                    issuer = label.Substring(0, colonIndex);
                    account = label.Substring(colonIndex + 1);
                }

                // Parse query parameters
                var queryParams = HttpUtility.ParseQueryString(query);

                return new TotpParameters
                {
                    Issuer = issuer ?? queryParams["issuer"],
                    Account = account,
                    Secret = queryParams["secret"],
                    Digits = ParseIntOrDefault(queryParams["digits"], 6),
                    Algorithm = queryParams["algorithm"] ?? "SHA1",
                    Period = ParseIntOrDefault(queryParams["period"], 30)
                };
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to parse TOTP URI", nameof(uri), ex);
            }
        }

        private static int ParseIntOrDefault(string value, int defaultValue)
        {
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
    }
}