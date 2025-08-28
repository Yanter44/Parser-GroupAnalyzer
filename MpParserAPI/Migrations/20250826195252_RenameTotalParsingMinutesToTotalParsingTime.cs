using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpParserAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenameTotalParsingMinutesToTotalParsingTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalParsingMinutes",
                table: "ParsersStates",
                newName: "TotalParsingTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalParsingTime",
                table: "ParsersStates",
                newName: "TotalParsingMinutes");
        }
    }
}
