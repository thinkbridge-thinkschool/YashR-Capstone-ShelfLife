namespace ShelfLife.Insights.Infrastructure;

public sealed class PopularTitleProjection
{
    public Guid BookTitleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}

public sealed class OverdueLoanProjection
{
    public Guid LoanId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public DateTimeOffset DueDate { get; set; }
}

public sealed class MemberActivityProjection
{
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int TotalBorrows { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
}
