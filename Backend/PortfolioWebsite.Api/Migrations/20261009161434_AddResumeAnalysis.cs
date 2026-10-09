using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResumeAnalyses",
                columns: table => new
                {
                    ResumeAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "running"),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeAnalyses", x => x.ResumeAnalysisId);
                    table.ForeignKey(
                        name: "FK_ResumeAnalyses_ResumeFiles_ResumeFileId",
                        column: x => x.ResumeFileId,
                        principalTable: "ResumeFiles",
                        principalColumn: "ResumeFileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeSuggestions",
                columns: table => new
                {
                    ResumeSuggestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Changes = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeSuggestions", x => x.ResumeSuggestionId);
                    table.ForeignKey(
                        name: "FK_ResumeSuggestions_ResumeAnalyses_ResumeAnalysisId",
                        column: x => x.ResumeAnalysisId,
                        principalTable: "ResumeAnalyses",
                        principalColumn: "ResumeAnalysisId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_ResumeFileId",
                table: "ResumeAnalyses",
                column: "ResumeFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_StartedAt",
                table: "ResumeAnalyses",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeSuggestions_ResumeAnalysisId",
                table: "ResumeSuggestions",
                column: "ResumeAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeSuggestions_Status_ResumeAnalysisId",
                table: "ResumeSuggestions",
                columns: new[] { "Status", "ResumeAnalysisId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResumeSuggestions");

            migrationBuilder.DropTable(
                name: "ResumeAnalyses");
        }
    }
}
