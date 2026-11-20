using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Core.ColaCorreos.Pruebas;

/// <summary>Criterios C-01, C-03, C-04 y C-05 de docs/criterios-notificaciones-reportes.md.</summary>
public sealed class ColaCorreosPruebas
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    // ---- Reintentos con límite (RF-NOT-10) ----

    [Fact]
    [Trait("Requisito", "RF-NOT-10")]
    public void AntesDelMaximoDeIntentosUnCorreoQueFallaSiguePendienteConSuError()
    {
        // Preparar
        var correo = CorreoEnCola.Nuevo("ana@ejemplo.com", "Asunto", "Cuerpo", Inicio);

        // Actuar
        correo.RegistrarFallo("SocketException: Connection refused", Inicio.AddSeconds(5), maximoIntentos: 3);

        // Verificar
        Assert.Equal((EstadoCorreo.Pendiente, 1), (correo.Estado, correo.Intentos));
        Assert.Equal("SocketException: Connection refused", correo.UltimoError);
        Assert.Equal(Inicio.AddSeconds(5), correo.UltimoIntentoEn);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-10")]
    public void AlLlegarAlMaximoDeIntentosElCorreoQuedaFallidoConElUltimoError()
    {
        // Preparar
        var correo = CorreoEnCola.Nuevo("ana@ejemplo.com", "Asunto", "Cuerpo", Inicio);
        correo.RegistrarFallo("error 1", Inicio.AddSeconds(5), maximoIntentos: 3);
        correo.RegistrarFallo("error 2", Inicio.AddSeconds(10), maximoIntentos: 3);

        // Actuar
        correo.RegistrarFallo("error 3", Inicio.AddSeconds(15), maximoIntentos: 3);

        // Verificar
        Assert.Equal((EstadoCorreo.Fallido, 3, "error 3"), (correo.Estado, correo.Intentos, correo.UltimoError));
        Assert.Null(correo.EnviadoEn);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-10")]
    public void UnCorreoQueFalloYDespuesSaleQuedaEnviadoConTodosSusIntentos()
    {
        // Preparar
        var correo = CorreoEnCola.Nuevo("ana@ejemplo.com", "Asunto", "Cuerpo", Inicio);
        correo.RegistrarFallo("error 1", Inicio.AddSeconds(5), maximoIntentos: 5);
        correo.RegistrarFallo("error 2", Inicio.AddSeconds(10), maximoIntentos: 5);

        // Actuar
        correo.MarcarEnviado(Inicio.AddSeconds(15));

        // Verificar
        Assert.Equal((EstadoCorreo.Enviado, 3), (correo.Estado, correo.Intentos));
        Assert.Equal(Inicio.AddSeconds(15), correo.EnviadoEn);
        Assert.Null(correo.UltimoError);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-10")]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("3", 3)]
    [InlineData(" 100 ", 100)]
    public void ElMaximoDeIntentosSaleDeLaVariableOEsCincoSiFalta(string? valor, int esperado)
    {
        // Preparar
        var variable = valor;

        // Actuar
        var opciones = OpcionesCola.DesdeVariable(variable);

        // Verificar
        Assert.Equal(esperado, opciones.MaximoIntentos);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-10")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("cinco")]
    public void UnMaximoDeIntentosFueraDeRangoEsUnErrorDeConfiguracion(string valor)
    {
        // Preparar
        var variable = valor;

        // Actuar
        var error = Record.Exception(() => OpcionesCola.DesdeVariable(variable));

        // Verificar
        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains(OpcionesCola.VariableMaximoIntentos, error.Message);
    }

    // ---- Encolar y consultar (RF-NOT-08, 11) ----

    [Fact]
    [Trait("Requisito", "RF-NOT-08")]
    public async Task EncolarSoloGuardaElCorreoPendienteSinIntentos()
    {
        // Preparar
        using var cola = new ColaEnMemoria();

        // Actuar
        await cola.Encolar().EncolarAsync("ana@ejemplo.com", "Asunto", "Cuerpo");

        // Verificar
        var correo = Assert.Single(cola.Leer());
        Assert.Equal((EstadoCorreo.Pendiente, 0, (DateTimeOffset?)null), (correo.Estado, correo.Intentos, correo.UltimoIntentoEn));
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-11")]
    public async Task LaConsultaMuestraEstadoIntentosFechasYUltimoErrorDeCadaCorreo()
    {
        // Preparar
        using var cola = new ColaEnMemoria();
        await cola.GuardarAsync(Pendiente("p@ejemplo.com"), Enviado("e@ejemplo.com"), Fallido("f@ejemplo.com", intentos: 5));

        // Actuar
        var resultado = await cola.Consulta().ListarAsync(estado: null);

        // Verificar
        var vista = resultado.Vista!;
        Assert.Equal((1, 1, 1), (vista.Pendientes, vista.Enviados, vista.Fallidos));
        var fallido = Assert.Single(vista.Correos, c => c.Destinatario == "f@ejemplo.com");
        Assert.Equal((EstadoCorreo.Fallido, 5, "SocketException: Connection refused"), (fallido.Estado, fallido.Intentos, fallido.UltimoError));
        Assert.NotNull(fallido.UltimoIntentoEn);
        Assert.NotNull(Assert.Single(vista.Correos, c => c.Destinatario == "e@ejemplo.com").EnviadoEn);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-11")]
    [InlineData("Fallido", "f@ejemplo.com")]
    [InlineData("pendiente", "p@ejemplo.com")]
    public async Task ElFiltroPorEstadoDevuelveSoloLosDeEseEstado(string estado, string esperado)
    {
        // Preparar
        using var cola = new ColaEnMemoria();
        await cola.GuardarAsync(Pendiente("p@ejemplo.com"), Enviado("e@ejemplo.com"), Fallido("f@ejemplo.com", intentos: 5));

        // Actuar
        var resultado = await cola.Consulta().ListarAsync(estado);

        // Verificar
        Assert.Equal(esperado, Assert.Single(resultado.Vista!.Correos).Destinatario);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-11")]
    [InlineData("Borrado")]
    [InlineData("2")]
    public async Task UnEstadoQueNoExisteDevuelveUnMensajeYNingunCorreo(string estado)
    {
        // Preparar
        using var cola = new ColaEnMemoria();
        await cola.GuardarAsync(Pendiente("p@ejemplo.com"));

        // Actuar
        var resultado = await cola.Consulta().ListarAsync(estado);

        // Verificar
        Assert.Null(resultado.Vista);
        Assert.Contains("Pendiente, Enviado, Fallido", resultado.Error);
    }

    private static CorreoEnCola Pendiente(string destinatario) => CorreoEnCola.Nuevo(destinatario, "Asunto", "Cuerpo", Inicio);

    private static CorreoEnCola Enviado(string destinatario)
    {
        var correo = Pendiente(destinatario);
        correo.MarcarEnviado(Inicio.AddSeconds(5));
        return correo;
    }

    private static CorreoEnCola Fallido(string destinatario, int intentos)
    {
        var correo = Pendiente(destinatario);
        for (var i = 1; i <= intentos; i++)
            correo.RegistrarFallo("SocketException: Connection refused", Inicio.AddSeconds(5 * i), intentos);
        return correo;
    }

    /// <summary>La cola en SQLite en memoria, con un contexto nuevo por operación. Cada prueba arma la suya.</summary>
    private sealed class ColaEnMemoria : IDisposable
    {
        private readonly SqliteConnection conexion = new("DataSource=:memory:");
        private readonly List<ColaCorreosDbContext> contextos = [];

        public ColaEnMemoria()
        {
            conexion.Open();
            Contexto().Database.EnsureCreated();
        }

        public IColaCorreos Encolar() => new ColaCorreos(Contexto(), new FakeTimeProvider(Inicio));

        public IConsultaCola Consulta() => new ConsultaCola(Contexto());

        public async Task GuardarAsync(params CorreoEnCola[] correos)
        {
            var db = Contexto();
            db.Correos.AddRange(correos);
            await db.SaveChangesAsync();
        }

        public List<CorreoEnCola> Leer() => Contexto().Correos.AsNoTracking().ToList();

        public void Dispose()
        {
            foreach (var contexto in contextos)
                contexto.Dispose();
            conexion.Dispose();
        }

        private ColaCorreosDbContext Contexto()
        {
            var contexto = new ColaCorreosDbContext(new DbContextOptionsBuilder<ColaCorreosDbContext>()
                .UseSqlite(conexion)
                .ReplaceService<IModelCustomizer, FechasComoNumero>()
                .Options);
            contextos.Add(contexto);
            return contexto;
        }
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
