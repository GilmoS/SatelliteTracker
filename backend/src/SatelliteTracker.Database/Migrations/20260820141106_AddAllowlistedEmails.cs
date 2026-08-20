using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowlistedEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowlistedEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowlistedEmails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllowlistedEmails_Email",
                table: "AllowlistedEmails",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowlistedEmails");
        }
    }
}
