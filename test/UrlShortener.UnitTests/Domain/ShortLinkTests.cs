using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Events;

namespace UrlShortener.UnitTests.Domain;

public sealed class ShortLinkTests
{
    [Theory]
    [InlineData(RedirectType.Temporary, null)]
    [InlineData(RedirectType.Permanent, 100L)]
    public void Create_preserves_settings_and_assigns_an_active_identity(RedirectType type, long? maxClicks)
    {
        var expiresAt = DateTime.UtcNow.AddDays(1);

        var link = ShortLink.Create("https://example.com/path?q=a%20b", "Ab12xyz", type, expiresAt, maxClicks);

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal(7, link.Id.Version);
        Assert.Equal("https://example.com/path?q=a%20b", link.OriginalUrl);
        Assert.Equal("Ab12xyz", link.ShortCode);
        Assert.Equal(type, link.RedirectType);
        Assert.Equal(expiresAt, link.ExpiresAt);
        Assert.Equal(maxClicks, link.MaxClicks);
        Assert.True(link.IsActive);
    }

    [Fact]
    public void Create_without_expiration_raises_one_complete_creation_event()
    {
        var before = DateTime.UtcNow;
        var link = ShortLink.Create("https://example.com", "Ab12xyz", RedirectType.Permanent, null, null);

        var raised = Assert.IsType<ShortLinkCreatedDomainEvent>(Assert.Single(link.DomainEvents));
        Assert.Equal(link.Id, raised.ShortLinkId);
        Assert.Equal(link.ShortCode, raised.ShortCode);
        Assert.Equal(link.OriginalUrl, raised.OriginalUrl);
        Assert.Equal(link.RedirectType, raised.RedirectType);
        Assert.Null(raised.ExpiresAt);
        Assert.NotEqual(Guid.Empty, raised.Id);
        Assert.NotEqual(link.Id, raised.Id);
        Assert.InRange(raised.OccurredOn, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, raised.OccurredOn.Kind);
    }

    [Fact]
    public void ClearEvents_removes_pending_events_without_changing_the_link()
    {
        var link = ShortLink.Create("https://example.com", "Ab12xyz", RedirectType.Temporary, null, null);
        var id = link.Id;

        link.ClearEvents();
        link.ClearEvents();

        Assert.Empty(link.DomainEvents);
        Assert.Equal(id, link.Id);
        Assert.Equal("Ab12xyz", link.ShortCode);
        Assert.True(link.IsActive);
    }

    [Fact]
    public void Two_links_with_identical_content_have_different_identities()
    {
        var first = ShortLink.Create("https://example.com", "Ab12xyz", RedirectType.Temporary, null, null);
        var second = ShortLink.Create("https://example.com", "Ab12xyz", RedirectType.Temporary, null, null);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first, second);
    }
}
