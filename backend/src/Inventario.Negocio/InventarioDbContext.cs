using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio;

/// <summary>Datos del módulo de negocio, en su propio esquema. No lee las tablas del Core.</summary>
public sealed class InventarioDbContext(DbContextOptions<InventarioDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "inventario";

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<OrdenDeCompra> OrdenesDeCompra => Set<OrdenDeCompra>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<Proveedor>(proveedor =>
        {
            proveedor.ToTable("proveedores");
            proveedor.Property(p => p.Nombre).HasMaxLength(150);
            proveedor.Property(p => p.Telefono).HasMaxLength(30);
            proveedor.Property(p => p.Correo).HasMaxLength(254);
        });

        modelo.Entity<Producto>(producto =>
        {
            producto.ToTable("productos");
            producto.Property(p => p.Nombre).HasMaxLength(150);
            producto.Property(p => p.Sku).HasMaxLength(50);
            producto.HasIndex(p => p.Sku).IsUnique();
            producto.Property(p => p.UnidadMedida).HasMaxLength(20);
            producto.Property(p => p.PuntoReorden).HasPrecision(12, 2);
            producto.Property(p => p.Existencia).HasPrecision(12, 2);
            producto.Ignore(p => p.StockBajo);
        });

        modelo.Entity<OrdenDeCompra>(orden =>
        {
            orden.ToTable("ordenes_de_compra");
            orden.HasOne(o => o.Proveedor).WithMany().HasForeignKey(o => o.ProveedorId).OnDelete(DeleteBehavior.Restrict);
            orden.HasMany(o => o.Lineas).WithOne(l => l.OrdenDeCompra).HasForeignKey(l => l.OrdenDeCompraId).OnDelete(DeleteBehavior.Cascade);
            orden.Navigation(o => o.Lineas).HasField("lineas");
            orden.Ignore(o => o.Total);
        });

        modelo.Entity<LineaOrdenCompra>(linea =>
        {
            linea.ToTable("lineas_orden_compra");
            linea.HasOne(l => l.Producto).WithMany().HasForeignKey(l => l.ProductoId).OnDelete(DeleteBehavior.Restrict);
            linea.Property(l => l.Cantidad).HasPrecision(12, 2);
            linea.Property(l => l.CostoUnitario).HasPrecision(12, 2);
            linea.Ignore(l => l.Subtotal);
        });
    }
}
