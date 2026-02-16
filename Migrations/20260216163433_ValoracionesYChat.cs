using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API_DJCONNECT.Migrations
{
    /// <inheritdoc />
    public partial class ValoracionesYChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mensajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservaId = table.Column<int>(type: "integer", nullable: false),
                    EmisorId = table.Column<int>(type: "integer", nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mensajes_reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mensajes_usuarios_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valoraciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservaId = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    DjId = table.Column<int>(type: "integer", nullable: false),
                    Puntuacion = table.Column<int>(type: "integer", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valoraciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_valoraciones_reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_valoraciones_usuarios_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valoraciones_usuarios_DjId",
                        column: x => x.DjId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_EmisorId",
                table: "mensajes",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_ReservaId",
                table: "mensajes",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_valoraciones_ClienteId",
                table: "valoraciones",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_valoraciones_DjId",
                table: "valoraciones",
                column: "DjId");

            migrationBuilder.CreateIndex(
                name: "IX_valoraciones_ReservaId",
                table: "valoraciones",
                column: "ReservaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensajes");

            migrationBuilder.DropTable(
                name: "valoraciones");
        }
    }
}
