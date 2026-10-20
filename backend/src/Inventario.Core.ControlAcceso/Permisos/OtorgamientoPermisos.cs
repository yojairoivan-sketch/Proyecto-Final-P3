using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso.Permisos;

internal sealed class OtorgamientoPermisos(ControlAccesoDbContext db, TimeProvider reloj) : IOtorgamientoPermisos
{
    public async Task OtorgarAsync(int usuarioId, string permiso, CancellationToken ct = default)
    {
        if (await db.PermisosOtorgados.AnyAsync(p => p.UsuarioId == usuarioId && p.Permiso == permiso, ct))
            return;

        db.PermisosOtorgados.Add(PermisoOtorgado.Otorgar(usuarioId, permiso, reloj.GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }
}
