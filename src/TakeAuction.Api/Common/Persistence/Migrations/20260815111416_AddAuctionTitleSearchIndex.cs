using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TakeAuction.Api.Common.Persistence.Migrations
{
    /// <summary>
    /// The salon's search runs a case-insensitive substring match, which no B-tree can serve:
    /// every query read the whole table. A trigram GIN index over the lowered title is the one
    /// index type that answers a LIKE with a leading wildcard.
    ///
    /// Written by hand because the expression and the operator class are beyond what the model
    /// builder can describe — which is also why it does not appear in the snapshot.
    /// </summary>
    /// <inheritdoc />
    public partial class AddAuctionTitleSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_auctions_title_trgm"
                ON auctions
                USING gin (lower("Title") gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_auctions_title_trgm";""");

            // The extension is left in place on purpose: something else may have come to rely
            // on it, and dropping a database-wide extension to undo one index is not a trade
            // this migration gets to make.
        }
    }
}
