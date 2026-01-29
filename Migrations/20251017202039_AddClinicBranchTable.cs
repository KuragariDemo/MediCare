using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediCare.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicBranchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAssignment_ClinicBranch_ClinicBranchId",
                table: "DoctorAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAssignment_Doctors_DoctorId",
                table: "DoctorAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorBlockedTime_Doctors_DoctorId",
                table: "DoctorBlockedTime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorBlockedTime",
                table: "DoctorBlockedTime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorAssignment",
                table: "DoctorAssignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicBranch",
                table: "ClinicBranch");

            migrationBuilder.RenameTable(
                name: "DoctorBlockedTime",
                newName: "DoctorBlockedTimes");

            migrationBuilder.RenameTable(
                name: "DoctorAssignment",
                newName: "DoctorAssignments");

            migrationBuilder.RenameTable(
                name: "ClinicBranch",
                newName: "ClinicBranches");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorBlockedTime_DoctorId_StartUtc_EndUtc_Status",
                table: "DoctorBlockedTimes",
                newName: "IX_DoctorBlockedTimes_DoctorId_StartUtc_EndUtc_Status");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorAssignment_DoctorId_StartUtc_EndUtc",
                table: "DoctorAssignments",
                newName: "IX_DoctorAssignments_DoctorId_StartUtc_EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorAssignment_ClinicBranchId",
                table: "DoctorAssignments",
                newName: "IX_DoctorAssignments_ClinicBranchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorBlockedTimes",
                table: "DoctorBlockedTimes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorAssignments",
                table: "DoctorAssignments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicBranches",
                table: "ClinicBranches",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAssignments_ClinicBranches_ClinicBranchId",
                table: "DoctorAssignments",
                column: "ClinicBranchId",
                principalTable: "ClinicBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAssignments_Doctors_DoctorId",
                table: "DoctorAssignments",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorBlockedTimes_Doctors_DoctorId",
                table: "DoctorBlockedTimes",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAssignments_ClinicBranches_ClinicBranchId",
                table: "DoctorAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAssignments_Doctors_DoctorId",
                table: "DoctorAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorBlockedTimes_Doctors_DoctorId",
                table: "DoctorBlockedTimes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorBlockedTimes",
                table: "DoctorBlockedTimes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorAssignments",
                table: "DoctorAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicBranches",
                table: "ClinicBranches");

            migrationBuilder.RenameTable(
                name: "DoctorBlockedTimes",
                newName: "DoctorBlockedTime");

            migrationBuilder.RenameTable(
                name: "DoctorAssignments",
                newName: "DoctorAssignment");

            migrationBuilder.RenameTable(
                name: "ClinicBranches",
                newName: "ClinicBranch");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorBlockedTimes_DoctorId_StartUtc_EndUtc_Status",
                table: "DoctorBlockedTime",
                newName: "IX_DoctorBlockedTime_DoctorId_StartUtc_EndUtc_Status");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorAssignments_DoctorId_StartUtc_EndUtc",
                table: "DoctorAssignment",
                newName: "IX_DoctorAssignment_DoctorId_StartUtc_EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DoctorAssignments_ClinicBranchId",
                table: "DoctorAssignment",
                newName: "IX_DoctorAssignment_ClinicBranchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorBlockedTime",
                table: "DoctorBlockedTime",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorAssignment",
                table: "DoctorAssignment",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicBranch",
                table: "ClinicBranch",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAssignment_ClinicBranch_ClinicBranchId",
                table: "DoctorAssignment",
                column: "ClinicBranchId",
                principalTable: "ClinicBranch",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAssignment_Doctors_DoctorId",
                table: "DoctorAssignment",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorBlockedTime_Doctors_DoctorId",
                table: "DoctorBlockedTime",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
