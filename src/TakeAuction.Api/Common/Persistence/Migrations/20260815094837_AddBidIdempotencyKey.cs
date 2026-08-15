using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBidIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "bids",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_bids_bidder_idempotency_key",
                table: "bids",
                columns: new[] { "BidderId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_bids_bidder_idempotency_key",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "bids");
        }
    }
}
