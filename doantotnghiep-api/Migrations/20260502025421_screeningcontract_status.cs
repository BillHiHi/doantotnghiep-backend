using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class screeningcontract_status : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ScreeningContracts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ScreeningContracts");
        }
    }
}
