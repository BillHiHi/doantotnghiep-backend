using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class AddTheaterIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TheaterId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TheaterId",
                table: "Users",
                column: "TheaterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Theaters_TheaterId",
                table: "Users",
                column: "TheaterId",
                principalTable: "Theaters",
                principalColumn: "TheaterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Theaters_TheaterId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TheaterId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TheaterId",
                table: "Users");
        }
    }
}
