using Amazon.S3;
using Amazon.S3.Model;

namespace PlikShare.Storages;

public static class S3LifecycleRules
{
    /// <summary>
    /// Universally useful on any S3-compatible storage: clean up zombie multipart uploads
    /// (parts uploaded without a matching CompleteMultipartUpload or AbortMultipartUpload).
    /// On AWS S3, DigitalOcean Spaces, and Backblaze B2 these incomplete uploads are billed
    /// as storage. Cloudflare R2 already aborts them automatically after 7 days, so there
    /// the rule is redundant but harmless.
    /// </summary>
    public static LifecycleRule AbortIncompleteMultipartUploadsAfter7Days => new()
    {
        Id = "abort-incomplete-multipart-uploads-after-7-days",
        Status = LifecycleRuleStatus.Enabled,
        Filter = new LifecycleFilter
        {
            LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = "" }
        },
        AbortIncompleteMultipartUpload = new LifecycleRuleAbortIncompleteMultipartUpload
        {
            DaysAfterInitiation = 7
        }
    };

    /// <summary>
    /// For versioning-enabled buckets: permanently delete noncurrent object versions after
    /// 1 day. Required on Backblaze B2 because its S3-compatible API always has versioning
    /// enabled — a plain DELETE there creates a hide marker and turns the previous version
    /// into a noncurrent version that lingers (and is billed) forever.
    ///
    /// We deliberately do NOT include <c>Expiration.ExpiredObjectDeleteMarker = true</c>
    /// here. AWS S3 (and providers that mirror its validator, including B2) reject
    /// standalone marker-cleanup with: <c>"has an ExpiredObjectDeleteMarker rule but there
    /// is no Expiration rule with the exact same prefix"</c> — the validator demands a
    /// sibling <c>Expiration { Days/Date }</c> rule on the same prefix, which would
    /// auto-delete current user files. Combining marker cleanup into the same rule as
    /// <c>NoncurrentVersionExpiration</c> also fails: the validator does not count
    /// noncurrent expiration as a satisfying "Expiration rule." Consequence: orphan
    /// delete markers accumulate on B2. Tests handle teardown via <c>S3HardPurge</c>;
    /// production bucket deletion on B2 with accumulated markers is a known limitation.
    /// </summary>
    public static LifecycleRule DeleteNoncurrentVersionsAfter1Day => new()
    {
        Id = "delete-noncurrent-versions-after-1-day",
        Status = LifecycleRuleStatus.Enabled,
        Filter = new LifecycleFilter
        {
            LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = "" }
        },
        NoncurrentVersionExpiration = new LifecycleRuleNoncurrentVersionExpiration
        {
            NoncurrentDays = 1
        }
    };
}