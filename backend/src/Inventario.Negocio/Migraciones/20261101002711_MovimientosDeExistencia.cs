using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Negocio.Migraciones
{
    /// <inheritdoc />
    public partial class MovimientosDeExistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "inventario",
                table: "productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "inventario",
                table: "ordenes_de_compra",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "movimientos_existencia",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    existencia_resultante = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden_de_compra_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registrado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimientos_existencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimientos_existencia_ordenes_de_compra_orden_de_compra_id",
                        column: x => x.orden_de_compra_id,
                        principalSchema: "inventario",
                        principalTable: "ordenes_de_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimientos_existencia_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "inventario",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_existencia_orden_de_compra_id",
                schema: "inventario",
                table: "movimientos_existencia",
                column: "orden_de_compra_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_existencia_producto_id_registrado_en",
                schema: "inventario",
                table: "movimientos_existencia",
                columns: new[] { "producto_id", "registrado_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimientos_existencia",
                schema: "inventario");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "inventario",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "inventario",
                table: "ordenes_de_compra");
        }
    }
}
