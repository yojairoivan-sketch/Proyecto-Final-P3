using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventario.Core.Auditoria.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.CreateTable(
                name: "historial",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    usuario_rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    valor_anterior = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    valor_nuevo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historial_entidad_entidad_id",
                schema: "auditoria",
                table: "historial",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_historial_fecha_hora",
                schema: "auditoria",
                table: "historial",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "ix_historial_usuario_id",
                schema: "auditoria",
                table: "historial",
                column: "usuario_id");

            // RF-AUD-06: la base misma rechaza modificar, borrar o vaciar el historial, venga de donde venga la orden
            // (la aplicación, un ExecuteUpdate o psql). Solo se pueden agregar filas.
            migrationBuilder.Sql("""
                CREATE FUNCTION auditoria.rechazar_cambio_historial() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'El historial de auditoría no se modifica ni se borra (RF-AUD-06).';
                END;
                $$;

                CREATE TRIGGER historial_sin_modificar_ni_borrar
                    BEFORE UPDATE OR DELETE ON auditoria.historial
                    FOR EACH ROW EXECUTE FUNCTION auditoria.rechazar_cambio_historial();

                CREATE TRIGGER historial_sin_vaciar
                    BEFORE TRUNCATE ON auditoria.historial
                    FOR EACH STATEMENT EXECUTE FUNCTION auditoria.rechazar_cambio_historial();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION auditoria.rechazar_cambio_historial() CASCADE;");
            migrationBuilder.DropTable(
                name: "historial",
                schema: "auditoria");
        }
    }
}
