using FluentAssertions;
using ShelfLife.Lending.Domain;
using Xunit;

namespace ShelfLife.Lending.Domain.Tests;

public sealed class LoanTests
{
    private static Loan CreateLoan() =>
        Loan.Create(
            Guid.NewGuid(),
            memberId: Guid.NewGuid(),
            bookTitleId: Guid.NewGuid(),
            copyId: Guid.NewGuid(),
            LoanPeriod.Create(DateTimeOffset.UtcNow));

    [Fact]
    public void Create_RaisesLoanCreatedDomainEvent()
    {
        var loan = CreateLoan();
        loan.DomainEvents.Should().ContainSingle(e => e is LoanCreatedDomainEvent);
    }

    [Fact]
    public void Return_SetsStatusToReturned()
    {
        var loan = CreateLoan();
        loan.Return();
        loan.Status.Should().Be(LoanStatus.Returned);
    }

    [Fact]
    public void Return_RaisesLoanReturnedDomainEvent()
    {
        var loan = CreateLoan();
        loan.Return();
        loan.DomainEvents.Should().Contain(e => e is LoanReturnedDomainEvent);
    }

    [Fact]
    public void Return_WhenAlreadyReturned_Throws()
    {
        var loan = CreateLoan();
        loan.Return();
        var act = () => loan.Return();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PlaceHold_AddsHoldToCollection()
    {
        var loan = CreateLoan();
        loan.PlaceHold(Guid.NewGuid(), Guid.NewGuid());
        loan.Holds.Should().HaveCount(1);
    }

    [Fact]
    public void PlaceHold_DuplicateMember_Throws()
    {
        var loan = CreateLoan();
        var memberId = Guid.NewGuid();
        loan.PlaceHold(Guid.NewGuid(), memberId);
        var act = () => loan.PlaceHold(Guid.NewGuid(), memberId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Return_WithPendingHold_RaisesHoldReadyEvent()
    {
        var loan = CreateLoan();
        loan.PlaceHold(Guid.NewGuid(), Guid.NewGuid());
        loan.Return();
        loan.DomainEvents.Should().Contain(e => e is HoldReadyDomainEvent);
    }

    [Fact]
    public void LoanPeriod_IsOverdue_WhenPastDueDate()
    {
        var period = LoanPeriod.Create(DateTimeOffset.UtcNow.AddDays(-20));
        period.IsOverdue(DateTimeOffset.UtcNow).Should().BeTrue();
    }
}
