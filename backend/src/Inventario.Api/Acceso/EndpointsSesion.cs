using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Sesiones;

namespace Inventario.Api.Acceso;

/// <summary>Inicio y cierre de sesión, y consulta del usuario autenticado.</summary>
public static class EndpointsSesion
{
    public sealed record SolicitudInicioSesion(string? Correo, string? Contrasena);

    public static IEndpointRouteBuilder MapearSesion(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/acceso/sesion", async (SolicitudInicioSesion solicitud, IServicioSesiones sesiones, CancellationToken ct) =>
            (await sesiones.IniciarAsync(solicitud.Correo, solicitud.Contrasena, ct)).ComoRespuesta());

        // RF-CA-07: sin sesión válida responde 401.
        app.MapGet("/api/yo", (IUsuarioActual actual) =>
        {
            var yo = actual.Usuario!;
            return Results.Ok(new { yo.Id, yo.Nombre, yo.Correo, yo.Rol });
        })
            .RequireAuthorization();

        return app;
    }
}
