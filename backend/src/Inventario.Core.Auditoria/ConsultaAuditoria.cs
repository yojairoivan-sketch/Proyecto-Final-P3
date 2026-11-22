using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Auditoria;

/// <summary>Quién pide la consulta. Lo arma el host con la sesión ya validada en el servidor (RD-06).</summary>
public sealed record Consultante(int Id, string Rol, bool EsAdministrador);

/// <summary>Los filtros tal como llegan (texto). Todos son opcionales; sin ninguno, salen los registros más recientes.</summary>
/// <param name="Usuario">Quién actuó: su id (un número) o su correo.</param>
/// <param name="Entidad">El tipo de lo que cambió, por ejemplo Usuario o Documento.</param>
/// <param name="EntidadId">Cuál: necesita <paramref name="Entidad"/>. Como <c>git log -- archivo</c>, el historial de una sola cosa.</param>
/// <param name="Desde">Primer día, AAAA-MM-DD, completo.</param>
/// <param name="Hasta">Último día, AAAA-MM-DD, completo.</param>
public sealed record FiltroAuditoria(
    string? Usuario = null, string? Entidad = null, string? EntidadId = null, string? Desde = null, string? Hasta = null);

/// <summary>Un registro tal como se muestra. Los valores salen como JSON, no como texto escapado.</summary>
public sealed record RegistroVista(
    long Id,
    DateTimeOffset FechaHora,
    int UsuarioId,
    string UsuarioCorreo,
    string UsuarioRol,
    string Accion,
    string Entidad,
    string EntidadId,
    JsonElement ValorAnterior,
    JsonElement ValorNuevo);

/// <summary>Cuántos registros cumplen el filtro y los más recientes de ellos (hasta <see cref="IConsultaAuditoria.Limite"/>).</summary>
public sealed record HistorialVista(int Total, IReadOnlyList<RegistroVista> Registros);

public enum FalloConsulta
{
    /// <summary>Quien consulta no es Administrador (RF-AUD-07).</summary>
    Prohibido,

    /// <summary>Un filtro no se puede leer (RD-07).</summary>
    FiltroInvalido,
}

/// <summary>La consulta o, si se rechaza, por qué.</summary>
public sealed record ResultadoConsultaAuditoria(HistorialVista? Vista, FalloConsulta? Fallo, string? Mensaje);

/// <summary>
/// RF-AUD-07: consulta del historial, filtrable por usuario, entidad y rango de fechas. Solo lectura: la pieza no ofrece ninguna
/// operación que modifique o borre registros (RF-AUD-06).
/// </summary>
public interface IConsultaAuditoria
{
    public const int Limite = 500;

    /// <summary>
    /// Los registros que cumplen el filtro, del más reciente al más antiguo, como <c>git log</c>. Reservada al Administrador:
    /// la regla vive aquí, en un solo lugar, y por eso se prueba sin levantar la API.
    /// </summary>
    Task<ResultadoConsultaAuditoria> ConsultarAsync(Consultante? quien, FiltroAuditoria filtro, CancellationToken ct = default);
}

internal sealed class ConsultaAuditoria(AuditoriaDbContext db, OpcionesAuditoria opciones) : IConsultaAuditoria
{
    public const string Operacion = "ConsultarAuditoria";

    public async Task<ResultadoConsultaAuditoria> ConsultarAsync(Consultante? quien, FiltroAuditoria filtro, CancellationToken ct = default)
    {
        // Primero quién pregunta: a un Estándar no se le dice nada más, ni siquiera si su filtro estaba mal.
        if (quien is null)
            return Rechazo(FalloConsulta.Prohibido, "Inicia sesión como Administrador para consultar el historial de auditoría.");
        if (!quien.EsAdministrador)
            return Rechazo(FalloConsulta.Prohibido, $"La operación {Operacion} es solo para: Administrador. Tu rol es {quien.Rol}.");

        var (valido, error) = FiltroValido.Leer(filtro, opciones);
        if (valido is null)
            return Rechazo(FalloConsulta.FiltroInvalido, error!);

        var consulta = db.Historial.AsNoTracking();
        if (valido.UsuarioId is { } usuarioId)
            consulta = consulta.Where(r => r.UsuarioId == usuarioId);
        if (valido.UsuarioCorreo is { } correo)
            consulta = consulta.Where(r => r.UsuarioCorreo == correo);
        if (valido.Entidad is { } entidad)
            consulta = consulta.Where(r => r.Entidad == entidad);
        if (valido.EntidadId is { } entidadId)
            consulta = consulta.Where(r => r.EntidadId == entidadId);
        if (valido.Inicio is { } inicio)
            consulta = consulta.Where(r => r.FechaHora >= inicio);
        if (valido.FinExclusivo is { } fin)
            consulta = consulta.Where(r => r.FechaHora < fin);

        var total = await consulta.CountAsync(ct);
        // El id crece con cada registro: ordenar por él es ordenar por el momento en que se guardó.
        var registros = await consulta.OrderByDescending(r => r.Id).Take(IConsultaAuditoria.Limite).ToListAsync(ct);

        return new ResultadoConsultaAuditoria(new HistorialVista(total, registros.Select(Vista).ToList()), null, null);
    }

