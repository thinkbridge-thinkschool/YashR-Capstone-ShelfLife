using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShelfLife.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchedNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DispatchedNotifications",
                schema: "notifications",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchedNotifications", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchedNotifications_DispatchedAt",
                schema: "notifications",
                table: "DispatchedNotifications",
                column: "DispatchedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispatchedNotifications",
                schema: "notifications");
        }
    }
}
