using FluentAssertions;
using ShelfLife.Catalog.Domain;
using Xunit;

namespace ShelfLife.Catalog.Domain.Tests;

public sealed class BookTitleTests
{
    private static BookTitle CreateBook() =>
        BookTitle.Create(Guid.NewGuid(), Isbn.Create("9780132350884"), "Clean Code", "Robert Martin", 2008);

    [Fact]
    public void Create_RaisesBookTitleCreatedEvent()
    {
        var book = CreateBook();
        book.DomainEvents.Should().ContainSingle(e => e is BookTitleCreatedEvent);
    }

    [Fact]
    public void AddCopy_IncreasesAvailability()
    {
        var book = CreateBook();
        book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        book.Status.Should().Be(BookTitleStatus.Available);
    }

    [Fact]
    public void AddCopy_DuplicateBarcode_Throws()
    {
        var book = CreateBook();
        book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        var act = () => book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LoanCopy_SetsStatusToOnLoan()
    {
        var book = CreateBook();
        var copy = book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        book.LoanCopy(copy.Id, Guid.NewGuid());
        book.Status.Should().Be(BookTitleStatus.FullyOnLoan);
    }

    [Fact]
    public void ReturnCopy_ResetsStatusToAvailable()
    {
        var book = CreateBook();
        var copy = book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        book.LoanCopy(copy.Id, Guid.NewGuid());
        book.ReturnCopy(copy.Id);
        book.Status.Should().Be(BookTitleStatus.Available);
    }

    [Fact]
    public void LoanCopy_WhenAlreadyOnLoan_Throws()
    {
        var book = CreateBook();
        var copy = book.AddCopy(Guid.NewGuid(), CopyBarcode.Create("BC-001"));
        book.LoanCopy(copy.Id, Guid.NewGuid());
        var act = () => book.LoanCopy(copy.Id, Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Isbn_InvalidFormat_Throws()
    {
        var act = () => Isbn.Create("INVALID");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Isbn_NormalisesHyphens()
    {
        var isbn = Isbn.Create("978-0-13-235088-4");
        isbn.Value.Should().Be("9780132350884");
    }

    [Fact]
    public void Isbn10_ValidCheckDigit_Accepted()
    {
        // 0-19-852663-6 is a known valid ISBN-10 (Oxford English Dictionary)
        var isbn = Isbn.Create("0-19-852663-6");
        isbn.Value.Should().Be("0198526636");
    }

    [Fact]
    public void Isbn10_InvalidCheckDigit_Throws()
    {
        // last digit changed from 6 → 7
        var act = () => Isbn.Create("0198526637");
        act.Should().Throw<ArgumentException>().WithMessage("*check digit*");
    }

    [Fact]
    public void Isbn13_InvalidCheckDigit_Throws()
    {
        // last digit changed from 4 → 5
        var act = () => Isbn.Create("9780132350885");
        act.Should().Throw<ArgumentException>().WithMessage("*check digit*");
    }

    [Fact]
    public void Isbn_Empty_Throws()
    {
        var act = () => Isbn.Create("   ");
        act.Should().Throw<ArgumentException>();
    }

    // ── Isbn.CreateManual ────────────────────────────────────────────────────

    [Fact]
    public void Isbn_CreateManual_ProducesValidIsbn13()
    {
        var isbn = Isbn.CreateManual(Guid.NewGuid());
        // Must round-trip through the strict validator without throwing
        var roundTripped = Isbn.Create(isbn.Value);
        roundTripped.Value.Should().Be(isbn.Value);
    }

    [Fact]
    public void Isbn_CreateManual_IsDeterministic()
    {
        var id = Guid.NewGuid();
        var first  = Isbn.CreateManual(id);
        var second = Isbn.CreateManual(id);
        first.Value.Should().Be(second.Value);
    }

    [Fact]
    public void Isbn_CreateManual_DifferentGuids_ProduceDifferentValues()
    {
        var a = Isbn.CreateManual(Guid.NewGuid());
        var b = Isbn.CreateManual(Guid.NewGuid());
        a.Value.Should().NotBe(b.Value);
    }
}
