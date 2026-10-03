using Inventario.Core.ControlAcceso;

namespace Inventario.Api.Acceso;

/// <summary>Traduce el Resultado de los servicios a HTTP. Aquí no hay reglas de negocio (RD-02).</summary>
public static class RespuestasHttp
{
    public static IResult ComoRespuesta(this Resultado resultado, int estadoExito = StatusCodes.Status200OK) =>
        resultado.Exito
            ? Results.Json(new { mensaje = resultado.Mensaje }, statusCode: estadoExito)
            : Problema(resultado);

    public static IResult ComoRespuesta<T>(this Resultado<T> resultado) =>
        resultado.Exito ? Results.Ok(resultado.Valor) : Problema(resultado);

    private static IResult Problema(Resultado resultado) =>
        Results.Problem(detail: resultado.Mensaje, statusCode: resultado.Fallo switch
        {
            TipoFallo.Validacion => StatusCodes.Status400BadRequest,
            TipoFallo.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoFallo.CuentaInactiva or TipoFallo.Prohibido => StatusCodes.Status403Forbidden,
            TipoFallo.NoEncontrado => StatusCodes.Status404NotFound,
            TipoFallo.Conflicto => StatusCodes.Status409Conflict,
            TipoFallo.Bloqueado => StatusCodes.Status423Locked,
            _ => StatusCodes.Status400BadRequest,
        });
}
