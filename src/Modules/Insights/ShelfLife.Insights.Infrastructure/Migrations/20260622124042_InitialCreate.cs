using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShelfLife.Insights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "insights");

            migrationBuilder.CreateTable(
                name: "MemberActivity",
                schema: "insights",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalBorrows = table.Column<int>(type: "int", nullable: false),
                    ActiveLoans = table.Column<int>(type: "int", nullable: false),
                    OverdueLoans = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberActivity", x => x.MemberId);
                });

            migrationBuilder.CreateTable(
                name: "OverdueLoans",
                schema: "insights",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BookTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverdueLoans", x => x.LoanId);
                });

            migrationBuilder.CreateTable(
                name: "PopularTitles",
                schema: "insights",
                columns: table => new
                {
                    BookTitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BorrowCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PopularTitles", x => x.BookTitleId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberActivity",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "OverdueLoans",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "PopularTitles",
                schema: "insights");
        }
    }
}
