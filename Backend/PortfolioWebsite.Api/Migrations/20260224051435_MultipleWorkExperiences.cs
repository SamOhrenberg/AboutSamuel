using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultipleWorkExperiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "ProjectWorkExperience",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkExperienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectWorkExperience", x => new { x.ProjectId, x.WorkExperienceId });
                    table.ForeignKey(
                        name: "FK_ProjectWorkExperience_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectWorkExperience_WorkExperiences_WorkExperienceId",
                        column: x => x.WorkExperienceId,
                        principalTable: "WorkExperiences",
                        principalColumn: "WorkExperienceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectWorkExperience_WorkExperienceId",
                table: "ProjectWorkExperience",
                column: "WorkExperienceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectWorkExperience");

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
    }
}
