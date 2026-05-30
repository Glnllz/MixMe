using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MixMe.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisputeNote",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeNote",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "Orders");
        }
    }
}
