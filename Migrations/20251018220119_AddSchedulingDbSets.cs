using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediCare.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingDbSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorAssignments");

            migrationBuilder.DropTable(
                name: "DoctorBlockedTimes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DutyAssignment",
                table: "DutyAssignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorBlockRequest",
                table: "DoctorBlockRequest");

            migrationBuilder.RenameTable(
                name: "DutyAssignment",
                newName: "DutyAssignments");

            migrationBuilder.RenameTable(
                name: "DoctorBlockRequest",
                newName: "DoctorBlockRequests");

            migrationBuilder.RenameIndex(
                name: "IX_DutyAssignment_DoctorId_StartUtc",
                table: "DutyAssignments",
                newName: "IX_DutyAssignments_DoctorId_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DutyAssignment_BranchId",
                table: "DutyAssignments",
                newName: "IX_DutyAssignments_BranchId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorBlockRequest_DoctorId_Status",
                table: "DoctorBlockRequests",
                newName: "IX_DoctorBlockRequests_DoctorId_Status");

            migrationBuilder.AddColumn<int>(
                name: "DoctorId1",
                table: "DutyAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoctorId1",
                table: "DoctorBlockRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DutyAssignments",
                table: "DutyAssignments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorBlockRequests",
                table: "DoctorBlockRequests",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_DoctorId1",
                table: "DutyAssignments",
                column: "DoctorId1");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorBlockRequests_DoctorId1",
                table: "DoctorBlockRequests",
                column: "DoctorId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorBlockRequests_Doctors_DoctorId1",
                table: "DoctorBlockRequests",
                column: "DoctorId1",
                principalTable: "Doctors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DutyAssignments_Doctors_DoctorId1",
                table: "DutyAssignments",
                column: "DoctorId1",
                principalTable: "Doctors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorBlockRequests_Doctors_DoctorId1",
                table: "DoctorBlockRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyAssignments_Doctors_DoctorId1",
                table: "DutyAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DutyAssignments",
                table: "DutyAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_DoctorId1",
                table: "DutyAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorBlockRequests",
                table: "DoctorBlockRequests");

            migrationBuilder.DropIndex(
                name: "IX_DoctorBlockRequests_DoctorId1",
                table: "DoctorBlockRequests");

            migrationBuilder.DropColumn(
                name: "DoctorId1",
                table: "DutyAssignments");

            migrationBuilder.DropColumn(
                name: "DoctorId1",
                table: "DoctorBlockRequests");

            migrationBuilder.RenameTable(
                name: "DutyAssignments",
                newName: "DutyAssignment");

            migrationBuilder.RenameTable(
                name: "DoctorBlockRequests",
                newName: "DoctorBlockRequest");

            migrationBuilder.RenameIndex(
                name: "IX_DutyAssignments_DoctorId_StartUtc",
                table: "DutyAssignment",
                newName: "IX_DutyAssignment_DoctorId_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DutyAssignments_BranchId",
                table: "DutyAssignment",
                newName: "IX_DutyAssignment_BranchId");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorBlockRequests_DoctorId_Status",
                table: "DoctorBlockRequest",
                newName: "IX_DoctorBlockRequest_DoctorId_Status");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DutyAssignment",
                table: "DutyAssignment",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorBlockRequest",
                table: "DoctorBlockRequest",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "DoctorAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicBranchId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorAssignments_ClinicBranches_ClinicBranchId",
                        column: x => x.ClinicBranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoctorAssignments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorBlockedTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByAdminUserId = table.Column<int>(type: "int", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorBlockedTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorBlockedTimes_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorAssignments_ClinicBranchId",
                table: "DoctorAssignments",
                column: "ClinicBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorAssignments_DoctorId",
                table: "DoctorAssignments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorBlockedTimes_DoctorId",
                table: "DoctorBlockedTimes",
                column: "DoctorId");
        }
    }
}
