using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class ContractTheater_tbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TheaterMovies",
                table: "TheaterMovies");

            migrationBuilder.DropIndex(
                name: "IX_TheaterMovies_TheaterId",
                table: "TheaterMovies");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TheaterMovies",
                table: "TheaterMovies",
                columns: new[] { "TheaterId", "MovieId" });

            migrationBuilder.CreateTable(
                name: "ContractTheaters",
                columns: table => new
                {
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    TheaterId = table.Column<int>(type: "integer", nullable: false),
                    AllocatedSlots = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTheaters", x => new { x.ContractId, x.TheaterId });
                    table.ForeignKey(
                        name: "FK_ContractTheaters_ScreeningContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "ScreeningContracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractTheaters_Theaters_TheaterId",
                        column: x => x.TheaterId,
                        principalTable: "Theaters",
                        principalColumn: "TheaterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TheaterMovies_MovieId",
                table: "TheaterMovies",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTheaters_TheaterId",
                table: "ContractTheaters",
                column: "TheaterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTheaters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TheaterMovies",
                table: "TheaterMovies");

            migrationBuilder.DropIndex(
                name: "IX_TheaterMovies_MovieId",
                table: "TheaterMovies");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TheaterMovies",
                table: "TheaterMovies",
                columns: new[] { "MovieId", "TheaterId" });

            migrationBuilder.CreateIndex(
                name: "IX_TheaterMovies_TheaterId",
                table: "TheaterMovies",
                column: "TheaterId");
        }
    }
}
