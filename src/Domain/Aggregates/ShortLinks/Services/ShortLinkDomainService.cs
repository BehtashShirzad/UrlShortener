using Domain.Aggregates.ShortLinks.Repositories;

namespace Domain.Aggregates.ShortLinks.Services;

internal sealed class ShortLinkDomainService(
    IShortLinkRepository shortLinkRepository)
    : IShortLinkDomainService
{
    public async Task<ShortLink> CreateShortLink(
        string originalUrl,
        RedirectType redirectType,
        DateTime? expiresAt,
        long? maxClicks)
    {
        var shortCode = await GenerateUniqueShortCodeAsync();

        return ShortLink.Create(
            originalUrl,
            shortCode,
            redirectType,
            expiresAt,
            maxClicks);
    }

    private async Task<string> GenerateUniqueShortCodeAsync()
    {
        while (true)
        {
            var shortCode = GenerateShortCode();

            var exists =
                await shortLinkRepository.ExistsByShortCodeAsync(shortCode);

            if (!exists)
                return shortCode;
        }
    }

    private static string GenerateShortCode()
    {
        const string chars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return string.Create(7, chars, static (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] =
                    alphabet[Random.Shared.Next(alphabet.Length)];
            }
        });
    }
}