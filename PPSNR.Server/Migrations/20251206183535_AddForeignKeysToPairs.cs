using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeysToPairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerId",
                table: "Pairs");

            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerId",
                table: "Pairs");

            migrationBuilder.DropIndex(
                name: "IX_Pairs_OwnerId",
                table: "Pairs");

            migrationBuilder.DropIndex(
                name: "IX_Pairs_PartnerId",
                table: "Pairs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Pairs");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Pairs");

            migrationBuilder.AlterColumn<string>(
                name: "PartnerUserId",
                table: "Pairs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerUserId",
                table: "Pairs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId1",
                table: "Pairs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerUserId1",
                table: "Pairs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pairs_OwnerUserId",
                table: "Pairs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pairs_PartnerUserId",
                table: "Pairs",
                column: "PartnerUserId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerUserId",
                table: "Pairs");

            migrationBuilder.DropForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerUserId",
                table: "Pairs");

            migrationBuilder.DropIndex(
                name: "IX_Pairs_OwnerUserId",
                table: "Pairs");

            migrationBuilder.DropIndex(
                name: "IX_Pairs_PartnerUserId",
                table: "Pairs");

            migrationBuilder.DropColumn(
                name: "OwnerUserId1",
                table: "Pairs");

            migrationBuilder.DropColumn(
                name: "PartnerUserId1",
                table: "Pairs");

            migrationBuilder.AlterColumn<string>(
                name: "PartnerUserId",
                table: "Pairs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerUserId",
                table: "Pairs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Pairs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerId",
                table: "Pairs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pairs_OwnerId",
                table: "Pairs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Pairs_PartnerId",
                table: "Pairs",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_OwnerId",
                table: "Pairs",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Pairs_AspNetUsers_PartnerId",
                table: "Pairs",
                column: "PartnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
