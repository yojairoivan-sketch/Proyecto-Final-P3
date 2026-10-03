using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Errores;

/// <summary>
/// RD-08: toda respuesta de error sale como ProblemDetails con un mensaje comprensible.
/// Nunca incluye trazas de pila, rutas de archivos ni consultas.
/// </summary>
public static class ErroresControlados
{
    public static IServiceCollection AgregarErroresControlados(this IServiceCollection services)
    {
        services.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
            Completar(contexto.ProblemDetails, contexto.Exception));

        // Un BadHttpRequestException (JSON malformado, cuerpo vacío...) es culpa de la petición: 400, no 500.
        services.Configure<ExceptionHandlerOptions>(opciones =>
            opciones.StatusCodeSelector = ex => ex is BadHttpRequestException malo ? malo.StatusCode : 500);

        return services;
    }

    public static WebApplication UsarErroresControlados(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    private static void Completar(ProblemDetails problema, Exception? excepcion)
    {
        var estado = problema.Status ?? 500;
        problema.Type = null;
        problema.Instance = null;
        problema.Extensions.Remove("traceId");
        problema.Extensions.Remove("exception");
        problema.Title = Titulo(estado);

        if (excepcion is not null || estado >= 500)
        {
            // El mensaje de la excepción se queda en el log; al usuario solo le llega un texto fijo.
            problema.Detail = estado >= 500
                ? "Ocurrió un error inesperado. Intenta de nuevo más tarde."
                : "La petición no tiene el formato esperado. Revisa que el cuerpo sea JSON válido.";
        }
        else
        {
            problema.Detail ??= DetallePorDefecto(estado);
        }
    }

    private static string Titulo(int estado) => estado switch
    {
        400 => "Petición inválida",
        401 => "No autenticado",
        403 => "Sin permiso",
        404 => "No encontrado",
        405 => "Método no permitido",
        409 => "Conflicto",
        413 => "Petición demasiado grande",
        415 => "Tipo de contenido no soportado",
        423 => "Cuenta bloqueada",
        _ when estado >= 500 => "Error del servidor",
        _ => "Error",
    };

    private static string DetallePorDefecto(int estado) => estado switch
    {
        400 => "La petición no tiene el formato esperado. Revisa que el cuerpo sea JSON válido.",
        401 => "Necesitas iniciar sesión para usar esta operación.",
        403 => "No tienes permiso para usar esta operación.",
        404 => "El recurso pedido no existe.",
        405 => "Esta ruta no acepta ese método HTTP.",
        415 => "El cuerpo debe enviarse como application/json.",
        _ => "No se pudo completar la petición.",
    };
}
