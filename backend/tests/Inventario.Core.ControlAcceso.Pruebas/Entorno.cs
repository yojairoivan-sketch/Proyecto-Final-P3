using System.Text.RegularExpressions;
using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso.Contrasenas;
using Inventario.Core.ControlAcceso.Cuentas;
using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Sesiones;
using Inventario.Core.ControlAcceso.Usuarios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>
/// Control de acceso aislado (RD-12): base SQLite en memoria, reloj controlado y una cola de correos falsa.
/// No levanta la API, ni PostgreSQL, ni el servidor de correo.
/// </summary>
public sealed class Entorno : IDisposable
{
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<ControlAccesoDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
    }

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    public ColaFalsa Cola { get; } = new();
    public OpcionesControlAcceso Opciones { get; } = new() { UrlPublica = "http://localhost:8080" };

    internal ControlAccesoDbContext NuevoContexto()
    {
        var contexto = new ControlAccesoDbContext(new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlite(conexion)
            .ReplaceService<IModelCustomizer, FechasComoNumero>()
            .Options);
        contextos.Add(contexto);
        return contexto;
    }

    // Cada llamada usa un contexto nuevo, como cada petición en la API.
    public IServicioCuentas Cuentas() => new ServicioCuentas(NuevoContexto(), Cola, Reloj, Opciones);
    public IServicioSesiones Sesiones() => new ServicioSesiones(NuevoContexto(), Reloj, Opciones);
    public IServicioContrasenas Contrasenas() => new ServicioContrasenas(NuevoContexto(), Cola, Reloj, Opciones);
    public IServicioUsuarios Usuarios() => new ServicioUsuarios(NuevoContexto(), Reloj);

    internal Usuario BuscarUsuario(string correo) =>
        NuevoContexto().Usuarios.Include(u => u.Rol).Single(u => u.Correo == correo);

    /// <summary>Registra una cuenta Estándar y la activa con el enlace del correo, como lo haría el usuario.</summary>
    public async Task<int> CrearUsuarioActivoAsync(string correo, string contrasena = "clave1234")
    {
        var registro = await Cuentas().RegistrarAsync("Prueba", correo, contrasena);
        Assert.True(registro.Exito, registro.Mensaje);
        var activacion = await Cuentas().ActivarAsync(Cola.UltimoValor(correo, "token"));
        Assert.True(activacion.Exito, activacion.Mensaje);
        return BuscarUsuario(correo).Id;
    }

    public async Task<int> CrearAdministradorAsync(string correo, string contrasena = "clave1234")
    {
        var db = NuevoContexto();
        var administrador = Usuario.CrearAdministrador("Administrador", correo, Reloj.GetUtcNow());
        administrador.CambiarHash(Secretos.HashDeContrasena(contrasena));
        db.Usuarios.Add(administrador);
        await db.SaveChangesAsync();
        return administrador.Id;
    }

    public async Task<string> IniciarSesionAsync(string correo, string contrasena = "clave1234")
    {
        var resultado = await Sesiones().IniciarAsync(correo, contrasena);
        Assert.True(resultado.Exito, resultado.Mensaje);
        return resultado.Valor!.Token;
    }

    public void Dispose()
    {
        foreach (var contexto in contextos)
            contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>SQLite no compara DateTimeOffset; aquí se guardan como número. En PostgreSQL son timestamptz.</summary>
    private sealed class FechasComoNumero(ModelCustomizerDependencies dependencias) : RelationalModelCustomizer(dependencias)
    {
        public override void Customize(ModelBuilder modelo, DbContext contexto)
        {
            base.Customize(modelo, contexto);
            var fechas = modelo.Model.GetEntityTypes()
                .SelectMany(entidad => entidad.GetProperties())
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));
            foreach (var propiedad in fechas)
                propiedad.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}

/// <summary>
/// Doble de la interfaz requerida IColaCorreos: guarda lo que Control de acceso encola para leer los enlaces.
/// </summary>
public sealed class ColaFalsa : IColaCorreos
{
    public List<(string Destinatario, string Asunto, string Cuerpo)> Correos { get; } = [];

    public Task EncolarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
    {
        Correos.Add((destinatario, asunto, cuerpo));
        return Task.CompletedTask;
    }

    /// <summary>Valor de <paramref name="parametro"/> en el enlace del último correo enviado a ese destinatario.</summary>
    public string UltimoValor(string destinatario, string parametro)
    {
        var cuerpo = Correos.Last(c => c.Destinatario == destinatario).Cuerpo;
        return Uri.UnescapeDataString(Regex.Match(cuerpo, $@"[?&]{parametro}=([^\s&]+)").Groups[1].Value);
    }
}
