using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso.Sesiones;

internal static class RevocacionSesiones
{
    /// <summary>Cierra todas las sesiones abiertas de un usuario: sus credenciales dejan de servir de inmediato.</summary>
    public static Task<int> RevocarSesionesDeAsync(this ControlAccesoDbContext db, int usuarioId, DateTimeOffset ahora, CancellationToken ct) =>
        db.Sesiones
            .Where(s => s.UsuarioId == usuarioId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadaEn, ahora), ct);
}
