namespace Inventario.Core.ColaCorreos;

/// <summary>
/// Interfaz provista de la cola: lo único que las demás piezas usan para mandar un correo.
/// </summary>
public interface IColaCorreos
{
    /// <summary>
    /// Deja el correo en la cola como Pendiente. Nunca contacta el servidor SMTP, así la operación
    /// que lo pide termina bien aunque el servidor de correo no responda (RF-NOT-08).
    /// </summary>
    Task EncolarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default);
}

internal sealed class ColaCorreos(ColaCorreosDbContext db, TimeProvider reloj) : IColaCorreos
{
    public async Task EncolarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var correo = CorreoEnCola.Nuevo(destinatario, asunto, cuerpo, ahora);

        // Hallazgo 1 (Asignación 3): una dirección que el transporte no puede interpretar nunca se va a entregar.
        // No se reintenta: queda Fallido desde el principio, con el motivo, para que el Administrador lo vea (RF-NOT-10, 11).
        if (!CorreoEnCola.DestinatarioEntregable(destinatario))
            correo.RegistrarDestinatarioInvalido(ahora);

        db.Correos.Add(correo);
        await db.SaveChangesAsync(ct);
    }
}
