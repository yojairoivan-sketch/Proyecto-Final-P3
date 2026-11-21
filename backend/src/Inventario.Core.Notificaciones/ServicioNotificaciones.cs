using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Notificaciones;

public sealed record NotificacionVista(long Id, string Titulo, string Mensaje, bool Leida, DateTimeOffset CreadaEn, DateTimeOffset? LeidaEn);

public sealed record BandejaVista(int NoLeidas, IReadOnlyList<NotificacionVista> Notificaciones);

public sealed record PreferenciasVista(bool Correo);

/// <summary>
/// La bandeja del usuario actual. Todo lo que hace es sobre sus propias notificaciones: las de otro no existen para él,
/// sea Estándar o Administrador (RF-NOT-04, 05; RD-06).
/// </summary>
public interface IServicioNotificaciones
{
    /// <summary>RF-NOT-04: sus notificaciones, las más nuevas primero. <paramref name="estado"/>: NoLeidas, Leidas o nada (todas).</summary>
    Task<Resultado<BandejaVista>> ListarMiasAsync(string? estado, CancellationToken ct = default);

    /// <summary>RF-NOT-05: marca como leída una notificación suya.</summary>
    Task<Resultado<NotificacionVista>> MarcarLeidaAsync(long id, CancellationToken ct = default);

    Task<Resultado<PreferenciasVista>> ConsultarPreferenciasAsync(CancellationToken ct = default);

    /// <summary>RF-NOT-07: activa o desactiva el canal de correo. El canal interno no se puede desactivar.</summary>
    Task<Resultado<PreferenciasVista>> CambiarPreferenciasAsync(bool? correo, CancellationToken ct = default);
}

internal sealed class ServicioNotificaciones(NotificacionesDbContext db, IUsuarioActual usuarioActual, TimeProvider reloj)
    : IServicioNotificaciones
{
    public async Task<Resultado<BandejaVista>> ListarMiasAsync(string? estado, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<BandejaVista>();

        var mias = db.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuario.Id);
        var filtradas = estado?.Trim().ToLowerInvariant() switch
        {
            null or "" => mias,
            "noleidas" or "no-leidas" => mias.Where(n => n.LeidaEn == null),
            "leidas" or "leídas" => mias.Where(n => n.LeidaEn != null),
            _ => null,
        };
        if (filtradas is null)
            return Resultado<BandejaVista>.Falla(TipoFallo.Validacion,
                $"El estado «{estado!.Trim()}» no existe. Usa NoLeidas o Leidas, o no pongas estado para verlas todas.");

        var noLeidas = await mias.CountAsync(n => n.LeidaEn == null, ct);
        var lista = await filtradas.OrderByDescending(n => n.CreadaEn).ThenByDescending(n => n.Id).ToListAsync(ct);
        return Resultado<BandejaVista>.Ok(new BandejaVista(noLeidas, lista.Select(Vista).ToList()));
    }

    public async Task<Resultado<NotificacionVista>> MarcarLeidaAsync(long id, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<NotificacionVista>();

        var notificacion = await db.Notificaciones.SingleOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuario.Id, ct);
        if (notificacion is null)
            return Resultado<NotificacionVista>.Falla(TipoFallo.NoEncontrado, "La notificación no existe.");

        notificacion.MarcarLeida(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Resultado<NotificacionVista>.Ok(Vista(notificacion));
    }

    public async Task<Resultado<PreferenciasVista>> ConsultarPreferenciasAsync(CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<PreferenciasVista>();

        var preferencia = await db.Preferencias.AsNoTracking().SingleOrDefaultAsync(p => p.UsuarioId == usuario.Id, ct);
        return Resultado<PreferenciasVista>.Ok(new PreferenciasVista(preferencia?.CorreoActivo ?? true));
    }

    public async Task<Resultado<PreferenciasVista>> CambiarPreferenciasAsync(bool? correo, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<PreferenciasVista>();

        if (correo is null)
            return Resultado<PreferenciasVista>.Falla(TipoFallo.Validacion,
                "Indica si quieres recibir las notificaciones por correo: {\"correo\": true} o {\"correo\": false}.");

        var ahora = reloj.GetUtcNow();
        try
        {
            await GuardarPreferenciaAsync(usuario.Id, correo.Value, ahora, ct);
        }
        catch (DbUpdateException)
        {
            // Hallazgo 4 (Asignación 3): otra petición del mismo usuario creó su fila entre la consulta y este guardado
            // (la clave es el usuario). Ya existe: se vuelve a leer y se cambia, en vez de responder 500 (RD-07, RD-08).
            db.ChangeTracker.Clear();
            await GuardarPreferenciaAsync(usuario.Id, correo.Value, ahora, ct);
        }

        return Resultado<PreferenciasVista>.Ok(new PreferenciasVista(correo.Value), correo.Value
            ? "Recibirás tus notificaciones dentro del sistema y por correo."
            : "Recibirás tus notificaciones solo dentro del sistema. No se te enviarán correos de notificación.");
    }

    /// <summary>Crea la fila de preferencias del usuario o cambia la que ya tiene.</summary>
    private async Task GuardarPreferenciaAsync(int usuarioId, bool correo, DateTimeOffset ahora, CancellationToken ct)
    {
        var preferencia = await db.Preferencias.SingleOrDefaultAsync(p => p.UsuarioId == usuarioId, ct);
        if (preferencia is null)
            db.Preferencias.Add(PreferenciaNotificacion.De(usuarioId, correo, ahora));
        else
            preferencia.Cambiar(correo, ahora);
        await db.SaveChangesAsync(ct);
    }

    private static NotificacionVista Vista(Notificacion n) => new(n.Id, n.Titulo, n.Mensaje, n.Leida, n.CreadaEn, n.LeidaEn);

    private static Resultado<T> SinSesion<T>() =>
        Resultado<T>.Falla(TipoFallo.NoAutenticado, "Necesitas iniciar sesión para usar esta operación.");
}
