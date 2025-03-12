namespace PlikShare.Core.Utils;

public class SizeInBytes
{
    public const int Mb = 1024 * 1024;

    public static long Mbs(int mbs)
    {
        return mbs * Mb;
    }

    public static decimal AsMb(long sizeInBytes)
    {
        return (decimal)sizeInBytes / Mb;
    }
}