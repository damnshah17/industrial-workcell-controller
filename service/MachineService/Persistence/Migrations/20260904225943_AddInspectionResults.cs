using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "InspectionFeatureCoverage",
                table: "production_cycles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionReason",
                table: "production_cycles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionSampleId",
                table: "production_cycles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionFeatureCoverage",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "InspectionReason",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "InspectionSampleId",
                table: "production_cycles");
        }
    }
}
