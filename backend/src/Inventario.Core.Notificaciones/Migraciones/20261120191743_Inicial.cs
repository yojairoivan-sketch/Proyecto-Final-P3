using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.Notificaciones.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notificaciones");

            migrationBuilder.CreateTable(
                name: "notificaciones",
                schema: "notificaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    leida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "preferencias",
                schema: "notificaciones",
                columns: table => new
                {
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    correo_activo = table.Column<bool>(type: "boolean", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preferencias", x => x.usuario_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_usuario_id_leida_en",
                schema: "notificaciones",
                table: "notificaciones",
                columns: new[] { "usuario_id", "leida_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones",
                schema: "notificaciones");

            migrationBuilder.DropTable(
                name: "preferencias",
                schema: "notificaciones");
        }
    }
}
