using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afisha.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAt",
                table: "Events",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedAt",
                table: "Events");
        }
    }
}
