using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class movietheater_tbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ ĐÃ ẨN: Bỏ qua lỗi khóa ngoại
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Theaters_Regions_RegionId",
            //     table: "Theaters");

            // ✅ ĐÃ ẨN: Bảng Regions đã không còn trong DB thực tế nên không cần Drop nữa (Fix lỗi 42P01)
            // migrationBuilder.DropTable(
            //     name: "Regions");

            // Nếu 2 lệnh dưới đây (DropIndex và DropColumn) tiếp tục báo lỗi "không tồn tại", 
            // bạn cũng có thể tự thêm dấu // vào trước chúng nhé. Hiện tại cứ để nguyên cho an toàn.
            migrationBuilder.DropIndex(
                name: "IX_Theaters_RegionId",
                table: "Theaters");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Theaters");

            migrationBuilder.CreateTable(
                name: "TheaterMovies",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    TheaterId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheaterMovies", x => new { x.MovieId, x.TheaterId });
                    table.ForeignKey(
                        name: "FK_TheaterMovies_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "MovieId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TheaterMovies_Theaters_TheaterId",
                        column: x => x.TheaterId,
                        principalTable: "Theaters",
                        principalColumn: "TheaterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TheaterMovies_TheaterId",
                table: "TheaterMovies",
                column: "TheaterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TheaterMovies");

            migrationBuilder.AddColumn<int>(
                name: "RegionId",
                table: "Theaters",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    RegionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.RegionId);
                    table.ForeignKey(
                        name: "FK_Regions_Regions_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Regions",
                        principalColumn: "RegionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Theaters_RegionId",
                table: "Theaters",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_ParentId",
                table: "Regions",
                column: "ParentId");

            // ✅ ĐÃ ẨN: Bỏ qua lỗi khóa ngoại khi Rollback
            // migrationBuilder.AddForeignKey(
            //     name: "FK_Theaters_Regions_RegionId",
            //     table: "Theaters",
            //     column: "RegionId",
            //     principalTable: "Regions",
            //     principalColumn: "RegionId");
        }
    }
}