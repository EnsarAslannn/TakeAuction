using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProxyBidding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutomatic",
                table: "bids",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAmount",
                table: "bids",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LeadingMaxAmount",
                table: "auctions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutomatic",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "MaxAmount",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "LeadingMaxAmount",
                table: "auctions");
        }
    }
}
