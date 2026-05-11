namespace PlikShare.Users.StorageAccess;

public enum UserStorageAccessMode
{
    /// <summary>Full access; the storage list is ignored.</summary>
    All = 0,

    /// <summary>Whitelist; only storages listed are usable.</summary>
    AllowOnly = 1,

    /// <summary>Blacklist; every storage except those listed is usable.</summary>
    AllowAllExcept = 2,
}
