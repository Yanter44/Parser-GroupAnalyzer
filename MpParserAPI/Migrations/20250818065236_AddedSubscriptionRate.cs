using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpParserAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedSubscriptionRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubscriptionRate",
                table: "ParsersStates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionRate",
                table: "ParsersStates");
        }
    }
}
