using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingJson",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "EmbeddingJson",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EmbeddingJson",
                table: "Information");

            migrationBuilder.CreateTable(
                name: "EmbeddingProjections",
                columns: table => new
                {
                    EmbeddingProjectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    SubLabel = table.Column<string>(type: "text", nullable: true),
                    X = table.Column<float>(type: "real", nullable: false),
                    Y = table.Column<float>(type: "real", nullable: false),
                    TechStack = table.Column<string>(type: "text", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingProjections", x => x.EmbeddingProjectionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProjections_EntityId",
                table: "EmbeddingProjections",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProjections_EntityType",
                table: "EmbeddingProjections",
                column: "EntityType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmbeddingProjections");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "WorkExperiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "Information",
                type: "text",
                nullable: true);
        }
    }
}
