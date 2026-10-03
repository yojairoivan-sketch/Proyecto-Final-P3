using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ColaCorreos;

public sealed record ResumenEnvio(int Enviados, int ConError);

/// <summary>
/// Toma los correos pendientes y los entrega (RF-NOT-09). Corre fuera de la operación que creó el correo.
/// </summary>
public sealed class ProcesadorCola(ColaCorreosDbContext db, ITransporteCorreo transporte, TimeProvider reloj)
{
    public async Task<ResumenEnvio> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var enviados = 0;
        var conError = new List<long>();

        while (true)
        {
            // Cada correo se bloquea con FOR UPDATE SKIP LOCKED dentro de su transacción: dos procesadores
            // a la vez nunca toman el mismo, y uno que ya quedó Enviado no vuelve a salir (RF-NOT-12).
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            var excluidos = conError.ToArray();
            var correo = (await db.Correos
                .FromSql($"""
                    SELECT * FROM cola_correos.correos_en_cola
                    WHERE estado = 'Pendiente' AND id <> ALL({excluidos})
                    ORDER BY id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct)).FirstOrDefault();

            if (correo is null)
                break;

            try
            {
                await transporte.EnviarAsync(correo, ct);
                correo.MarcarEnviado(reloj.GetUtcNow());
                enviados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Queda Pendiente con el error anotado; en esta pasada no se vuelve a intentar.
                correo.RegistrarFallo($"{ex.GetType().Name}: {ex.Message}");
                conError.Add(correo.Id);
            }

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }

        return new ResumenEnvio(enviados, conError.Count);
    }
}
