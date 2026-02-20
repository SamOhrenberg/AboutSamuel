using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class TiedEmployerToWorkExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Employer",
                table: "Projects");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkExperienceId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkExperienceId",
                table: "Projects",
                column: "WorkExperienceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_WorkExperiences_WorkExperienceId",
                table: "Projects",
                column: "WorkExperienceId",
                principalTable: "WorkExperiences",
                principalColumn: "WorkExperienceId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_WorkExperiences_WorkExperienceId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_WorkExperienceId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkExperienceId",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "Employer",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
