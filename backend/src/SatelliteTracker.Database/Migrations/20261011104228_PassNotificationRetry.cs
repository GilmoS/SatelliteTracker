using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatelliteTracker.Database.Migrations
{
    /// <inheritdoc />
    public partial class PassNotificationRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                table: "PassNotificationLogs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "PassNotificationLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "PassNotificationLogs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "PassNotificationLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PassNotificationLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every row before this migration was written after a send that was assumed to have
            // succeeded, so record it as one Sent attempt.
            migrationBuilder.Sql(
                """
                UPDATE "PassNotificationLogs"
                SET "Status" = 'Sent', "AttemptCount" = 1, "LastAttemptAt" = "SentAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SentAt becomes required again; unsent attempts have no send time to keep.
            migrationBuilder.Sql(
                """
                DELETE FROM "PassNotificationLogs" WHERE "SentAt" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "PassNotificationLogs");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "PassNotificationLogs");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "PassNotificationLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PassNotificationLogs");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                table: "PassNotificationLogs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
