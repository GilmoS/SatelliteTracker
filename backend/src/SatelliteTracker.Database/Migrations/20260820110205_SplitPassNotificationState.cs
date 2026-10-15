using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class SplitPassNotificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationSent",
                table: "Passes");

            migrationBuilder.DropColumn(
                name: "NotificationSentAt",
                table: "Passes");

            migrationBuilder.DropColumn(
                name: "Notify",
                table: "Passes");

            migrationBuilder.CreateTable(
                name: "PassNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertMinutes = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassNotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassNotificationLogs_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassNotificationLogs_Passes_PassId",
                        column: x => x.PassId,
                        principalTable: "Passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PassId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notify = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassSubscriptions_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassSubscriptions_Passes_PassId",
                        column: x => x.PassId,
                        principalTable: "Passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassNotificationLogs_ApiKeyId",
                table: "PassNotificationLogs",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_PassNotificationLogs_PassId_ApiKeyId_AlertMinutes",
                table: "PassNotificationLogs",
                columns: new[] { "PassId", "ApiKeyId", "AlertMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PassSubscriptions_ApiKeyId",
                table: "PassSubscriptions",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_PassSubscriptions_PassId_ApiKeyId",
                table: "PassSubscriptions",
                columns: new[] { "PassId", "ApiKeyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PassNotificationLogs");

            migrationBuilder.DropTable(
                name: "PassSubscriptions");

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

            migrationBuilder.AddColumn<bool>(
                name: "Notify",
                table: "Passes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
