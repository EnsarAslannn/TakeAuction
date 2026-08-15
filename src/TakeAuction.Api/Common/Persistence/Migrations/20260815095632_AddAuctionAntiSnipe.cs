using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionAntiSnipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AntiSnipeExtensionSeconds",
                table: "auctions",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "AntiSnipeWindowSeconds",
                table: "auctions",
                type: "integer",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AntiSnipeExtensionSeconds",
                table: "auctions");

            migrationBuilder.DropColumn(
                name: "AntiSnipeWindowSeconds",
                table: "auctions");
        }
    }
}
