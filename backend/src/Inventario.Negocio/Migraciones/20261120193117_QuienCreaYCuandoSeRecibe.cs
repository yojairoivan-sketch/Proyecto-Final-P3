using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Negocio.Migraciones
{
    /// <inheritdoc />
    public partial class QuienCreaYCuandoSeRecibe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "creada_por_id",
                schema: "inventario",
                table: "ordenes_de_compra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recibida_en",
                schema: "inventario",
                table: "ordenes_de_compra",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_de_compra_estado_recibida_en",
                schema: "inventario",
                table: "ordenes_de_compra",
                columns: new[] { "estado", "recibida_en" });

            // Las órdenes recibidas antes de esta migración: su último cambio fue la recepción (Recibida es terminal).
            migrationBuilder.Sql("UPDATE inventario.ordenes_de_compra SET recibida_en = actualizada_en WHERE estado = 'Recibida';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordenes_de_compra_estado_recibida_en",
                schema: "inventario",
                table: "ordenes_de_compra");

            migrationBuilder.DropColumn(
                name: "creada_por_id",
                schema: "inventario",
                table: "ordenes_de_compra");

            migrationBuilder.DropColumn(
                name: "recibida_en",
                schema: "inventario",
                table: "ordenes_de_compra");
        }
    }
}
