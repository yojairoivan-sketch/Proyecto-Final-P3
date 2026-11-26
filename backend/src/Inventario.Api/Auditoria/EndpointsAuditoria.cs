using Inventario.Api.Acceso;
using Inventario.Core.Auditoria;
using Inventario.Core.ControlAcceso;

namespace Inventario.Api.Auditoria;

/// <summary>
/// RF-AUD-07: consulta del historial de auditoría. Es el único endpoint de la pieza y es un GET: no hay ninguna ruta que
/// modifique o borre registros (RF-AUD-06). Quién puede consultar (solo un Administrador) y los filtros los decide la pieza.
/// </summary>
public static class EndpointsAuditoria
{
    public static IEndpointRouteBuilder MapearAuditoria(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auditoria", async (
                string? usuario, string? entidad, string? entidadId, string? desde, string? hasta,
                IUsuarioActual actual, IConsultaAuditoria consulta, CancellationToken ct) =>
            {
                var quien = actual.Usuario is { } u ? new Consultante(u.Id, u.Rol, u.EsAdministrador) : null;
                return await consulta.ConsultarAsync(quien, new FiltroAuditoria(usuario, entidad, entidadId, desde, hasta), ct) switch
                {
                    { Vista: { } vista } => Results.Ok(vista),
                    { Fallo: FalloConsulta.Prohibido } rechazo =>
                        Results.Problem(detail: rechazo.Mensaje, statusCode: StatusCodes.Status403Forbidden),
                    var rechazo => Results.Problem(detail: rechazo.Mensaje, statusCode: StatusCodes.Status400BadRequest),
                };
            })
            .Requiere(Operaciones.ConsultarAuditoria);

        return app;
    }
}
