namespace PlikShare.Trash;

// Trash retention. Three states:
//   Enabled = false                    → trash off; deletes are immediate and the sweeper
//                                        purges any leftover trashed files (retention 0 days).
//   Enabled = true,  RetentionDays = N → trashed files auto-purged N days after deletion.
//   Enabled = true,  RetentionDays null → trashed files kept forever, never auto-purged.
public sealed record TrashPolicy(bool Enabled, int? RetentionDays)
{
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 3650;

    public static readonly TrashPolicy Disabled = new(false, null);

    public static bool TryCreate(
        bool enabled,
        int? retentionDays,
        out TrashPolicy policy)
    {
        if (!enabled)
        {
            policy = Disabled;
            return true;
        }

        // Enabled: a null retentionDays means "keep forever"; a provided value must be in range.
        if (retentionDays is null or (>= MinRetentionDays and <= MaxRetentionDays))
        {
            policy = new TrashPolicy(Enabled: true, RetentionDays: retentionDays);
            return true;
        }

        policy = Disabled;
        return false;
    }

    // When a file trashed at deletedAt is expected to be permanently removed by the sweeper.
    // null only for the "keep forever" policy.
    public DateTimeOffset? AutoDeleteMoment(DateTimeOffset deletedAt)
    {
        if (!Enabled)
            return deletedAt; // disabled → purged at the next sweep

        if (RetentionDays is null)
            return null; // enabled, no limit → never

        return deletedAt.AddDays(RetentionDays.Value); // enabled → N days after deletion
    }
}
