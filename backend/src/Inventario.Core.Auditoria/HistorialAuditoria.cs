using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Inventario.Core.Auditoria;

/// <summary>
/// Interfaz provista a las piezas que se auditan: «deja este cambio en el historial».
/// <para>
/// Dónde se escribe el registro: en el mismo contexto de la pieza que hace el cambio, y se guarda con el mismo SaveChanges
/// (o dentro de la misma transacción). La tabla del historial está mapeada en el contexto de cada pieza auditada, pero solo
/// <see cref="AuditoriaDbContext"/> la crea en sus migraciones. Así, o se guardan el cambio y su registro, o ninguno de los dos:
/// no puede quedar un cambio sin su registro, ni un registro de un cambio que no pasó.
/// </para>
/// <para>
/// RD-03: el historial solo guarda textos (la entidad, su id y dos JSON). No conoce ninguna entidad de otra pieza ni del negocio;
/// qué entidades existen se lo dice el host en <see cref="OpcionesAuditoria"/>.
/// </para>
/// </summary>
public static class HistorialAuditoria
{
    public const string Esquema = "auditoria";
    public const string Tabla = "historial";

    /// <summary>Valor anterior de una entidad que se crea: no había nada (como el /dev/null de un archivo nuevo en git).</summary>
    public static readonly object NoExistia = new { estado = "No existía" };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Las tildes quedan tal cual («Estándar», no á): el historial se lee también con psql.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>
    /// Mapea la tabla del historial en el contexto de una pieza auditada, sin incluirla en sus migraciones.
    /// Se llama desde su OnModelCreating, junto con <see cref="ProtegerHistorialAuditoria"/> en su OnConfiguring.
    /// </summary>
    public static ModelBuilder MapearHistorialAuditoria(this ModelBuilder modelo) => Mapear(modelo, propietaria: false);

    /// <summary>Agrega <see cref="GuardaHistorial"/>: el contexto se niega a guardar un registro modificado o borrado (RF-AUD-06).</summary>
    public static DbContextOptionsBuilder ProtegerHistorialAuditoria(this DbContextOptionsBuilder opciones) =>
        opciones.AddInterceptors(GuardaHistorial.Instancia);

    /// <summary>
    /// Agrega al contexto el registro de un cambio. Se guarda con el próximo SaveChanges de ese contexto: llámalo antes de guardar
    /// el cambio, o dentro de la misma transacción. Los valores son objetos con solo los campos que cambian, por ejemplo
    /// <c>new { rol = "Estándar" }</c>; nunca contraseñas, hashes ni códigos (RF-AUD-05).
    /// </summary>
    public static RegistroAuditoria Auditar(
        this DbContext db,
        QuienActua quien,
        string accion,
        string entidad,
        object entidadId,
        object valorAnterior,
        object valorNuevo,
        DateTimeOffset fechaHora)
    {
        var registro = RegistroAuditoria.Nuevo(
            quien, accion, entidad, Convert.ToString(entidadId, CultureInfo.InvariantCulture) ?? "",
            ComoJson(valorAnterior, "anterior"), ComoJson(valorNuevo, "nuevo"), fechaHora);
        db.Set<RegistroAuditoria>().Add(registro);
        return registro;
    }

    internal static ModelBuilder Mapear(ModelBuilder modelo, bool propietaria)
    {
        modelo.Entity<RegistroAuditoria>(registro =>
        {
            registro.ToTable(Tabla, Esquema, tabla =>
            {
                if (!propietaria)
                    tabla.ExcludeFromMigrations();
            });
            registro.HasKey(r => r.Id);
            registro.Property(r => r.UsuarioCorreo).HasMaxLength(254);
            registro.Property(r => r.UsuarioRol).HasMaxLength(30);
            registro.Property(r => r.Accion).HasMaxLength(60);
            registro.Property(r => r.Entidad).HasMaxLength(60);
            registro.Property(r => r.EntidadId).HasMaxLength(64);
            registro.Property(r => r.ValorAnterior).HasMaxLength(RegistroAuditoria.MaximoValor);
            registro.Property(r => r.ValorNuevo).HasMaxLength(RegistroAuditoria.MaximoValor);
            registro.HasIndex(r => r.FechaHora);
            registro.HasIndex(r => r.UsuarioId);
            registro.HasIndex(r => new { r.Entidad, r.EntidadId });

            // RF-AUD-06: una vez guardado, cambiar cualquiera de sus columnas hace fallar el SaveChanges.
            foreach (var propiedad in registro.Metadata.GetProperties())
                propiedad.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        });
        return modelo;
    }

    private static string ComoJson(object valor, string cual)
    {
        ArgumentNullException.ThrowIfNull(valor, $"valor {cual}");
        var json = JsonSerializer.Serialize(valor, valor.GetType(), Json);
        // Siempre un objeto con nombres de campo: así se lee qué cambió, no solo un valor suelto.
        if (!json.StartsWith('{') || json == "{}")
            throw new ArgumentException($"El valor {cual} de un registro de auditoría es un objeto con al menos un campo, por ejemplo new {{ rol = \"Estándar\" }}.");
        return json;
    }
}
