using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ShelfLife.Api.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace ShelfLife.Api.IntegrationTests.Tests;

/// <summary>
/// P99 benchmark for GET /api/v1/catalog/books — the hottest read path.
///
/// Perf fix applied before this run:
///   BooksReadModel  : AsQueryable()  → AsNoTracking()
///   MembersReadModel: AsQueryable()  → AsNoTracking()
///
/// Why AsNoTracking() helps:
///   EF Core normally attaches every tracked entity to the DbContext change-tracker,
///   allocating identity-map entries and snapshot objects even for read-only queries.
///   AsNoTracking() skips that overhead entirely — no snapshots, no identity map,
///   results materialise directly from the reader.  For a list query returning N rows
///   this is O(N) allocation savings per request.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class PerformanceTests(ShelfLifeApiFactory factory, ITestOutputHelper output)
{
    private const int WarmupRequests = 20;
    private const int MeasuredRequests = 200;

    [Fact]
    public async Task GetBooks_P99_IsBelowThreshold()
    {
        // ── Seed 30 books so the query has real work to do ───────────────────
        var libClient = factory.CreateClient();
        libClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        for (var i = 0; i < 30; i++)
        {
            await libClient.PostAsJsonAsync("/api/v1/catalog/books/manual", new
            {
                Title = $"Perf Book {i:D3}",
                Author = $"Perf Author {i:D3}",
                PublicationYear = 2000 + (i % 24),
            });
        }

        // ── Warm-up: discard first N requests so JIT and pool settle ─────────
        var measureClient = factory.CreateClient();
        measureClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        for (var i = 0; i < WarmupRequests; i++)
            await measureClient.GetAsync("/api/v1/catalog/books?page=1&pageSize=20");

        // ── Measured run ─────────────────────────────────────────────────────
        var latencies = new List<double>(MeasuredRequests);

        for (var i = 0; i < MeasuredRequests; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await measureClient.GetAsync("/api/v1/catalog/books?page=1&pageSize=20");
            sw.Stop();

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // ── Compute statistics ───────────────────────────────────────────────
        latencies.Sort();

        var p50 = Percentile(latencies, 50);
        var p95 = Percentile(latencies, 95);
        var p99 = Percentile(latencies, 99);
        var mean = latencies.Average();
        var max = latencies.Max();

        output.WriteLine("══════════════════════════════════════════════════");
        output.WriteLine($"  GET /api/v1/catalog/books — {MeasuredRequests} requests (after AsNoTracking fix)");
        output.WriteLine($"  mean = {mean:F1} ms");
        output.WriteLine($"  p50  = {p50:F1} ms");
        output.WriteLine($"  p95  = {p95:F1} ms");
        output.WriteLine($"  p99  = {p99:F1} ms");
        output.WriteLine($"  max  = {max:F1} ms");
        output.WriteLine("══════════════════════════════════════════════════");

        // ── Gate: p99 must stay under 500 ms on any CI machine ───────────────
        p99.Should().BeLessThan(500,
            "p99 latency for the catalog list endpoint must stay under 500 ms");
    }

    [Fact]
    public async Task GetMembers_P99_IsBelowThreshold()
    {
        var libClient = factory.CreateClient();
        libClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        // Warm-up
        for (var i = 0; i < WarmupRequests; i++)
            await libClient.GetAsync("/api/v1/identity/members?page=1&pageSize=20");

        // Measured
        var latencies = new List<double>(MeasuredRequests);
        for (var i = 0; i < MeasuredRequests; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await libClient.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
            sw.Stop();

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        var p99 = Percentile(latencies, 99);

        output.WriteLine("══════════════════════════════════════════════════");
        output.WriteLine($"  GET /api/v1/identity/members — {MeasuredRequests} requests (after AsNoTracking fix)");
        output.WriteLine($"  p50 = {Percentile(latencies, 50):F1} ms  " +
                         $"p95 = {Percentile(latencies, 95):F1} ms  " +
                         $"p99 = {p99:F1} ms");
        output.WriteLine("══════════════════════════════════════════════════");

        p99.Should().BeLessThan(500,
            "p99 latency for the members list endpoint must stay under 500 ms");
    }

    private static double Percentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var idx = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}
