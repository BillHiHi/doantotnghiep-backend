using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class Update_ScreeningContractTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoldHourPercentage",
                table: "ScreeningContracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredGoldHourSlots",
                table: "ScreeningContracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoldHourPercentage",
                table: "ScreeningContracts");

            migrationBuilder.DropColumn(
                name: "RequiredGoldHourSlots",
                table: "ScreeningContracts");
        }
    }
}
