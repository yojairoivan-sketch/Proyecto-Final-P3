using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ColaCorreos;

/// <summary>Lo que se muestra de un correo de la cola. No incluye el cuerpo: puede llevar enlaces de activación o códigos.</summary>
public sealed record CorreoVista(
    long Id,
    string Destinatario,
    string Asunto,
    EstadoCorreo Estado,
    int Intentos,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoIntentoEn,
    DateTimeOffset? EnviadoEn,
    string? UltimoError);

/// <summary>Cuántos correos hay en cada estado, y los correos pedidos.</summary>
public sealed record ConsultaColaVista(int Pendientes, int Enviados, int Fallidos, IReadOnlyList<CorreoVista> Correos);

/// <summary>La consulta o, si el filtro no es válido, el mensaje que dice por qué.</summary>
public sealed record ResultadoConsultaCola(ConsultaColaVista? Vista, string? Error);

/// <summary>
/// RF-NOT-11: qué pasó con cada correo. Quién puede consultarla (solo un Administrador) lo declara el host,
/// igual que las demás operaciones de administración.
/// </summary>
public interface IConsultaCola
{
    public const int Limite = 200;

    /// <summary>Los correos más nuevos primero (hasta <see cref="Limite"/>). <paramref name="estado"/>: Pendiente, Enviado, Fallido o nada.</summary>
    Task<ResultadoConsultaCola> ListarAsync(string? estado, CancellationToken ct = default);
}

internal sealed class ConsultaCola(ColaCorreosDbContext db) : IConsultaCola
{
    public async Task<ResultadoConsultaCola> ListarAsync(string? estado, CancellationToken ct = default)
    {
        var correos = db.Correos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (Leer(estado) is not { } filtro)
                return new ResultadoConsultaCola(null,
                    $"El estado «{estado.Trim()}» no existe. Usa {string.Join(", ", Enum.GetNames<EstadoCorreo>())}, " +
                    "o no pongas estado para verlos todos.");
            correos = correos.Where(c => c.Estado == filtro);
        }

        // El resumen se cuenta en la base, agrupado por estado.
        var porEstado = await db.Correos.AsNoTracking()
            .GroupBy(c => c.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);
        int Contar(EstadoCorreo e) => porEstado.SingleOrDefault(x => x.Estado == e)?.Cantidad ?? 0;

        var lista = await correos
            .OrderByDescending(c => c.Id)
            .Take(IConsultaCola.Limite)
            .Select(c => new CorreoVista(c.Id, c.Destinatario, c.Asunto, c.Estado, c.Intentos, c.CreadoEn,
                c.UltimoIntentoEn, c.EnviadoEn, c.UltimoError))
            .ToListAsync(ct);

        return new ResultadoConsultaCola(
            new ConsultaColaVista(Contar(EstadoCorreo.Pendiente), Contar(EstadoCorreo.Enviado), Contar(EstadoCorreo.Fallido), lista),
            null);
    }

    /// <summary>El nombre de un estado, sin importar mayúsculas. Un número no cuenta como estado.</summary>
    private static EstadoCorreo? Leer(string texto) =>
        texto.Trim() is { Length: > 0 } limpio && limpio.All(char.IsLetter)
            && Enum.TryParse<EstadoCorreo>(limpio, ignoreCase: true, out var estado)
            ? estado
            : null;
}
