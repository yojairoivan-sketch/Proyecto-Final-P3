using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Core.ControlAcceso.Migraciones
{
    /// <inheritdoc />
    public partial class BloqueoPorIntentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bloqueado_hasta",
                schema: "control_acceso",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intentos_fallidos",
                schema: "control_acceso",
                table: "usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bloqueado_hasta",
                schema: "control_acceso",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "intentos_fallidos",
                schema: "control_acceso",
                table: "usuarios");
        }
    }
}
