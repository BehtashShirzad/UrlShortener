using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UrlShortener.IntegrationTests.Support;

// Observes real SQL without changing its execution or results.
public sealed class ShortLinkReadCounter : DbCommandInterceptor
{
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("\"ShortLinks\"", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _count);
        }
        return ValueTask.FromResult(result);
    }
}
