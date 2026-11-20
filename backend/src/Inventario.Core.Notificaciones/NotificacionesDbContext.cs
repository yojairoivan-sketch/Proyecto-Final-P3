using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Notificaciones;

/// <summary>Datos de Notificaciones, en su propio esquema. No lee las tablas de Control de acceso ni de la cola.</summary>
public sealed class NotificacionesDbContext(DbContextOptions<NotificacionesDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "notificaciones";

    internal DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    internal DbSet<PreferenciaNotificacion> Preferencias => Set<PreferenciaNotificacion>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<Notificacion>(notificacion =>
        {
            notificacion.ToTable("notificaciones");
            notificacion.Property(n => n.Titulo).HasMaxLength(Notificacion.LargoMaximoTitulo);
            notificacion.Property(n => n.Mensaje).HasMaxLength(Notificacion.LargoMaximoMensaje);
            notificacion.HasIndex(n => new { n.UsuarioId, n.LeidaEn });
            notificacion.Ignore(n => n.Leida);
        });

        modelo.Entity<PreferenciaNotificacion>(preferencia =>
        {
            preferencia.ToTable("preferencias");
            preferencia.HasKey(p => p.UsuarioId);
            preferencia.Property(p => p.UsuarioId).ValueGeneratedNever();
        });
    }
}
