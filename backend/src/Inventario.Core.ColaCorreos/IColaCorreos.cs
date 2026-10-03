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
        db.Correos.Add(CorreoEnCola.Nuevo(destinatario, asunto, cuerpo, reloj.GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }
}
