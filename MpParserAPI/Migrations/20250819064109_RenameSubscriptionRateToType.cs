using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpParserAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenameSubscriptionRateToType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubscriptionRate",
                table: "ParsersStates",
                newName: "SubscriptionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubscriptionType",
                table: "ParsersStates",
                newName: "SubscriptionRate");
        }
    }
}
