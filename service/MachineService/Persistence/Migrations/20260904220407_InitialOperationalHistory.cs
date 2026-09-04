using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MachineService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOperationalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fault_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FaultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MachineState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CycleState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ClearedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fault_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "machine_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MachineState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "production_cycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Accepted = table.Column<bool>(type: "boolean", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    FinalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Faulted = table.Column<bool>(type: "boolean", nullable: false),
                    FaultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FaultMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_cycles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fault_events_Timestamp",
                table: "fault_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_machine_events_Timestamp",
                table: "machine_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_production_cycles_StartedAt",
                table: "production_cycles",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fault_events");

            migrationBuilder.DropTable(
                name: "machine_events");

            migrationBuilder.DropTable(
                name: "production_cycles");
        }
    }
}
