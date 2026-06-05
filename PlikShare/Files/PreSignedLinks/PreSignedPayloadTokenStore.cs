using System.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using PlikShare.Core.Utils;

namespace PlikShare.Files.PreSignedLinks;

public sealed class PreSignedPayloadTokenStore(
    IMemoryCache cache,
    IDataProtectionProvider dataProtectionProvider)
{
    private const string TokenProtectionPurpose = "PresignedToken";

    public string Store<TPayload>(
        TPayload payload,
        DateTimeOffset expiresAt)
        where TPayload : PreSignedUrlsService.PreSignedPayload
    {
        var token = Guid.NewGuid().ToBase62();

        cache.Set(
            key: CacheKey(token),
            value: payload,
            absoluteExpiration: expiresAt);

        return Protect(token);
    }

    public TPayload? TryGet<TPayload>(string protectedToken)
        where TPayload : PreSignedUrlsService.PreSignedPayload
    {
        var token = Unprotect(protectedToken);

        if (token is null)
            return null;

        return cache.TryGetValue(CacheKey(token), out TPayload? payload)
            ? payload
            : null;
    }

    private string Protect(string token)
    {
        var protector = dataProtectionProvider.CreateProtector(
            TokenProtectionPurpose);

        return HttpUtility.UrlEncode(
            protector.Protect(token));
    }

    private string? Unprotect(string protectedTokenUrlEncoded)
    {
        try
        {
            var protector = dataProtectionProvider.CreateProtector(
                TokenProtectionPurpose);

            var protectedToken = HttpUtility.UrlDecode(
                protectedTokenUrlEncoded);

            return protector.Unprotect(
                protectedToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string CacheKey(string token) => $"presigned-token:{token}";
}
