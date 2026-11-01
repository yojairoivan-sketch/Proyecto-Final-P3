using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio;

/// <summary>Rechazos que comparten los servicios del negocio.</summary>
internal static class Fallos
{
    public static Resultado<T> SinSesion<T>() =>
        Resultado<T>.Falla(TipoFallo.NoAutenticado, "Necesitas iniciar sesión para usar esta operación.");

    public static Resultado<T> NoExiste<T>(string que) => Resultado<T>.Falla(TipoFallo.NoEncontrado, $"{que} no existe.");

    public static Resultado<T> Como<T>(Resultado fallido) => Resultado<T>.Falla(fallido.Fallo!.Value, fallido.Mensaje);

    /// <summary>Guarda los cambios. Si otro usuario cambió lo mismo al mismo tiempo (Version), devuelve el rechazo y no guarda nada.</summary>
    public static async Task<Resultado<T>?> GuardarAsync<T>(DbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Resultado<T>.Falla(TipoFallo.Conflicto,
                "Otro usuario hizo un cambio sobre lo mismo al mismo tiempo. Vuelve a consultarlo e inténtalo de nuevo.");
        }
    }
}
