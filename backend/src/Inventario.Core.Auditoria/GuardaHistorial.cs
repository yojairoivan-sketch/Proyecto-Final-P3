using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventario.Core.Auditoria;

/// <summary>
/// RF-AUD-06: antes de cada SaveChanges, revisa que ningún registro del historial esté modificado o marcado para borrar.
/// Si lo hay, no se guarda nada. Se agrega en cada contexto que mapea la tabla (<see cref="HistorialAuditoria.ProtegerHistorialAuditoria"/>).
/// </summary>
public sealed class GuardaHistorial : SaveChangesInterceptor
{
    public const string Mensaje = "El historial de auditoría no se modifica ni se borra (RF-AUD-06).";

    public static GuardaHistorial Instancia { get; } = new();

    private GuardaHistorial()
    {
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData datos, InterceptionResult<int> resultado)
    {
        Revisar(datos.Context);
        return base.SavingChanges(datos, resultado);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData datos, InterceptionResult<int> resultado, CancellationToken ct = default)
    {
        Revisar(datos.Context);
        return base.SavingChangesAsync(datos, resultado, ct);
    }

    private static void Revisar(DbContext? contexto)
    {
        if (contexto is null)
            return;

        if (contexto.ChangeTracker.Entries<RegistroAuditoria>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException(Mensaje);
    }
}
