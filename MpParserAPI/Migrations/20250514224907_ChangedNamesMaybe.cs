using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpParserAPI.Migrations
{
    /// <inheritdoc />
    public partial class ChangedNamesMaybe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "ParserLogsTable",
                newName: "ParserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ParserId",
                table: "ParserLogsTable",
                newName: "ClientId");
        }
    }
}
