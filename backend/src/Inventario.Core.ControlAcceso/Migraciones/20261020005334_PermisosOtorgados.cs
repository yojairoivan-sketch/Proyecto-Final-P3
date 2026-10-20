using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.ControlAcceso.Migraciones
{
    /// <inheritdoc />
    public partial class PermisosOtorgados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permisos_otorgados",
                schema: "control_acceso",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    permiso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    otorgado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permisos_otorgados", x => x.id);
                    table.ForeignKey(
                        name: "fk_permisos_otorgados_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "control_acceso",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_permisos_otorgados_usuario_id_permiso",
                schema: "control_acceso",
                table: "permisos_otorgados",
                columns: new[] { "usuario_id", "permiso" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permisos_otorgados",
                schema: "control_acceso");
        }
    }
}
