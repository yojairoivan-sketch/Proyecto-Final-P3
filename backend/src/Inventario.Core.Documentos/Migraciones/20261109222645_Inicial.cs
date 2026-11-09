using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.Documentos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documentos");

            migrationBuilder.CreateTable(
                name: "documentos",
                schema: "documentos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    subido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subido_por_id = table.Column<int>(type: "integer", nullable: false),
                    subido_por_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    archivo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    eliminado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eliminado_por_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_archivo",
                schema: "documentos",
                table: "documentos",
                column: "archivo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_subido_por_id",
                schema: "documentos",
                table: "documentos",
                column: "subido_por_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos",
                schema: "documentos");
        }
    }
}
