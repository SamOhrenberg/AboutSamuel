using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobFitRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobFitRuns",
                columns: table => new
                {
                    JobFitRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseTookMs = table.Column<float>(type: "real", nullable: false),
                    SessionTrackingId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: false),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    Company = table.Column<string>(type: "text", nullable: true),
                    IsJobDescription = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<string>(type: "text", nullable: true),
                    FitScore = table.Column<double>(type: "double precision", nullable: true),
                    MustHavesMet = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CoverLetter = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFitRuns", x => x.JobFitRunId);
                });

            migrationBuilder.CreateTable(
                name: "JobFitRequirements",
                columns: table => new
                {
                    JobFitRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobFitRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementKey = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Importance = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Citations = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFitRequirements", x => x.JobFitRequirementId);
                    table.ForeignKey(
                        name: "FK_JobFitRequirements_JobFitRuns_JobFitRunId",
                        column: x => x.JobFitRunId,
                        principalTable: "JobFitRuns",
                        principalColumn: "JobFitRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobFitRequirements_JobFitRunId",
                table: "JobFitRequirements",
                column: "JobFitRunId");

            migrationBuilder.CreateIndex(
                name: "IX_JobFitRequirements_Status_Importance",
                table: "JobFitRequirements",
                columns: new[] { "Status", "Importance" });

            migrationBuilder.CreateIndex(
                name: "IX_JobFitRuns_ReceivedAt",
                table: "JobFitRuns",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobFitRequirements");

            migrationBuilder.DropTable(
                name: "JobFitRuns");
        }
    }
}
