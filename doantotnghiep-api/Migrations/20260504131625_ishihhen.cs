using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class ishihhen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Seats",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Seats");
        }
    }
}
