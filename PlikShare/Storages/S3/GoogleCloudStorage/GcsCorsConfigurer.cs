using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace PlikShare.Storages.S3.GoogleCloudStorage;

/// <summary>
/// Sets CORS on a GCS bucket via the interop XML API. Standalone (no AWSSDK) because
/// GCS uses a CORS XML schema that is not S3-compatible (root <c>&lt;CorsConfig&gt;</c>
/// with <c>&lt;Origins&gt;/&lt;Methods&gt;/&lt;ResponseHeaders&gt;/&lt;MaxAgeSec&gt;</c>),
/// so the SDK's <c>PutCORSConfigurationRequest</c> serializer produces a body GCS
/// rejects with <c>MalformedBucketConfiguration</c>. We sign manually with AWS
/// Signature V4 (service=<c>s3</c>, region=<c>auto</c>) — the same auth GCS interop
/// already accepts for the rest of our SDK-driven calls.
///
/// NOTE on ResponseHeaders: in GCS CORS, &lt;ResponseHeader&gt; entries control BOTH
/// the headers exposed to JS (Access-Control-Expose-Headers) AND the headers the
/// browser is allowed to send on the actual request (Access-Control-Allow-Headers
/// on the preflight response). Signed PUTs include Content-Type in SignedHeaders,
/// so Content-Type MUST be listed here — otherwise preflight fails with the
/// generic "No Access-Control-Allow-Origin" error.
/// </summary>
public static class GcsCorsConfigurer
{
    private const string Endpoint = "storage.googleapis.com";
    private const string Region = "auto";
    private const string Service = "s3";

    private static readonly HttpClient Http = new();

    public static async Task PutBucketCorsAsync(
        string accessKey,
        string secretKey,
        string bucketName,
        string allowedOrigin,
        CancellationToken cancellationToken = default)
    {
        var origin = new Uri(allowedOrigin).GetLeftPart(UriPartial.Authority);

        var xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<CorsConfig><Cors>" +
            $"<Origins><Origin>{origin}</Origin></Origins>" +
            "<Methods><Method>GET</Method><Method>PUT</Method></Methods>" +
            "<ResponseHeaders>" +
                "<ResponseHeader>Content-Type</ResponseHeader>" +
                "<ResponseHeader>ETag</ResponseHeader>" +
            "</ResponseHeaders>" +
            "<MaxAgeSec>3600</MaxAgeSec>" +
            "</Cors></CorsConfig>";

        var body = Encoding.UTF8.GetBytes(xml);
        var bodySha256Hex = ToHex(SHA256.HashData(body));

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        // V4 canonical request — headers we sign must match what HttpClient sends.
        // Host is auto-added by HttpClient from the URI; the rest we add explicitly.
        var canonicalUri = $"/{bucketName}";
        var canonicalQuery = "cors=";

        var signedHeaderMap = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["content-type"] = "application/xml",
            ["host"] = Endpoint,
            ["x-amz-content-sha256"] = bodySha256Hex,
            ["x-amz-date"] = amzDate,
        };

        var canonicalHeaders = string.Concat(
            signedHeaderMap.Select(kv => $"{kv.Key}:{kv.Value.Trim()}\n"));
        var signedHeaders = string.Join(";", signedHeaderMap.Keys);

        var canonicalRequest =
            $"PUT\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{bodySha256Hex}";
        var canonicalRequestHash = ToHex(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var stringToSign =
            $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        // Signing key chain: kSecret → kDate → kRegion → kService → kSigning.
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HmacSha256(dateStamp, kSecret);
        var kRegion = HmacSha256(Region, kDate);
        var kService = HmacSha256(Service, kRegion);
        var kSigning = HmacSha256("aws4_request", kService);
        var signature = ToHex(HmacSha256(stringToSign, kSigning));

        var authHeader =
            $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, " +
            $"SignedHeaders={signedHeaders}, Signature={signature}";

        var url = $"https://{Endpoint}{canonicalUri}?cors";

        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", bodySha256Hex);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);

        using var response = await Http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);

            Log.Error(
                "[S3:GOOGLE_CLOUD_STORAGE] CORS PUT failed for '{BucketName}'. Status: {Status}. Body: {Body}",
                bucketName, response.StatusCode, errBody);

            throw new InvalidOperationException(
                $"GCS CORS PUT failed for '{bucketName}': {(int)response.StatusCode} {response.ReasonPhrase}. {errBody}");
        }

        Log.Information(
            "[S3:GOOGLE_CLOUD_STORAGE] CORS for Bucket '{BucketName}' was set. AllowedOrigin '{AllowedOrigin}'",
            bucketName, origin);
    }

    private static byte[] HmacSha256(string data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}