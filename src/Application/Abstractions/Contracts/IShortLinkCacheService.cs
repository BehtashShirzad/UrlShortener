namespace Application.Abstractions.Caching;

public interface IShortLinkCacheService
{
    Task<ShortLinkCacheEntry?> GetAsync(
       string shortCode,
       CancellationToken cancellationToken = default);

    Task SetAsync(
        string shortCode,
        ShortLinkCacheEntry entry,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string shortCode,
        CancellationToken cancellationToken = default);
}