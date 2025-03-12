namespace PlikShare.Core.Utils;

public static class Try
{
    public static async Task Execute(Func<Task> @try, Func<ValueTask> @finally)
    {
        try
        {
            await @try();
        }
        finally
        {
            await @finally();
        }
    }

    public static async Task<T> Execute<T>(Func<Task<T>> @try, Func<ValueTask> @finally)
    {
        try
        {
            return await @try();
        }
        finally
        {
            await @finally();
        }
    }
}