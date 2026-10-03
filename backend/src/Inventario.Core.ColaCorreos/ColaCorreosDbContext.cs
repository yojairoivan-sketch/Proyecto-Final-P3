using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ColaCorreos;

/// <summary>Datos propios de la cola, en su propio esquema. Ninguna otra pieza lee estas tablas.</summary>
public sealed class ColaCorreosDbContext(DbContextOptions<ColaCorreosDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "cola_correos";

    public DbSet<CorreoEnCola> Correos => Set<CorreoEnCola>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<CorreoEnCola>(correo =>
        {
            correo.ToTable("correos_en_cola");
            correo.Property(c => c.Destinatario).HasMaxLength(254);
            correo.Property(c => c.Asunto).HasMaxLength(200);
            correo.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
            correo.Property(c => c.UltimoError).HasMaxLength(1000);
            correo.HasIndex(c => c.Estado);
        });
    }
}
