using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Auditoria;

/// <summary>
/// Datos propios de la auditoría, en su propio esquema. Es el único contexto que crea la tabla del historial (en sus
/// migraciones, con el trigger que rechaza UPDATE, DELETE y TRUNCATE); las piezas auditadas solo agregan filas.
/// </summary>
public sealed class AuditoriaDbContext(DbContextOptions<AuditoriaDbContext> opciones) : DbContext(opciones)
{
    internal DbSet<RegistroAuditoria> Historial => Set<RegistroAuditoria>();

    protected override void OnConfiguring(DbContextOptionsBuilder opciones) => opciones.ProtegerHistorialAuditoria();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(HistorialAuditoria.Esquema);
        HistorialAuditoria.Mapear(modelo, propietaria: true);
    }
}
