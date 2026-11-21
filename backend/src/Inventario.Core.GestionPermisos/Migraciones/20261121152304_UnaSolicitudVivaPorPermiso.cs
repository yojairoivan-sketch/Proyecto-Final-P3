using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Core.GestionPermisos.Migraciones
{
    /// <inheritdoc />
    public partial class UnaSolicitudVivaPorPermiso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_solicitudes_permiso_una_viva",
                schema: "gestion_permisos",
                table: "solicitudes_permiso",
                columns: new[] { "solicitante_id", "permiso" },
                unique: true,
                filter: "estado <> 'Rechazada'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_solicitudes_permiso_una_viva",
                schema: "gestion_permisos",
                table: "solicitudes_permiso");
        }
    }
}
