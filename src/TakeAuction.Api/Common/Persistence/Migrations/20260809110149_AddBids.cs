using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BidCount",
                table: "auctions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadingBidderId",
                table: "auctions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bids_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bids_users_BidderId",
                        column: x => x.BidderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bids_AuctionId_Amount",
                table: "bids",
                columns: new[] { "AuctionId", "Amount" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_bids_BidderId",
                table: "bids",
                column: "BidderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropColumn(
                name: "BidCount",
                table: "auctions");

            migrationBuilder.DropColumn(
                name: "LeadingBidderId",
                table: "auctions");
        }
    }
}
