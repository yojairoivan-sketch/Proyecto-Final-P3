namespace Inventario.Core.Auditoria;

/// <summary>Quién hizo el cambio. Se copia en el registro: el historial no lee las tablas de usuarios de Control de acceso.</summary>
public sealed record QuienActua(int Id, string Correo, string Rol);

/// <summary>
/// Un registro del historial de auditoría (RF-AUD-01): quién actuó, qué acción, sobre qué entidad y cuál, cuándo, y el valor
/// anterior y el nuevo. Es como un commit: una vez guardado no cambia (RF-AUD-06). No tiene ningún método que lo modifique,
/// el modelo rechaza cambiar sus columnas, <see cref="GuardaHistorial"/> rechaza modificarlo o borrarlo, y en PostgreSQL un
/// trigger rechaza UPDATE, DELETE y TRUNCATE sobre la tabla.
/// </summary>
public sealed class RegistroAuditoria
{
    /// <summary>Lo que el registro guarda como texto, como máximo. Evita que un valor gigante llene la tabla.</summary>
    public const int MaximoValor = 4000;

    public long Id { get; private set; }

    /// <summary>RD-11: la misma hora que guarda el cambio, del mismo reloj del sistema, en UTC.</summary>
    public DateTimeOffset FechaHora { get; private set; }

    public int UsuarioId { get; private set; }
    public string UsuarioCorreo { get; private set; } = "";

    /// <summary>El rol que tenía al actuar. Si después le cambian el rol, el registro sigue diciendo con cuál lo hizo.</summary>
    public string UsuarioRol { get; private set; } = "";

    /// <summary>Qué hizo, por ejemplo CambiarRol o EliminarDocumento.</summary>
    public string Accion { get; private set; } = "";

    /// <summary>El tipo de lo que cambió, por ejemplo Usuario, SolicitudPermiso o Documento.</summary>
    public string Entidad { get; private set; } = "";

    /// <summary>Cuál: el identificador de la entidad. Es texto para no atar el historial al tipo de clave de ninguna pieza.</summary>
    public string EntidadId { get; private set; } = "";

    /// <summary>JSON con lo que había antes, solo los campos que cambian. Nunca vacío: si la entidad no existía, lo dice.</summary>
    public string ValorAnterior { get; private set; } = "";

    /// <summary>JSON con lo que quedó.</summary>
    public string ValorNuevo { get; private set; } = "";

    private RegistroAuditoria()
    {
    }

    internal static RegistroAuditoria Nuevo(
        QuienActua quien, string accion, string entidad, string entidadId, string valorAnterior, string valorNuevo, DateTimeOffset fechaHora)
    {
        // RF-AUD-01: un registro sin alguno de sus datos no cumple, así que ni siquiera se crea. Si pasa, es un error de
        // programación de la pieza que audita, no del usuario: por eso es una excepción y no un mensaje.
        ArgumentNullException.ThrowIfNull(quien);
        if (quien.Id <= 0)
            throw new ArgumentException("El registro de auditoría necesita el id del usuario que actuó.", nameof(quien));
        Requerido(quien.Correo, "el correo del usuario que actuó");
        Requerido(quien.Rol, "el rol del usuario que actuó");
        Requerido(accion, "la acción");
        Requerido(entidad, "la entidad");
        Requerido(entidadId, "el identificador de la entidad");
        Requerido(valorAnterior, "el valor anterior");
        Requerido(valorNuevo, "el valor nuevo");
        if (valorAnterior.Length > MaximoValor || valorNuevo.Length > MaximoValor)
            throw new ArgumentException($"Los valores de un registro de auditoría no pasan de {MaximoValor} caracteres.");

        return new RegistroAuditoria
        {
            FechaHora = fechaHora.ToUniversalTime(),
            UsuarioId = quien.Id,
            UsuarioCorreo = quien.Correo,
            UsuarioRol = quien.Rol,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            ValorAnterior = valorAnterior,
            ValorNuevo = valorNuevo,
        };
    }

    private static void Requerido(string? valor, string que)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException($"El registro de auditoría necesita {que}.");
    }
}
