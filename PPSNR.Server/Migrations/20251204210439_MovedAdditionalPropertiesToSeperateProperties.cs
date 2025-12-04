using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server.Migrations
{
    /// <inheritdoc />
    public partial class MovedAdditionalPropertiesToSeperateProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalProperties",
                table: "Slots");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Slots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Slots",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Height",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Slots");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalProperties",
                table: "Slots",
                type: "text",
                nullable: true);
        }
    }
}
