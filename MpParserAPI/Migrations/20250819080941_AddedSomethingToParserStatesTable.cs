using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpParserAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedSomethingToParserStatesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidMinutes",
                table: "ParsersStates");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionEndDate",
                table: "ParsersStates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "ParsersStates");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PaidMinutes",
                table: "ParsersStates",
                type: "interval",
                nullable: true);
        }
    }
}