    private static RegistroVista Vista(RegistroAuditoria r) => new(
        r.Id, r.FechaHora, r.UsuarioId, r.UsuarioCorreo, r.UsuarioRol, r.Accion, r.Entidad, r.EntidadId,
        Json(r.ValorAnterior), Json(r.ValorNuevo));

    private static JsonElement Json(string texto)
    {
        using var documento = JsonDocument.Parse(texto);
        return documento.RootElement.Clone();
    }

    private static ResultadoConsultaAuditoria Rechazo(FalloConsulta fallo, string mensaje) => new(null, fallo, mensaje);
}

/// <summary>Los filtros ya leídos. Las fechas son los límites del rango, en UTC, como se guardan.</summary>
internal sealed record FiltroValido(
    int? UsuarioId, string? UsuarioCorreo, string? Entidad, string? EntidadId, DateTimeOffset? Inicio, DateTimeOffset? FinExclusivo)
{
    public const string FormatoFecha = "yyyy-MM-dd";
    public const int PrimerAnio = 1900;
    public const int UltimoAnio = 2100;

    /// <summary>Lee cada filtro. Si alguno no sirve, el mensaje dice cuál y cómo escribirlo (RD-07).</summary>
    public static (FiltroValido? Filtro, string? Error) Leer(FiltroAuditoria filtro, OpcionesAuditoria opciones)
    {
        int? usuarioId = null;
        string? correo = null;
        if (Recortado(filtro.Usuario) is { } usuario)
        {
            if (usuario.All(char.IsAsciiDigit))
            {
                if (!int.TryParse(usuario, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    return Error($"El usuario «{usuario}» no existe: los ids de usuario son números desde 1.");
                usuarioId = id;
            }
            else if (EsCorreo(usuario))
            {
                correo = usuario.ToLowerInvariant();
            }
            else
            {
                return Error($"El filtro usuario «{usuario}» no es válido: usa el id del usuario (un número, por ejemplo 2) " +
                    "o su correo (por ejemplo ana@ejemplo.com).");
            }
        }

        string? entidad = null;
        if (Recortado(filtro.Entidad) is { } textoEntidad)
        {
            entidad = opciones.Entidades.FirstOrDefault(e => string.Equals(e, textoEntidad, StringComparison.OrdinalIgnoreCase));
            if (entidad is null)
                return Error($"La entidad «{textoEntidad}» no existe en el historial. Usa una de estas: " +
                    $"{string.Join(", ", opciones.Entidades.Order(StringComparer.Ordinal))}.");
        }

        var entidadId = Recortado(filtro.EntidadId);
        if (entidadId is not null)
        {
            if (entidad is null)
                return Error("Para filtrar por entidadId indica también la entidad, por ejemplo ?entidad=Documento&entidadId=3.");
            if (entidadId.Length > 64 || !entidadId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                return Error($"El identificador «{entidadId}» no es válido: usa solo letras, números o guiones, hasta 64.");
        }

        DateOnly? desde = null, hasta = null;
        if (Recortado(filtro.Desde) is { } textoDesde)
        {
            if (Fecha(textoDesde, "desde", out var dia) is { } errorDesde)
                return Error(errorDesde);
            desde = dia;
        }
        if (Recortado(filtro.Hasta) is { } textoHasta)
        {
            if (Fecha(textoHasta, "hasta", out var dia) is { } errorHasta)
                return Error(errorHasta);
            hasta = dia;
        }
        if (desde > hasta)
            return Error($"La fecha desde ({desde:yyyy-MM-dd}) es posterior a hasta ({hasta:yyyy-MM-dd}). Invierte las fechas.");

        // Los dos días se incluyen completos, en el día del negocio: desde a las 00:00 hasta el día siguiente a hasta a las 00:00.
        return (new FiltroValido(usuarioId, correo, entidad, entidadId,
            desde is { } d ? Medianoche(d, opciones) : null,
            hasta is { } h ? Medianoche(h.AddDays(1), opciones) : null), null);
    }

    private static string? Fecha(string texto, string cual, out DateOnly dia)
    {
        if (!DateOnly.TryParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out dia))
            return $"La fecha {cual} «{texto}» no es válida. Usa el formato AAAA-MM-DD, por ejemplo 2026-11-22.";
        if (dia.Year is < PrimerAnio or > UltimoAnio)
            return $"Las fechas deben estar entre {PrimerAnio} y {UltimoAnio}.";
        return null;
    }

    /// <summary>Medianoche de ese día en la hora del negocio, expresada en UTC: así se guardan las fechas (RD-11).</summary>
    private static DateTimeOffset Medianoche(DateOnly dia, OpcionesAuditoria opciones) =>
        new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), opciones.DesfaseNegocio).ToUniversalTime();

    private static bool EsCorreo(string texto) =>
        texto.Length <= 254 && texto.IndexOf('@') is > 0 and var arroba && arroba < texto.Length - 1
        && texto.Count(c => c == '@') == 1 && !texto.Any(char.IsWhiteSpace);

    private static string? Recortado(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static (FiltroValido?, string?) Error(string mensaje) => (null, mensaje);
}
