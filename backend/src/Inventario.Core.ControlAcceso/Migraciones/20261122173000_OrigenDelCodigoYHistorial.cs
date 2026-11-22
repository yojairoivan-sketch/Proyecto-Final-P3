using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Core.ControlAcceso.Migraciones
{
    /// <inheritdoc />
    public partial class OrigenDelCodigoYHistorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los códigos que ya existían los pidió el propio usuario: hasta ahora no se distinguía. La tabla del historial
            // de auditoría está en el modelo, pero no se crea aquí: la crea la migración de la pieza de Auditoría.
            migrationBuilder.AddColumn<string>(
                name: "origen",
                schema: "control_acceso",
                table: "codigos_recuperacion",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PropioUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origen",
                schema: "control_acceso",
                table: "codigos_recuperacion");
        }
    }
}
