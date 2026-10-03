using Inventario.Core.ControlAcceso.Cuentas;
using Inventario.Core.ControlAcceso.Sesiones;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso;

/// <summary>Datos propios de Control de acceso, en su propio esquema. Ninguna otra pieza lee estas tablas.</summary>
public sealed class ControlAccesoDbContext(DbContextOptions<ControlAccesoDbContext> opciones) : DbContext(opciones)
{
    public const string Esquema = "control_acceso";

    internal DbSet<Rol> Roles => Set<Rol>();
    internal DbSet<Usuario> Usuarios => Set<Usuario>();
    internal DbSet<TokenActivacion> TokensActivacion => Set<TokenActivacion>();
    internal DbSet<Sesion> Sesiones => Set<Sesion>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Esquema);

        modelo.Entity<Rol>(rol =>
        {
            rol.ToTable("roles");
            rol.Property(r => r.Id).ValueGeneratedNever();
            rol.Property(r => r.Nombre).HasMaxLength(30);
            rol.HasIndex(r => r.Nombre).IsUnique();
            rol.HasData(new Rol(Rol.IdAdministrador, Rol.Administrador), new Rol(Rol.IdEstandar, Rol.Estandar));
        });

        modelo.Entity<Usuario>(usuario =>
        {
            usuario.ToTable("usuarios");
            usuario.Property(u => u.Nombre).HasMaxLength(100);
            usuario.Property(u => u.Correo).HasMaxLength(254);
            usuario.HasIndex(u => u.Correo).IsUnique();
            usuario.Property(u => u.HashContrasena).HasMaxLength(200);
            usuario.HasOne(u => u.Rol).WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
            usuario.Ignore(u => u.Estado);
            usuario.Ignore(u => u.PendienteDeActivacion);
        });

        modelo.Entity<TokenActivacion>(token =>
        {
            token.ToTable("tokens_activacion");
            token.Property(t => t.HashCodigo).HasMaxLength(64);
            token.HasIndex(t => t.HashCodigo).IsUnique();
            token.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<Sesion>(sesion =>
        {
            sesion.ToTable("sesiones");
            sesion.Property(s => s.HashToken).HasMaxLength(64);
            sesion.HasIndex(s => s.HashToken).IsUnique();
            sesion.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
