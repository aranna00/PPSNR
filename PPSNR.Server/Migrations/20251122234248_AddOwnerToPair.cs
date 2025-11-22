using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerToPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Pairs",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Pairs");
        }
    }
}
