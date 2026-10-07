using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruiterTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecruiterEmails",
                columns: table => new
                {
                    RecruiterEmailId = table.Column<Guid>(type: "uuid", nullable: false),
                    GmailMessageId = table.Column<string>(type: "text", nullable: false),
                    GmailThreadId = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: true),
                    FromAddress = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Personalized = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false, defaultValue: "none"),
                    GmailDraftId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterEmails", x => x.RecruiterEmailId);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterPostings",
                columns: table => new
                {
                    RecruiterPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    HiringCompany = table.Column<string>(type: "text", nullable: true),
                    WorkArrangement = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    EmploymentType = table.Column<string>(type: "text", nullable: false),
                    ContractTerms = table.Column<string>(type: "text", nullable: false),
                    AnnualPay = table.Column<double>(type: "double precision", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Reasons = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    Missing = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    Conflicts = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CanReply = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterPostings", x => x.RecruiterPostingId);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterTriageSettings",
                columns: table => new
                {
                    RecruiterTriageSettingsId = table.Column<int>(type: "integer", nullable: false),
                    TriageEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShadowMode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RemotePayFloor = table.Column<double>(type: "double precision", nullable: false),
                    HybridPayFloor = table.Column<double>(type: "double precision", nullable: false),
                    OnsitePayFloor = table.Column<double>(type: "double precision", nullable: false),
                    HoursPerYear = table.Column<double>(type: "double precision", nullable: false),
                    AllowRemote = table.Column<bool>(type: "boolean", nullable: false),
                    AllowHybrid = table.Column<bool>(type: "boolean", nullable: false),
                    AllowOnsite = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptableLocations = table.Column<string>(type: "text", nullable: false),
                    HomeState = table.Column<string>(type: "text", nullable: false, defaultValue: "OK"),
                    AllowedEmploymentTypes = table.Column<string>(type: "text", nullable: false),
                    DealbreakerContractTerms = table.Column<string>(type: "text", nullable: false),
                    FreeTextRequirements = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterTriageSettings", x => x.RecruiterTriageSettingsId);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterPitches",
                columns: table => new
                {
                    RecruiterPitchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterEmailId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruitingAgency = table.Column<string>(type: "text", nullable: true),
                    RecruiterName = table.Column<string>(type: "text", nullable: true),
                    AnnualPay = table.Column<double>(type: "double precision", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterPitches", x => x.RecruiterPitchId);
                    table.ForeignKey(
                        name: "FK_RecruiterPitches_RecruiterEmails_RecruiterEmailId",
                        column: x => x.RecruiterEmailId,
                        principalTable: "RecruiterEmails",
                        principalColumn: "RecruiterEmailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecruiterPitches_RecruiterPostings_RecruiterPostingId",
                        column: x => x.RecruiterPostingId,
                        principalTable: "RecruiterPostings",
                        principalColumn: "RecruiterPostingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RecruiterTriageSettings",
                columns: new[] { "RecruiterTriageSettingsId", "AcceptableLocations", "AllowHybrid", "AllowOnsite", "AllowRemote", "AllowedEmploymentTypes", "DealbreakerContractTerms", "FreeTextRequirements", "HomeState", "HoursPerYear", "HybridPayFloor", "LastSyncedAt", "OnsitePayFloor", "RemotePayFloor", "ShadowMode", "TriageEnabled", "UpdatedAt" },
                values: new object[] { 1, "[\"Oklahoma City\",\"OKC\",\"Edmond\",\"Moore\",\"Midwest City\",\"Del City\",\"Yukon\",\"Mustang\",\"Bethany\",\"Warr Acres\",\"Nichols Hills\",\"The Village\",\"Choctaw\",\"Norman\",\"Tinker AFB\",\"Tinker Air Force Base\",\"Piedmont\",\"Newcastle\",\"Spencer\",\"Harrah\",\"Jones\"]", true, true, true, "[\"full_time\",\"contract_to_hire\"]", "[\"c2c\",\"1099\"]", "The role must be primarily hands-on software development or AI engineering, where I'm writing code. Not desktop support, help desk, manual QA, project management, security analysis, or BI reporting without real development.", "OK", 2080.0, 90000.0, null, 90000.0, 90000.0, true, false, new DateTimeOffset(new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterEmails_GmailMessageId",
                table: "RecruiterEmails",
                column: "GmailMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterEmails_GmailThreadId",
                table: "RecruiterEmails",
                column: "GmailThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterPitches_RecruiterEmailId",
                table: "RecruiterPitches",
                column: "RecruiterEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterPitches_RecruiterPostingId",
                table: "RecruiterPitches",
                column: "RecruiterPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterPostings_LastSeenAt",
                table: "RecruiterPostings",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterPostings_ReviewStatus_LastSeenAt",
                table: "RecruiterPostings",
                columns: new[] { "ReviewStatus", "LastSeenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecruiterPitches");

            migrationBuilder.DropTable(
                name: "RecruiterTriageSettings");

            migrationBuilder.DropTable(
                name: "RecruiterEmails");

            migrationBuilder.DropTable(
                name: "RecruiterPostings");
        }
    }
}
