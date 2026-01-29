using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediCare.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorBlockedTimes_DoctorId_StartUtc_EndUtc_Status",
                table: "DoctorBlockedTimes");

            migrationBuilder.DropIndex(
                name: "IX_DoctorAssignments_DoctorId_StartUtc_EndUtc",
                table: "DoctorAssignments");

            migrationBuilder.CreateTable(
                name: "DoctorBlockRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorBlockRequest", x => x.Id);
                    table.CheckConstraint("CK_BlockRequest_Dates", "[EndDateUtc] >= [StartDateUtc]");
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignment", x => x.Id);
                    table.CheckConstraint("CK_DutyAssignment_Time", "[EndUtc] > [StartUtc]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorBlockedTimes_DoctorId",
                table: "DoctorBlockedTimes",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorAssignments_DoctorId",
                table: "DoctorAssignments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorBlockRequest_DoctorId_Status",
                table: "DoctorBlockRequest",
                columns: new[] { "DoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignment_BranchId",
                table: "DutyAssignment",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignment_DoctorId_StartUtc",
                table: "DutyAssignment",
                columns: new[] { "DoctorId", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorBlockRequest");

            migrationBuilder.DropTable(
                name: "DutyAssignment");

            migrationBuilder.DropIndex(
                name: "IX_DoctorBlockedTimes_DoctorId",
                table: "DoctorBlockedTimes");

            migrationBuilder.DropIndex(
                name: "IX_DoctorAssignments_DoctorId",
                table: "DoctorAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorBlockedTimes_DoctorId_StartUtc_EndUtc_Status",
                table: "DoctorBlockedTimes",
                columns: new[] { "DoctorId", "StartUtc", "EndUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorAssignments_DoctorId_StartUtc_EndUtc",
                table: "DoctorAssignments",
                columns: new[] { "DoctorId", "StartUtc", "EndUtc" });
        }
    }
}
