using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class ShortLinkRepository(
    ShortLinkDbContext dbContext)
    : IShortLinkRepository
{
    public async Task AddAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default)
    {
        await dbContext.ShortLinks.AddAsync(
            shortLink,
            cancellationToken);
    }

  

    public Task<bool> ExistsByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ShortLinks
            .AsNoTracking()
            .AnyAsync(
                x => x.ShortCode == shortCode,
                cancellationToken);
    }

    public Task<ShortLink?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ShortLinks
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<ShortLink?> GetByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {

        
        return dbContext.ShortLinks
            .FirstOrDefaultAsync(
                x => x.ShortCode == shortCode,
                cancellationToken);
    }

    public void Remove(ShortLink shortLink)
    {
        dbContext.ShortLinks.Remove(shortLink);
    }

    public void Update(ShortLink shortLink)
    {
        dbContext.ShortLinks.Update(shortLink);
    }
}