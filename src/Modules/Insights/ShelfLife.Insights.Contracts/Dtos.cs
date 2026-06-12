namespace ShelfLife.Insights.Contracts;

public sealed record PopularTitleDto(Guid BookTitleId, string Title, string Author, int BorrowCount);

public sealed record OverdueLoanDto(Guid LoanId, Guid MemberId, string MemberName, string BookTitle, DateTimeOffset DueDate, int DaysOverdue);

public sealed record MemberActivityDto(Guid MemberId, string FullName, int TotalBorrows, int ActiveLoans, int OverdueLoans);
