using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Satellites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NoradId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Satellites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertMinutes = table.Column<int[]>(type: "integer[]", nullable: false),
                    OutlookDays = table.Column<int>(type: "integer", nullable: false),
                    TeamEmail = table.Column<string>(type: "text", nullable: true),
                    MinElevation = table.Column<decimal>(type: "numeric", nullable: false),
                    FcmToken = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TleRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SatelliteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Line1 = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    Line2 = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    Epoch = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TleRecords_Satellites_SatelliteId",
                        column: x => x.SatelliteId,
                        principalTable: "Satellites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Passes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SatelliteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrbitNumber = table.Column<int>(type: "integer", nullable: false),
                    Aos = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Los = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxElevation = table.Column<decimal>(type: "numeric", nullable: false),
                    AosAzimuth = table.Column<decimal>(type: "numeric", nullable: false),
                    LosAzimuth = table.Column<decimal>(type: "numeric", nullable: false),
                    DurationSec = table.Column<int>(type: "integer", nullable: false),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutlookSynced = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passes_Satellites_SatelliteId",
                        column: x => x.SatelliteId,
                        principalTable: "Satellites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Passes_TleRecords_TleId",
                        column: x => x.TleId,
                        principalTable: "TleRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Passes_PassId",
                        column: x => x.PassId,
                        principalTable: "Passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_PassId",
                table: "Notes",
                column: "PassId");

            migrationBuilder.CreateIndex(
                name: "IX_Passes_SatelliteId",
                table: "Passes",
                column: "SatelliteId");

            migrationBuilder.CreateIndex(
                name: "IX_Passes_TleId",
                table: "Passes",
                column: "TleId");

            migrationBuilder.CreateIndex(
                name: "IX_Satellites_NoradId",
                table: "Satellites",
                column: "NoradId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TleRecords_SatelliteId",
                table: "TleRecords",
                column: "SatelliteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Notes");
            migrationBuilder.DropTable(name: "Settings");
            migrationBuilder.DropTable(name: "Passes");
            migrationBuilder.DropTable(name: "TleRecords");
            migrationBuilder.DropTable(name: "Satellites");
        }
    }
}
