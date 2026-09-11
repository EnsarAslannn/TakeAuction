using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndWatchlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction_watches",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosingSoonNotifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction_watches", x => new { x.UserId, x.AuctionId });
                    table.ForeignKey(
                        name: "FK_auction_watches_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_auction_watches_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AuctionEndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auction_watches_awaiting_reminder",
                table: "auction_watches",
                column: "AuctionId",
                filter: "\"ClosingSoonNotifiedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_AuctionId",
                table: "notifications",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_unread",
                table: "notifications",
                column: "UserId",
                filter: "\"ReadAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_notifications_user_kind_auction",
                table: "notifications",
                columns: new[] { "UserId", "Kind", "AuctionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auction_watches");

            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
