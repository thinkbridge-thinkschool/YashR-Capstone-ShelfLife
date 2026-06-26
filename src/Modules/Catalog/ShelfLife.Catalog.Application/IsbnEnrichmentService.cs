using System.Text.Json;

namespace ShelfLife.Catalog.Application;

public sealed record IsbnMetadata(string Isbn, string Title, string Author, int PublicationYear);

public interface IIsbnEnrichmentService
{
    Task<IsbnMetadata?> LookupAsync(string isbn, CancellationToken ct = default);
}

public sealed class IsbnEnrichmentService : IIsbnEnrichmentService
{
    private readonly HttpClient _http;

    public IsbnEnrichmentService(HttpClient http) => _http = http;

    public async Task<IsbnMetadata?> LookupAsync(string isbn, CancellationToken ct = default)
    {
        var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=details";
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var key = $"ISBN:{isbn}";
        if (!root.TryGetProperty(key, out var entry)) return null;

        var details = entry.GetProperty("details");
        var title = details.GetProperty("title").GetString() ?? string.Empty;
        var author = details.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0
            ? authors[0].GetProperty("name").GetString() ?? string.Empty
            : string.Empty;
        var year = details.TryGetProperty("publish_date", out var date)
            ? int.TryParse(date.GetString()?[^4..], out var y) ? y : 0
            : 0;

        return new IsbnMetadata(isbn, title, author, year);
    }
}
