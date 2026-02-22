using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingJSON : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "WorkExperiences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "Information",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
