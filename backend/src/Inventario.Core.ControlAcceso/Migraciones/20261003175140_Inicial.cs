using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventario.Core.ControlAcceso.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "control_acceso");

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "control_acceso",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "control_acceso",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    hash_contrasena = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rol_id = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    activado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_rol_id",
                        column: x => x.rol_id,
                        principalSchema: "control_acceso",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tokens_activacion",
                schema: "control_acceso",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    hash_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    emitido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vence_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usado = table.Column<bool>(type: "boolean", nullable: false),
                    usado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_activacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_tokens_activacion_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "control_acceso",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "control_acceso",
                table: "roles",
                columns: new[] { "id", "nombre" },
                values: new object[,]
                {
                    { 1, "Administrador" },
                    { 2, "Estándar" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_nombre",
                schema: "control_acceso",
                table: "roles",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_activacion_hash_codigo",
                schema: "control_acceso",
                table: "tokens_activacion",
                column: "hash_codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_activacion_usuario_id",
                schema: "control_acceso",
                table: "tokens_activacion",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_correo",
                schema: "control_acceso",
                table: "usuarios",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_rol_id",
                schema: "control_acceso",
                table: "usuarios",
                column: "rol_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens_activacion",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "control_acceso");
        }
    }
}
