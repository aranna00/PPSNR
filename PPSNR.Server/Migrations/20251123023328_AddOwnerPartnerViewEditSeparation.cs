using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPSNR.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerPartnerViewEditSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Slots_LayoutId_SlotType_Index",
                table: "Slots");

            migrationBuilder.AddColumn<int>(
                name: "Profile",
                table: "Slots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerViewToken",
                table: "PairLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerEditToken",
                table: "PairLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Slots_LayoutId_SlotType_Index_Profile",
                table: "Slots",
                columns: new[] { "LayoutId", "SlotType", "Index", "Profile" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Slots_LayoutId_SlotType_Index_Profile",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "Profile",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "OwnerViewToken",
                table: "PairLinks");

            migrationBuilder.DropColumn(
                name: "PartnerEditToken",
                table: "PairLinks");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_LayoutId_SlotType_Index",
                table: "Slots",
                columns: new[] { "LayoutId", "SlotType", "Index" },
                unique: true);
        }
    }
}
