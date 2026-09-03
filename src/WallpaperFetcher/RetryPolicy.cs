// Exponential backoff for transient failures (no internet, Wallhaven unreachable). Gives up after maxAttempts.
namespace WallpaperFetcher;

public sealed class RetryExhaustedException : Exception
{
    public RetryExhaustedException(string message, Exception inner) : base(message, inner) { }
}

public static class RetryPolicy
{
    public static async Task<T> RunWithBackoffAsync<T>(
        Func<Task<T>> action,
        int maxAttempts,
        TimeSpan baseDelay,
        Action<int, Exception, TimeSpan> onRetry,
        CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                last = ex;
                if (attempt == maxAttempts)
                    break;

                var delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
                onRetry(attempt, ex, delay);
                await Task.Delay(delay, ct);
            }
        }

        throw new RetryExhaustedException($"Gave up after {maxAttempts} attempt(s).", last!);
    }

    private static bool IsTransient(Exception ex) => ex is HttpRequestException
        or TaskCanceledException
        or System.Net.Sockets.SocketException
        or IOException;
}
