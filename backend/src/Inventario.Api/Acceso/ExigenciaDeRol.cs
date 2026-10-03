namespace Inventario.Api.Acceso;

public static class ExigenciaDeRol
{
    /// <summary>
    /// Declara la operación que ejecuta el endpoint. El servidor verifica la sesión y el rol en cada petición,
    /// antes de leer el cuerpo, aunque la petición se arme a mano (RD-06).
    /// </summary>
    public static TBuilder Requiere<TBuilder>(this TBuilder endpoint, Operacion operacion)
        where TBuilder : IEndpointConventionBuilder
    {
        endpoint.WithMetadata(operacion);
        return operacion.EsPublica
            ? endpoint.AllowAnonymous()
            : endpoint.RequireAuthorization(politica => politica.RequireAuthenticatedUser().RequireRole(operacion.Roles));
    }

    /// <summary>Detiene el arranque si algún endpoint de la API no declaró su operación.</summary>
    public static void VerificarQueTodoEndpointDeclareSuOperacion(this IEndpointRouteBuilder app)
    {
        var sinDeclarar = app.DataSources
            .SelectMany(fuente => fuente.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<Operacion>() is null)
            .Select(endpoint => endpoint.DisplayName ?? endpoint.RoutePattern.RawText)
            .ToList();

        if (sinDeclarar.Count > 0)
            throw new InvalidOperationException(
                "Estos endpoints no declaran su operación con .Requiere(Operaciones.X) (RF-CA-05): " +
                string.Join(", ", sinDeclarar));
    }
}
