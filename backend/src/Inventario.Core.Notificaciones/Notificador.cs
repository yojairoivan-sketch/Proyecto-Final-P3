using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Notificaciones;

/// <summary>
/// Interfaz provista a las demás piezas y al negocio: «avísale a este usuario» o «avísales a los Administradores».
/// Cada aviso queda en la bandeja interna y, si el destinatario no desactivó el correo, se encola un correo (RF-NOT-06, 07).
/// Nunca contacta el servidor SMTP: el correo sale después por la cola (RF-NOT-08).
/// </summary>
public interface INotificador
{
    /// <summary>Notifica a un usuario. Si el usuario no existe, no hace nada.</summary>
    Task NotificarAsync(int usuarioId, string titulo, string mensaje, CancellationToken ct = default);

    /// <summary>Notifica a cada Administrador activo, y a nadie más (RF-NOT-01).</summary>
    Task NotificarAdministradoresAsync(string titulo, string mensaje, CancellationToken ct = default);
}

internal sealed class Notificador(
    NotificacionesDbContext db,
    IDirectorioUsuarios directorio,
    IColaCorreos cola,
    TimeProvider reloj) : INotificador
{
    public async Task NotificarAsync(int usuarioId, string titulo, string mensaje, CancellationToken ct = default)
    {
        if (await directorio.BuscarAsync(usuarioId, ct) is { } usuario)
            await EntregarAsync([usuario], titulo, mensaje, ct);
    }

    public async Task NotificarAdministradoresAsync(string titulo, string mensaje, CancellationToken ct = default) =>
        await EntregarAsync(await directorio.AdministradoresActivosAsync(ct), titulo, mensaje, ct);

    private async Task EntregarAsync(IReadOnlyList<UsuarioContacto> destinatarios, string titulo, string mensaje, CancellationToken ct)
    {
        if (destinatarios.Count == 0)
            return;

        var ahora = reloj.GetUtcNow();
        foreach (var destinatario in destinatarios)
            db.Notificaciones.Add(Notificacion.Nueva(destinatario.Id, titulo, mensaje, ahora));
        await db.SaveChangesAsync(ct);

        var ids = destinatarios.Select(d => d.Id).ToList();
        var sinCorreo = await db.Preferencias.AsNoTracking()
            .Where(p => ids.Contains(p.UsuarioId) && !p.CorreoActivo)
            .Select(p => p.UsuarioId)
            .ToListAsync(ct);

        foreach (var destinatario in destinatarios.Where(d => !sinCorreo.Contains(d.Id)))
            await cola.EncolarAsync(destinatario.Correo, titulo, Cuerpo(destinatario, mensaje), ct);
    }

    private static string Cuerpo(UsuarioContacto destinatario, string mensaje) =>
        $"""
        Hola, {destinatario.Nombre}:

        {mensaje}

        También lo tienes en tus notificaciones dentro del sistema. Si no quieres recibir estos avisos por correo,
        desactívalo en tus preferencias de notificaciones.
        """;
}
