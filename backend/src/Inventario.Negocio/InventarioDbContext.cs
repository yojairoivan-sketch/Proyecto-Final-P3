using Inventario.Negocio.Existencias;
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
    public DbSet<MovimientoExistencia> Movimientos => Set<MovimientoExistencia>();

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
            producto.Property(p => p.Version).IsConcurrencyToken();
            producto.Ignore(p => p.StockBajo);
            producto.HasMany(p => p.Movimientos).WithOne(m => m.Producto).HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
            producto.Navigation(p => p.Movimientos).HasField("movimientos");
        });

        modelo.Entity<MovimientoExistencia>(movimiento =>
        {
            movimiento.ToTable("movimientos_existencia");
            movimiento.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
            movimiento.Property(m => m.Cantidad).HasPrecision(12, 2);
            movimiento.Property(m => m.ExistenciaResultante).HasPrecision(12, 2);
            movimiento.Property(m => m.Motivo).HasMaxLength(MovimientoExistencia.LargoMaximoMotivo);
            movimiento.Property(m => m.UsuarioNombre).HasMaxLength(100);
            movimiento.HasOne(m => m.OrdenDeCompra).WithMany().HasForeignKey(m => m.OrdenDeCompraId).OnDelete(DeleteBehavior.Restrict);
            movimiento.HasIndex(m => new { m.ProductoId, m.RegistradoEn });
        });

        modelo.Entity<OrdenDeCompra>(orden =>
        {
            orden.ToTable("ordenes_de_compra");
            orden.Property(o => o.Estado).HasConversion<string>().HasMaxLength(20);
            orden.Property(o => o.Version).IsConcurrencyToken();
            orden.HasIndex(o => o.Estado);
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
