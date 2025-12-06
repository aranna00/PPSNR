using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePairsForeignKeyDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerUserId",
                table: "Pairs");

            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerUserId",
                table: "Pairs");

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerUserId",
                table: "Pairs",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerUserId",
                table: "Pairs",
                column: "PartnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerUserId",
                table: "Pairs");

            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerUserId",
                table: "Pairs");

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerUserId",
                table: "Pairs",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerUserId",
                table: "Pairs",
                column: "PartnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
