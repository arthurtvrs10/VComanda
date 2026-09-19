using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VarthexComanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProdutoFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "foto_arquivo",
                table: "produto",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "foto_arquivo",
                table: "produto");
        }
    }
}
