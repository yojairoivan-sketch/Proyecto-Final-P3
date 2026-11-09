using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Documentos;

/// <summary>Registros de los documentos, en su propio esquema. No lee las tablas de Control de acceso.</summary>
public sealed class DocumentosDbContext(DbContextOptions<DocumentosDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "documentos";

    internal DbSet<Documento> Documentos => Set<Documento>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<Documento>(documento =>
        {
            documento.ToTable("documentos");
            documento.Property(d => d.Nombre).HasMaxLength(ServicioDocumentos.LargoMaximoNombre);
            documento.Property(d => d.Tipo).HasMaxLength(ServicioDocumentos.LargoMaximoTipo);
            documento.Property(d => d.SubidoPorNombre).HasMaxLength(100);
            documento.Property(d => d.Archivo).HasMaxLength(64);
            documento.HasIndex(d => d.Archivo).IsUnique();
            documento.HasIndex(d => d.SubidoPorId);
            documento.Ignore(d => d.Eliminado);
        });
    }
}
