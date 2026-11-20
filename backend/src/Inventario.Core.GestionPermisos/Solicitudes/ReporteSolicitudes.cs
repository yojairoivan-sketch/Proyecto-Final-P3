using Inventario.Core.ControlAcceso;
using Inventario.Core.Reportes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.GestionPermisos.Solicitudes;

/// <summary>Cuántas solicitudes se crearon en un mes y quedaron en un estado.</summary>
public sealed record FilaSolicitudesPorMes(string Mes, EstadoSolicitud Estado, int Cantidad);

public sealed record TotalPorEstado(EstadoSolicitud Estado, int Cantidad);

public sealed record ReporteSolicitudesVista(
    DateOnly Desde,
    DateOnly Hasta,
    bool SoloLasMias,
    int Total,
    IReadOnlyList<TotalPorEstado> PorEstado,
    IReadOnlyList<FilaSolicitudesPorMes> PorMes);

/// <summary>
/// RF-REP-01: reporte del Core. Solicitudes de permiso creadas en un rango de fechas, agrupadas por mes y por estado actual.
/// Un Estándar solo cuenta las suyas; un Administrador, las de todos (RF-REP-04).
/// </summary>
public interface IReporteSolicitudes
{
    Task<Resultado<ReporteSolicitudesVista>> GenerarAsync(string? desde, string? hasta, CancellationToken ct = default);
}

internal sealed class ReporteSolicitudes(GestionPermisosDbContext db, IUsuarioActual usuarioActual) : IReporteSolicitudes
{
    public async Task<Resultado<ReporteSolicitudesVista>> GenerarAsync(string? desde, string? hasta, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return Resultado<ReporteSolicitudesVista>.Falla(TipoFallo.NoAutenticado, "Necesitas iniciar sesión para usar esta operación.");

        var leido = RangoFechas.Leer(desde, hasta);
        if (!leido.Exito)
            return Resultado<ReporteSolicitudesVista>.Falla(leido.Fallo!.Value, leido.Mensaje);
        var rango = leido.Valor!;

        var visibles = db.Solicitudes.AsNoTracking();
        if (!usuario.EsAdministrador)
            visibles = visibles.Where(s => s.SolicitanteId == usuario.Id);

        // La agrupación y el conteo los hace la base (GROUP BY estado), un mes a la vez: nunca se traen las solicitudes (RF-REP-03).
        var porMes = new List<FilaSolicitudesPorMes>();
        foreach (var periodo in rango.Meses())
        {
            var grupos = await visibles
                .Where(s => s.CreadaEn >= periodo.Inicio && s.CreadaEn < periodo.FinExclusivo)
                .GroupBy(s => s.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync(ct);
            porMes.AddRange(grupos
                .OrderBy(g => g.Estado)
                .Select(g => new FilaSolicitudesPorMes(periodo.Mes, g.Estado, g.Cantidad)));
        }

        var porEstado = porMes
            .GroupBy(f => f.Estado)
            .OrderBy(g => g.Key)
            .Select(g => new TotalPorEstado(g.Key, g.Sum(f => f.Cantidad)))
            .ToList();

        return Resultado<ReporteSolicitudesVista>.Ok(new ReporteSolicitudesVista(
            rango.Desde, rango.Hasta, !usuario.EsAdministrador, porMes.Sum(f => f.Cantidad), porEstado, porMes));
    }
}
