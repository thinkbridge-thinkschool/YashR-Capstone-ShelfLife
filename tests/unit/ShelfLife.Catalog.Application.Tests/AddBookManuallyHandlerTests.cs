using FluentAssertions;
using NSubstitute;
using ShelfLife.Catalog.Application;
using ShelfLife.Catalog.Domain;
using ShelfLife.SharedKernel;
using Xunit;

namespace ShelfLife.Catalog.Application.Tests;

public sealed class AddBookManuallyHandlerTests
{
    private static AddBookManuallyHandler BuildHandler(
        IBookTitleRepository? repo = null,
        IUnitOfWork? uow = null)
    {
        repo ??= Substitute.For<IBookTitleRepository>();
        uow  ??= Substitute.For<IUnitOfWork>();
        repo.FindByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BookTitle?)null);
        return new AddBookManuallyHandler(repo, uow);
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsSuccessAndPersistsBook()
    {
        var repo = Substitute.For<IBookTitleRepository>();
        var uow  = Substitute.For<IUnitOfWork>();
        repo.FindByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BookTitle?)null);

        var handler = new AddBookManuallyHandler(repo, uow);
        var result  = await handler.HandleAsync(new AddBookManuallyCommand("Clean Code", "Robert Martin", 2008));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await repo.Received(1).AddAsync(
            Arg.Is<BookTitle>(b => b.Title == "Clean Code" && b.Author == "Robert Martin"),
            Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyTitle_ReturnsFailureWithoutHittingRepo()
    {
        var repo    = Substitute.For<IBookTitleRepository>();
        var handler = new AddBookManuallyHandler(repo, Substitute.For<IUnitOfWork>());

        var result = await handler.HandleAsync(new AddBookManuallyCommand("  ", "Author", 2008));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("required");
        await repo.DidNotReceive().AddAsync(Arg.Any<BookTitle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyAuthor_ReturnsFailure()
    {
        var result = await BuildHandler()
            .HandleAsync(new AddBookManuallyCommand("Title", "", 2008));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("required");
    }

    [Theory]
    [InlineData(999)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_YearTooOld_ReturnsFailure(int year)
    {
        var result = await BuildHandler()
            .HandleAsync(new AddBookManuallyCommand("Title", "Author", year));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("year");
    }

    [Fact]
    public async Task Handle_YearFarFuture_ReturnsFailure()
    {
        var futureYear = DateTimeOffset.UtcNow.Year + 2;
        var result = await BuildHandler()
            .HandleAsync(new AddBookManuallyCommand("Title", "Author", futureYear));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("year");
    }

    [Fact]
    public async Task Handle_ValidInput_WhitespaceTrimmed()
    {
        var repo = Substitute.For<IBookTitleRepository>();
        repo.FindByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BookTitle?)null);

        var handler = new AddBookManuallyHandler(repo, Substitute.For<IUnitOfWork>());
        var result  = await handler.HandleAsync(
            new AddBookManuallyCommand("  Dune  ", "  Frank Herbert  ", 1965));

        result.IsSuccess.Should().BeTrue();
        await repo.Received(1).AddAsync(
            Arg.Is<BookTitle>(b => b.Title == "Dune" && b.Author == "Frank Herbert"),
            Arg.Any<CancellationToken>());
    }
}
