using Inventario.Core.ControlAcceso.Usuarios;

namespace Inventario.Api.Acceso;

/// <summary>Administración de usuarios. La exigencia de rol de cada una está en Operaciones.</summary>
public static class EndpointsUsuarios
{
    public static IEndpointRouteBuilder MapearUsuarios(this IEndpointRouteBuilder app)
    {
        var usuarios = app.MapGroup("/api/usuarios");

        usuarios.MapGet("/", async (IServicioUsuarios servicio, CancellationToken ct) =>
            Results.Ok(await servicio.ListarAsync(ct)))
            .Requiere(Operaciones.ListarUsuarios);

        return app;
    }
}
