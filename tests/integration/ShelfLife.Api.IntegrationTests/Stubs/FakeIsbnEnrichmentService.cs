using ShelfLife.Catalog.Application;

namespace ShelfLife.Api.IntegrationTests.Stubs;

/// <summary>
/// Fake ISBN enrichment that returns deterministic metadata for any valid ISBN.
/// Prevents outbound HTTP to Open Library during integration tests.
/// </summary>
internal sealed class FakeIsbnEnrichmentService : IIsbnEnrichmentService
{
    public Task<IsbnMetadata?> LookupAsync(string isbn, CancellationToken ct = default)
        => Task.FromResult<IsbnMetadata?>(
            new IsbnMetadata(isbn, $"Test Book [{isbn}]", "Test Author", 2024));
}
