using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Negocio.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventario");

            migrationBuilder.CreateTable(
                name: "productos",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidad_medida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    punto_reorden = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    existencia = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proveedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_de_compra",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proveedor_id = table.Column<int>(type: "integer", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordenes_de_compra", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordenes_de_compra_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalSchema: "inventario",
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lineas_orden_compra",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orden_de_compra_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas_orden_compra", x => x.id);
                    table.ForeignKey(
                        name: "fk_lineas_orden_compra_ordenes_de_compra_orden_de_compra_id",
                        column: x => x.orden_de_compra_id,
                        principalSchema: "inventario",
                        principalTable: "ordenes_de_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lineas_orden_compra_productos_producto_id",
                        column: x => x.producto_id,
                        principalSchema: "inventario",
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lineas_orden_compra_orden_de_compra_id",
                schema: "inventario",
                table: "lineas_orden_compra",
                column: "orden_de_compra_id");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_orden_compra_producto_id",
                schema: "inventario",
                table: "lineas_orden_compra",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_de_compra_proveedor_id",
                schema: "inventario",
                table: "ordenes_de_compra",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_productos_sku",
                schema: "inventario",
                table: "productos",
                column: "sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_orden_compra",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "ordenes_de_compra",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "productos",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "proveedores",
                schema: "inventario");
        }
    }
}
