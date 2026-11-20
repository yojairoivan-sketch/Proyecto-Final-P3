using Inventario.Api.Acceso;
using Inventario.Core.Notificaciones;

namespace Inventario.Api.Notificaciones;

/// <summary>
/// La bandeja de notificaciones del usuario de la sesión. Endpoints delgados: que cada uno vea y marque solo las suyas
/// lo decide <see cref="IServicioNotificaciones"/> en el servidor (RD-06).
/// </summary>
public static class EndpointsNotificaciones
{
    public sealed record CambioPreferencias(bool? Correo);

    public static IEndpointRouteBuilder MapearNotificaciones(this IEndpointRouteBuilder app)
    {
        var notificaciones = app.MapGroup("/api/notificaciones");

        notificaciones.MapGet("/", async (string? estado, IServicioNotificaciones servicio, CancellationToken ct) =>
            (await servicio.ListarMiasAsync(estado, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarMisNotificaciones);

        notificaciones.MapPost("/{id:long}/leida", async (long id, IServicioNotificaciones servicio, CancellationToken ct) =>
            (await servicio.MarcarLeidaAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.MarcarNotificacionLeida);

        notificaciones.MapGet("/preferencias", async (IServicioNotificaciones servicio, CancellationToken ct) =>
            (await servicio.ConsultarPreferenciasAsync(ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarPreferenciasNotificacion);

        // El cuerpo puede faltar: el servicio responde con su propio mensaje en vez del 400 genérico de formato.
        notificaciones.MapPut("/preferencias", async (CambioPreferencias? cambio, IServicioNotificaciones servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.CambiarPreferenciasAsync(cambio?.Correo, ct);
            return resultado.Exito
                ? Results.Ok(new { mensaje = resultado.Mensaje, correo = resultado.Valor!.Correo })
                : resultado.ComoRespuesta();
        })
            .Requiere(Operaciones.CambiarPreferenciasNotificacion);

        return app;
    }
}
