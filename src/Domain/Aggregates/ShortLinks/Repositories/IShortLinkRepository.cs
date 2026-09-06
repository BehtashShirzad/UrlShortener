namespace Domain.Aggregates.ShortLinks.Repositories;

public interface IShortLinkRepository
{
    Task<ShortLink?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ShortLink?> GetByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

 
    Task AddAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default);

    void Update(ShortLink shortLink);

    void Remove(ShortLink shortLink);
}