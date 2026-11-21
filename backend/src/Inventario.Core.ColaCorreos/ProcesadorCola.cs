using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ColaCorreos;

/// <param name="Enviados">Entregados en esta pasada.</param>
/// <param name="ConError">Fallaron en esta pasada y se reintentan en la próxima.</param>
/// <param name="Fallidos">Fallaron por última vez en esta pasada: quedaron Fallido.</param>
public sealed record ResumenEnvio(int Enviados, int ConError, int Fallidos = 0);

/// <summary>Cuántas veces se intenta un correo antes de dejarlo Fallido (RF-NOT-10). Variable COLA_MAX_INTENTOS.</summary>
public sealed class OpcionesCola
{
    public const string VariableMaximoIntentos = "COLA_MAX_INTENTOS";
    public const int MaximoIntentosPorDefecto = 5;

    public int MaximoIntentos { get; set; } = MaximoIntentosPorDefecto;

    /// <summary>Un número de 1 a 100; si falta, el de por defecto. Cualquier otra cosa es un error de configuración.</summary>
    public static OpcionesCola DesdeVariable(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new OpcionesCola();
        return int.TryParse(valor.Trim(), out var maximo) && maximo is >= 1 and <= 100
            ? new OpcionesCola { MaximoIntentos = maximo }
            : throw new InvalidOperationException($"{VariableMaximoIntentos} debe ser un número entre 1 y 100.");
    }
}

/// <summary>
/// Toma los correos pendientes y los entrega (RF-NOT-09). Corre fuera de la operación que creó el correo.
/// </summary>
public sealed class ProcesadorCola(ColaCorreosDbContext db, ITransporteCorreo transporte, TimeProvider reloj, OpcionesCola opciones)
{
    public async Task<ResumenEnvio> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var enviados = 0;
        var conError = new List<long>();
        var fallidos = 0;

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

            if (!CorreoEnCola.DestinatarioEntregable(correo.Destinatario))
            {
                // Hallazgo 1: un Pendiente que ya estaba en la cola con una dirección inválida no gasta intentos.
                correo.RegistrarDestinatarioInvalido(reloj.GetUtcNow());
                conError.Add(correo.Id);
                fallidos++;
                await db.SaveChangesAsync(ct);
                await transaccion.CommitAsync(ct);
                db.ChangeTracker.Clear();
                continue;
            }

            try
            {
                await transporte.EnviarAsync(correo, ct);
                correo.MarcarEnviado(reloj.GetUtcNow());
                enviados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Queda Pendiente con el error anotado (en esta pasada no se vuelve a intentar),
                // o Fallido si era su último intento (RF-NOT-10).
                correo.RegistrarFallo($"{ex.GetType().Name}: {ex.Message}", reloj.GetUtcNow(), opciones.MaximoIntentos);
                conError.Add(correo.Id);
                if (correo.Estado == EstadoCorreo.Fallido)
                    fallidos++;
            }

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }

        return new ResumenEnvio(enviados, conError.Count - fallidos, fallidos);
    }
}
