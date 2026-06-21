using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSentToPass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificationSent",
                table: "Passes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationSentAt",
                table: "Passes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationSent",
                table: "Passes");

            migrationBuilder.DropColumn(
                name: "NotificationSentAt",
                table: "Passes");
        }
    }
}