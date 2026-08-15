using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <summary>
    /// AddProxyBidding gave every existing row a ceiling of zero, which is not a neutral
    /// default: it says the leader agreed to nothing. Read literally by the new bidding rules,
    /// the next challenger on a lot standing at 159,500 would take it for one increment, and
    /// the price would collapse.
    ///
    /// A bid placed before proxy bidding existed was a flat commitment to its own amount, so
    /// that is exactly the ceiling it gets — no more, since nobody ever agreed to more.
    /// </summary>
    /// <inheritdoc />
    public partial class BackfillProxyBiddingCeilings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE bids SET "MaxAmount" = "Amount" WHERE "MaxAmount" < "Amount";""");

            migrationBuilder.Sql(
                """
                UPDATE auctions SET "LeadingMaxAmount" = "CurrentPrice"
                WHERE "BidCount" > 0 AND "LeadingMaxAmount" < "CurrentPrice";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo. Putting the zeroes back would restore the very state this
            // migration exists to repair.
        }
    }
}
