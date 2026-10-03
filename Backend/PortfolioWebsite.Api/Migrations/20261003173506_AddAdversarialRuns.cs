using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdversarialRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdversarialRuns",
                columns: table => new
                {
                    AdversarialRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "running"),
                    Error = table.Column<string>(type: "text", nullable: true),
                    TargetModel = table.Column<string>(type: "text", nullable: false),
                    JudgeModel = table.Column<string>(type: "text", nullable: false),
                    TotalCases = table.Column<int>(type: "integer", nullable: false),
                    CasesAnswered = table.Column<int>(type: "integer", nullable: false),
                    CasesJudged = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<int>(type: "integer", nullable: false),
                    Scored = table.Column<int>(type: "integer", nullable: false),
                    PassRate = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdversarialRuns", x => x.AdversarialRunId);
                });

            migrationBuilder.CreateTable(
                name: "AdversarialCaseResults",
                columns: table => new
                {
                    AdversarialCaseResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdversarialRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseKey = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    History = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    PlantedClaim = table.Column<string>(type: "text", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    ToolCalls = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    BlockedByContentFilter = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    FailureType = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    PremiseHandling = table.Column<string>(type: "text", nullable: false),
                    Claims = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    Explanation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdversarialCaseResults", x => x.AdversarialCaseResultId);
                    table.ForeignKey(
                        name: "FK_AdversarialCaseResults_AdversarialRuns_AdversarialRunId",
                        column: x => x.AdversarialRunId,
                        principalTable: "AdversarialRuns",
                        principalColumn: "AdversarialRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdversarialCaseResults_AdversarialRunId",
                table: "AdversarialCaseResults",
                column: "AdversarialRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AdversarialCaseResults_CaseKey",
                table: "AdversarialCaseResults",
                column: "CaseKey");

            migrationBuilder.CreateIndex(
                name: "IX_AdversarialRuns_StartedAt",
                table: "AdversarialRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdversarialCaseResults");

            migrationBuilder.DropTable(
                name: "AdversarialRuns");
        }
    }
}
