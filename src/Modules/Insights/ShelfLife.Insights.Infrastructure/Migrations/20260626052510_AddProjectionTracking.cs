using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShelfLife.Insights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedProjectionEvents",
                schema: "insights",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedProjectionEvents", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedProjectionEvents_ProcessedAt",
                schema: "insights",
                table: "ProcessedProjectionEvents",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedProjectionEvents",
                schema: "insights");
        }
    }
}
