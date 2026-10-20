using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.GestionPermisos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gestion_permisos");

            migrationBuilder.CreateTable(
                name: "solicitudes_permiso",
                schema: "gestion_permisos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    solicitante_id = table.Column<int>(type: "integer", nullable: false),
                    solicitante_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    solicitante_correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    permiso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    justificacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resuelta_por_id = table.Column<int>(type: "integer", nullable: true),
                    resuelta_por_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resuelta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aplicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes_permiso", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_permiso_estado",
                schema: "gestion_permisos",
                table: "solicitudes_permiso",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_permiso_solicitante_id",
                schema: "gestion_permisos",
                table: "solicitudes_permiso",
                column: "solicitante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitudes_permiso",
                schema: "gestion_permisos");
        }
    }
}
