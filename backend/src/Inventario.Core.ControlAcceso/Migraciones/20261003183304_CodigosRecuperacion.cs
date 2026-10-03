using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.ControlAcceso.Migraciones
{
    /// <inheritdoc />
    public partial class CodigosRecuperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "codigos_recuperacion",
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
                    table.PrimaryKey("pk_codigos_recuperacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_codigos_recuperacion_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "control_acceso",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_codigos_recuperacion_hash_codigo",
                schema: "control_acceso",
                table: "codigos_recuperacion",
                column: "hash_codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_codigos_recuperacion_usuario_id",
                schema: "control_acceso",
                table: "codigos_recuperacion",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigos_recuperacion",
                schema: "control_acceso");
        }
    }
}
