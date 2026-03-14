using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace doantotnghiep_api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentCodeToBooking2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<string>(
            //    name: "Combos",
            //    table: "SeatLocks",
            //    type: "text",
            //    nullable: true);

            //migrationBuilder.AddColumn<string>(
            //    name: "PaymentCode",
            //    table: "SeatLocks",
            //    type: "text",
            //    nullable: true);

            //migrationBuilder.AddColumn<decimal>(
            //    name: "TotalAmount",
            //    table: "SeatLocks",
            //    type: "numeric",
            //    nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Combos",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCode",
                table: "Bookings",
                type: "text",
                nullable: true);

            //migrationBuilder.CreateTable(
            //    name: "Promotions",
            //    columns: table => new
            //    {
            //        PromotionId = table.Column<int>(type: "integer", nullable: false)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            //        Summary = table.Column<string>(type: "text", nullable: true),
            //        Content = table.Column<string>(type: "text", nullable: true),
            //        ImageUrl = table.Column<string>(type: "text", nullable: true),
            //        StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
            //        EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
            //        IsPublished = table.Column<bool>(type: "boolean", nullable: false),
            //        CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Promotions", x => x.PromotionId);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropColumn(
                name: "Combos",
                table: "SeatLocks");

            migrationBuilder.DropColumn(
                name: "PaymentCode",
                table: "SeatLocks");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "SeatLocks");

            migrationBuilder.DropColumn(
                name: "Combos",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PaymentCode",
                table: "Bookings");
        }
    }
}
