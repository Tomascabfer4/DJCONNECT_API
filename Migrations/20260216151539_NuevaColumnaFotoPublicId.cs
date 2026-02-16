using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API_DJCONNECT.Migrations
{
    /// <inheritdoc />
    public partial class NuevaColumnaFotoPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoPerfilPublicId",
                table: "usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoPerfilPublicId",
                table: "usuarios");
        }
    }
}
