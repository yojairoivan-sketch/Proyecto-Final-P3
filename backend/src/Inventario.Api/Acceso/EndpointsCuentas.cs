using Inventario.Core.ControlAcceso.Cuentas;

namespace Inventario.Api.Acceso;

/// <summary>Registro, activación y reenvío del enlace. Endpoints delgados: la lógica está en IServicioCuentas.</summary>
public static class EndpointsCuentas
{
    public sealed record SolicitudRegistro(string? Nombre, string? Correo, string? Contrasena);

    public static IEndpointRouteBuilder MapearCuentas(this IEndpointRouteBuilder app)
    {
        var acceso = app.MapGroup("/api/acceso");

        acceso.MapPost("/registro", async (SolicitudRegistro solicitud, IServicioCuentas cuentas, CancellationToken ct) =>
            (await cuentas.RegistrarAsync(solicitud.Nombre, solicitud.Correo, solicitud.Contrasena, ct))
                .ComoRespuesta(StatusCodes.Status201Created));

        return app;
    }
}
