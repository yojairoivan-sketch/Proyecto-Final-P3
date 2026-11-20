using Inventario.Api.Acceso;
using Inventario.Core.GestionPermisos.Solicitudes;
using Inventario.Negocio.Reportes;

namespace Inventario.Api.Reportes;

/// <summary>
/// Los dos reportes con agregación (RF-REP-01 a 05). Endpoints delgados: el rango de fechas, el rol y la agregación
/// los resuelve cada reporte en su pieza.
/// </summary>
public static class EndpointsReportes
{
    public static IEndpointRouteBuilder MapearReportes(this IEndpointRouteBuilder app)
    {
        var reportes = app.MapGroup("/api/reportes");

        reportes.MapGet("/solicitudes", async (string? desde, string? hasta, IReporteSolicitudes reporte, CancellationToken ct) =>
            (await reporte.GenerarAsync(desde, hasta, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarReporteSolicitudes);

        reportes.MapGet("/compras-por-proveedor", async (string? desde, string? hasta, IReporteCompras reporte, CancellationToken ct) =>
            (await reporte.GenerarAsync(desde, hasta, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarReporteCompras);

        return app;
    }
}
