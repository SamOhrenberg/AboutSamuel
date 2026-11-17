using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminMcpOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminMcpAuthorizations",
                columns: table => new
                {
                    AdminMcpAuthorizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    RedirectUri = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: true),
                    CodeChallenge = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    BrowserKeyHash = table.Column<string>(type: "text", nullable: false),
                    ApprovalTokenHash = table.Column<string>(type: "text", nullable: false),
                    UserCodeHash = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    RequestIp = table.Column<string>(type: "text", nullable: true),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminMcpAuthorizations", x => x.AdminMcpAuthorizationId);
                });

            migrationBuilder.CreateTable(
                name: "AdminMcpClients",
                columns: table => new
                {
                    AdminMcpClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RedirectUris = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedIp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminMcpClients", x => x.AdminMcpClientId);
                });

            migrationBuilder.CreateTable(
                name: "AdminMcpRefreshTokens",
                columns: table => new
                {
                    AdminMcpRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FamilyExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminMcpRefreshTokens", x => x.AdminMcpRefreshTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminMcpAuthorizations_ApprovalTokenHash",
                table: "AdminMcpAuthorizations",
                column: "ApprovalTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminMcpAuthorizations_CodeHash",
                table: "AdminMcpAuthorizations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminMcpAuthorizations_ExpiresAt",
                table: "AdminMcpAuthorizations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminMcpRefreshTokens_FamilyId",
                table: "AdminMcpRefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminMcpRefreshTokens_TokenHash",
                table: "AdminMcpRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminMcpAuthorizations");

            migrationBuilder.DropTable(
                name: "AdminMcpClients");

            migrationBuilder.DropTable(
                name: "AdminMcpRefreshTokens");
        }
    }
}
