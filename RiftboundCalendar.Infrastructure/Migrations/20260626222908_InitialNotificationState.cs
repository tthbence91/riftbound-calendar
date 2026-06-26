using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_states",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "text", nullable: false),
                    last_status = table.Column<string>(type: "text", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_states", x => x.event_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_states");
        }
    }
}
