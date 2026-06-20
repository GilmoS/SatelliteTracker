using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifyToPass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Notify",
                table: "Passes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notify",
                table: "Passes");
        }
    }
}
