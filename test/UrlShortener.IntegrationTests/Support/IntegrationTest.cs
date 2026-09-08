namespace UrlShortener.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ShortenerFactory>
{
    public const string Name = "PostgreSQL and Redis";
}

// xUnit serializes this collection. Every test starts with clean PostgreSQL,
// Redis and memory caches; container startup is shared across these tests.
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTest(ShortenerFactory factory) : IAsyncLifetime
{
    protected ShortenerFactory Factory { get; } = factory;
    public Task InitializeAsync() => Factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
