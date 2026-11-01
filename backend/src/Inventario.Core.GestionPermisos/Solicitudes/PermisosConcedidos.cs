using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.GestionPermisos.Solicitudes;

internal sealed class PermisosConcedidos(GestionPermisosDbContext db) : IPermisosConcedidos
{
    // Mientras la solicitud esté Pendiente, Aprobada sin aplicar o Rechazada, el permiso no está concedido.
    public Task<bool> EstaConcedidoAsync(int usuarioId, string permiso, CancellationToken ct = default) =>
        db.Solicitudes.AnyAsync(s => s.SolicitanteId == usuarioId && s.Permiso == permiso && s.Estado == EstadoSolicitud.Aplicada, ct);
}
