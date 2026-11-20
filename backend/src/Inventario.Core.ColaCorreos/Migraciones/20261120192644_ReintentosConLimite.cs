using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Core.ColaCorreos.Migraciones
{
    /// <inheritdoc />
    public partial class ReintentosConLimite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ultimo_intento_en",
                schema: "cola_correos",
                table: "correos_en_cola",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ultimo_intento_en",
                schema: "cola_correos",
                table: "correos_en_cola");
        }
    }
}
