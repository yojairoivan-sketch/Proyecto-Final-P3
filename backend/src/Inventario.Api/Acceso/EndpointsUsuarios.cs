using Inventario.Core.ControlAcceso.Usuarios;

namespace Inventario.Api.Acceso;

/// <summary>Administración de usuarios. La exigencia de rol de cada una está en Operaciones.</summary>
public static class EndpointsUsuarios
{
    public sealed record SolicitudCambioRol(string? Rol);

    public static IEndpointRouteBuilder MapearUsuarios(this IEndpointRouteBuilder app)
    {
        var usuarios = app.MapGroup("/api/usuarios");

        usuarios.MapGet("/", async (IServicioUsuarios servicio, CancellationToken ct) =>
            Results.Ok(await servicio.ListarAsync(ct)))
            .Requiere(Operaciones.ListarUsuarios);

        usuarios.MapPut("/{id:int}/rol", async (int id, SolicitudCambioRol solicitud, IServicioUsuarios servicio, CancellationToken ct) =>
            (await servicio.CambiarRolAsync(id, solicitud.Rol, ct)).ComoRespuesta())
            .Requiere(Operaciones.CambiarRol);

        return app;
    }
}
