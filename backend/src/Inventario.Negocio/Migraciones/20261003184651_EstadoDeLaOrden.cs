using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Negocio.Migraciones
{
    /// <inheritdoc />
    public partial class EstadoDeLaOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado",
                schema: "inventario",
                table: "ordenes_de_compra",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_de_compra_estado",
                schema: "inventario",
                table: "ordenes_de_compra",
                column: "estado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordenes_de_compra_estado",
                schema: "inventario",
                table: "ordenes_de_compra");

            migrationBuilder.DropColumn(
                name: "estado",
                schema: "inventario",
                table: "ordenes_de_compra");
        }
    }
}
