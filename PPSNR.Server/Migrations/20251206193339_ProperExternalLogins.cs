using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server.Migrations
{
    /// <inheritdoc />
    public partial class ProperExternalLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Streamers_TwitchId",
                table: "Streamers");

            migrationBuilder.DropColumn(
                name: "TwitchId",
                table: "Streamers");

            migrationBuilder.DropColumn(
                name: "OwnerUserId1",
                table: "Pairs");

            migrationBuilder.DropColumn(
                name: "PartnerUserId1",
                table: "Pairs");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Streamers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

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

            migrationBuilder.CreateTable(
                name: "ExternalIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderAvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIdentities_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streamers_ApplicationUserId",
                table: "Streamers",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_ApplicationUserId_ProviderName_ProviderU~",
                table: "ExternalIdentities",
                columns: new[] { "ApplicationUserId", "ProviderName", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_ProviderName_ProviderUserId",
                table: "ExternalIdentities",
                columns: new[] { "ProviderName", "ProviderUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Streamers_AspNetUsers_ApplicationUserId",
                table: "Streamers",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Streamers_AspNetUsers_ApplicationUserId",
                table: "Streamers");

            migrationBuilder.DropTable(
                name: "ExternalIdentities");

            migrationBuilder.DropIndex(
                name: "IX_Streamers_ApplicationUserId",
                table: "Streamers");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Streamers");

            migrationBuilder.AddColumn<string>(
                name: "TwitchId",
                table: "Streamers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

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
                name: "IX_Streamers_TwitchId",
                table: "Streamers",
                column: "TwitchId");
        }
    }
}
