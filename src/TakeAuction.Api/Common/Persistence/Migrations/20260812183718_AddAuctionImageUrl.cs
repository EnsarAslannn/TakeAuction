using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "auctions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "auctions");
        }
    }
}
