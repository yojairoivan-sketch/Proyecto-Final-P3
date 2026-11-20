using Inventario.Api.Acceso;
using Inventario.Core.ColaCorreos;

namespace Inventario.Api.Notificaciones;

/// <summary>RF-NOT-11: el Administrador consulta la cola de correos. La consulta y su filtro viven en <see cref="IConsultaCola"/>.</summary>
public static class EndpointsColaCorreos
{
    public static IEndpointRouteBuilder MapearColaCorreos(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cola-correos", async (string? estado, IConsultaCola consulta, CancellationToken ct) =>
            await consulta.ListarAsync(estado, ct) switch
            {
                { Vista: { } vista } => Results.Ok(vista),
                var fallo => Results.Problem(detail: fallo.Error, statusCode: StatusCodes.Status400BadRequest),
            })
            .Requiere(Operaciones.ConsultarColaCorreos);

        return app;
    }
}
