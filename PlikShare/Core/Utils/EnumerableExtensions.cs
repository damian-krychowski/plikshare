namespace PlikShare.Core.Utils;

public static class EnumerableExtensions
{
    public static List<T> AsList<T>(this IEnumerable<T> enumerable)
    {
        return enumerable as List<T> ?? enumerable.ToList();
    }
}