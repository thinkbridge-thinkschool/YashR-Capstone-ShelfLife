using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Domain;

public sealed class Isbn : ValueObject
{
    public string Value { get; }

    private Isbn(string value) => Value = value;

    public static Isbn Create(string raw)
    {
        var digits = raw.Replace("-", "").Replace(" ", "");
        if (digits.Length is not 10 and not 13)
            throw new ArgumentException($"Invalid ISBN: {raw}");
        return new Isbn(digits);
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
