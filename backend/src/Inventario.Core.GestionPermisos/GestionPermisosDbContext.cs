using Inventario.Core.GestionPermisos.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.GestionPermisos;

/// <summary>Datos propios de Gestión de permisos, en su propio esquema. No lee las tablas de Control de acceso.</summary>
public sealed class GestionPermisosDbContext(DbContextOptions<GestionPermisosDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "gestion_permisos";

    internal DbSet<SolicitudPermiso> Solicitudes => Set<SolicitudPermiso>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<SolicitudPermiso>(solicitud =>
        {
            solicitud.ToTable("solicitudes_permiso");
            solicitud.Property(s => s.SolicitanteNombre).HasMaxLength(100);
            solicitud.Property(s => s.SolicitanteCorreo).HasMaxLength(254);
            solicitud.Property(s => s.Permiso).HasMaxLength(100);
            solicitud.Property(s => s.Justificacion).HasMaxLength(500);
            solicitud.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20);
            solicitud.Property(s => s.ResueltaPorNombre).HasMaxLength(100);
            solicitud.Property(s => s.MotivoRechazo).HasMaxLength(TransicionesSolicitud.LargoMaximoMotivo);
            solicitud.Property(s => s.Version).IsConcurrencyToken();
            solicitud.HasIndex(s => s.Estado);
            solicitud.HasIndex(s => s.SolicitanteId);
        });
    }
}
