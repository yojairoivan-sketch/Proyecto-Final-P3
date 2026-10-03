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

        return app;
    }
}
