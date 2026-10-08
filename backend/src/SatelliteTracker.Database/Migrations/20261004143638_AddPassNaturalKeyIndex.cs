using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPassNaturalKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Passes_SatelliteId",
                table: "Passes");

            migrationBuilder.CreateIndex(
                name: "IX_Passes_SatelliteId_OrbitNumber",
                table: "Passes",
                columns: new[] { "SatelliteId", "OrbitNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Passes_SatelliteId_OrbitNumber",
                table: "Passes");

            migrationBuilder.CreateIndex(
                name: "IX_Passes_SatelliteId",
                table: "Passes",
                column: "SatelliteId");
        }
    }
}
