using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Domain;

public sealed class Isbn : ValueObject
{
    public string Value { get; }

    private Isbn(string value) => Value = value;

    // Normalise and validate an ISBN-10 or ISBN-13 string.
    // Uses ReadOnlySpan<char> + stackalloc so the hot path makes zero heap
    // allocations beyond the single string stored on the object itself.
    public static Isbn Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException($"Invalid ISBN: {raw}");

        // Strip hyphens and spaces into a fixed stack buffer (max 17 raw chars → 13 digits).
        Span<char> buf = stackalloc char[13];
        var normalised = Normalise(raw.AsSpan(), buf);

        if (normalised.Length == 10)
        {
            if (!IsValidIsbn10(normalised))
                throw new ArgumentException($"Invalid ISBN-10 check digit: {raw}");
        }
        else if (normalised.Length == 13)
        {
            if (!IsValidIsbn13(normalised))
                throw new ArgumentException($"Invalid ISBN-13 check digit: {raw}");
        }
        else
        {
            throw new ArgumentException($"Invalid ISBN: {raw}");
        }

        return new Isbn(new string(normalised));
    }

    // Copy only digit (and for ISBN-10, 'X') characters from src into dst.
    private static ReadOnlySpan<char> Normalise(ReadOnlySpan<char> src, Span<char> dst)
    {
        var written = 0;
        foreach (var c in src)
        {
            if (char.IsAsciiDigit(c) || c is 'X' or 'x')
            {
                if (written >= dst.Length) return dst[..0]; // overflow → invalid length
                dst[written++] = char.ToUpperInvariant(c);
            }
        }
        return dst[..written];
    }

    // ISBN-10: sum of (digit × position) mod 11 == 0; 'X' counts as 10.
    private static bool IsValidIsbn10(ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            if (!char.IsAsciiDigit(digits[i])) return false;
            sum += (digits[i] - '0') * (10 - i);
        }
        var check = digits[9] == 'X' ? 10 : digits[9] - '0';
        sum += check;
        return sum % 11 == 0;
    }

    // ISBN-13: alternating weight 1/3, total mod 10 == 0.
    private static bool IsValidIsbn13(ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            if (!char.IsAsciiDigit(digits[i])) return false;
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }
        var check = (10 - sum % 10) % 10;
        return digits[12] - '0' == check;
    }

    // Generate a valid ISBN-13 deterministically from a GUID for manually-entered books.
    public static Isbn CreateManual(Guid bookId)
    {
        var bytes = bookId.ToByteArray();
        Span<char> digits = stackalloc char[13];
        for (var i = 0; i < 12; i++)
            digits[i] = (char)('0' + bytes[i] % 10);

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        digits[12] = (char)('0' + (10 - sum % 10) % 10);

        return new Isbn(new string(digits));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

public sealed class CopyBarcode : ValueObject
{
    public string Value { get; }

    private CopyBarcode(string value) => Value = value;

    public static CopyBarcode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Barcode cannot be empty.");
        return new CopyBarcode(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
