using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Contrasenas;

namespace Inventario.Api.Acceso;

/// <summary>Recuperación, restablecimiento y cambio de contraseña. La lógica está en IServicioContrasenas.</summary>
public static class EndpointsContrasenas
{
    public sealed record SolicitudRecuperacion(string? Correo);

    public sealed record SolicitudRestablecimiento(string? Codigo, string? ContrasenaNueva);

    public sealed record SolicitudCambio(string? ContrasenaActual, string? ContrasenaNueva);

    public static IEndpointRouteBuilder MapearContrasenas(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/acceso/recuperacion", async (SolicitudRecuperacion solicitud, IServicioContrasenas contrasenas, CancellationToken ct) =>
            (await contrasenas.SolicitarRecuperacionAsync(solicitud.Correo, ct)).ComoRespuesta())
            .Requiere(Operaciones.SolicitarRecuperacion);

        app.MapPost("/api/acceso/restablecer", async (SolicitudRestablecimiento solicitud, IServicioContrasenas contrasenas, CancellationToken ct) =>
            (await contrasenas.RestablecerAsync(solicitud.Codigo, solicitud.ContrasenaNueva, ct)).ComoRespuesta())
            .Requiere(Operaciones.RestablecerContrasena);

        app.MapPut("/api/yo/contrasena", async (SolicitudCambio solicitud, IUsuarioActual actual, IServicioContrasenas contrasenas, CancellationToken ct) =>
            (await contrasenas.CambiarAsync(actual.Usuario!.Id, solicitud.ContrasenaActual, solicitud.ContrasenaNueva, ct)).ComoRespuesta())
            .Requiere(Operaciones.CambiarMiContrasena);

        app.MapPost("/api/usuarios/{id:int}/forzar-restablecimiento", async (int id, IServicioContrasenas contrasenas, CancellationToken ct) =>
            (await contrasenas.ForzarRestablecimientoAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.ForzarRestablecimiento);

        return app;
    }
}
