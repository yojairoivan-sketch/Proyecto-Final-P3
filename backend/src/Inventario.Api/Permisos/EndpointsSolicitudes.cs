using Inventario.Api.Acceso;
using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos.Solicitudes;

namespace Inventario.Api.Permisos;

/// <summary>
/// Gestión de permisos: pedir un permiso y resolver la solicitud. Endpoints delgados: las reglas y la máquina
/// de estados viven en IServicioSolicitudes; la exigencia de rol de cada uno está en Operaciones.
/// </summary>
public static class EndpointsSolicitudes
{
    public sealed record NuevaSolicitud(string? Permiso, string? Justificacion);

    public sealed record Rechazo(string? Motivo);

    public static IEndpointRouteBuilder MapearSolicitudes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/permisos/solicitables", (IServicioSolicitudes servicio) => Results.Ok(servicio.PermisosSolicitables))
            .Requiere(Operaciones.ConsultarPermisosSolicitables);

        var solicitudes = app.MapGroup("/api/solicitudes");

        solicitudes.MapPost("/", async (NuevaSolicitud nueva, IServicioSolicitudes servicio, CancellationToken ct) =>
            Responder(await servicio.CrearAsync(nueva.Permiso, nueva.Justificacion, ct), StatusCodes.Status201Created))
            .Requiere(Operaciones.CrearSolicitudPermiso);

        solicitudes.MapGet("/mias", async (IServicioSolicitudes servicio, CancellationToken ct) =>
            (await servicio.ListarMiasAsync(ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarMisSolicitudes);

        solicitudes.MapGet("/pendientes", async (IServicioSolicitudes servicio, CancellationToken ct) =>
            Results.Ok(await servicio.ListarPendientesAsync(ct)))
            .Requiere(Operaciones.ConsultarSolicitudesPendientes);

        solicitudes.MapGet("/{id:int}", async (int id, IServicioSolicitudes servicio, CancellationToken ct) =>
            (await servicio.ConsultarAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarSolicitud);

        solicitudes.MapPost("/{id:int}/aprobar", async (int id, IServicioSolicitudes servicio, CancellationToken ct) =>
            Responder(await servicio.AprobarAsync(id, ct)))
            .Requiere(Operaciones.AprobarSolicitud);

        // El cuerpo puede faltar: entonces no hay motivo y el servicio rechaza la operación con su mensaje (RF-GP-04).
        solicitudes.MapPost("/{id:int}/rechazar", async (int id, Rechazo? rechazo, IServicioSolicitudes servicio, CancellationToken ct) =>
            Responder(await servicio.RechazarAsync(id, rechazo?.Motivo, ct)))
            .Requiere(Operaciones.RechazarSolicitud);

        return app;
    }

    /// <summary>Éxito: el mensaje y la solicitud como quedó. Fallo: el ProblemDetails de siempre.</summary>
    private static IResult Responder(Resultado<SolicitudVista> resultado, int estadoExito = StatusCodes.Status200OK) =>
        resultado.Exito
            ? Results.Json(new { mensaje = resultado.Mensaje, solicitud = resultado.Valor }, statusCode: estadoExito)
            : resultado.ComoRespuesta();
}
