using System.Net;
using System.Text;
using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Cotizaciones;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// El cliente de ExchangeRate-API con las respuestas HTTP simuladas: no sale a internet ni necesita una clave real.
/// Comprueba que la clave sale de las opciones (RD-10) y que sin clave, con una clave mala o con la API caída
/// devuelve un fallo controlado y nunca una excepción (RD-07, RD-08).
/// </summary>
public sealed class TasasExchangeRateApiPruebas
{
    private const string Exito = """
        {"result":"success","time_last_update_unix":1762646401,"base_code":"DOP","target_code":"USD","conversion_rate":0.01563}
        """;

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task PideLaTasaConLaClaveDeLasOpcionesYLeeLaRespuesta()
    {
        // Preparar
        var api = new ApiFalsa(HttpStatusCode.OK, Exito);
        var tasas = Cliente(api, clave: "clave-de-prueba");

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "USD");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(0.01563m, resultado.Valor!.Valor);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1762646401), resultado.Valor.ActualizadaEn);
        Assert.Equal("https://api.prueba/v6/clave-de-prueba/pair/DOP/USD", Assert.Single(api.Pedidas).ToString());
    }

    [Theory]
    [Trait("Requisito", "RD-10")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SinClaveSeRechazaSinLlamarALaApi(string? clave)
    {
        // Preparar
        var api = new ApiFalsa(HttpStatusCode.OK, Exito);
        var tasas = Cliente(api, clave);

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoDisponible, resultado.Fallo);
        Assert.Contains("TASA_CAMBIO_API_CLAVE", resultado.Mensaje);
        Assert.Empty(api.Pedidas);
    }

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task UnaClaveInvalidaSeRechazaConUnMensajeQueNoMuestraLaClave()
    {
        // Preparar: así responde ExchangeRate-API a una clave que no existe, como la de .env.example.
        var api = new ApiFalsa(HttpStatusCode.Forbidden, """{"result":"error","error-type":"invalid-key"}""");
        var tasas = Cliente(api, clave: "clave-de-ejemplo");

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoDisponible, resultado.Fallo);
        Assert.Contains("rechazó la clave", resultado.Mensaje);
        Assert.DoesNotContain("clave-de-ejemplo", resultado.Mensaje);
    }

    [Theory]
    [Trait("Requisito", "RD-08")]
    [InlineData("""{"result":"error","error-type":"quota-reached"}""", TipoFallo.NoDisponible)]
    [InlineData("""{"result":"error","error-type":"inactive-account"}""", TipoFallo.NoDisponible)]
    [InlineData("""{"result":"error","error-type":"unsupported-code"}""", TipoFallo.Validacion)]
    [InlineData("""{"result":"error","error-type":"algo-nuevo"}""", TipoFallo.NoDisponible)]
    [InlineData("<html>502 Bad Gateway</html>", TipoFallo.NoDisponible)]
    [InlineData("", TipoFallo.NoDisponible)]
    [InlineData("""{"result":"success","conversion_rate":0}""", TipoFallo.NoDisponible)]
    public async Task UnaRespuestaDeErrorOInesperadaSeRechazaSinExcepcion(string cuerpo, TipoFallo esperado)
    {
        // Preparar
        var tasas = Cliente(new ApiFalsa(HttpStatusCode.OK, cuerpo), clave: "clave-de-prueba");

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "XYZ");

        // Verificar
        Assert.Equal(esperado, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task SiLaApiNoRespondeSeRechazaSinExcepcion()
    {
        // Preparar: el servidor no existe (falla la conexión).
        var tasas = Cliente(new ApiFalsa(new HttpRequestException("No such host is known.")), clave: "clave-de-prueba");

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoDisponible, resultado.Fallo);
        Assert.Contains("no respondió", resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task SiLaApiTardaDemasiadoSeRechazaSinExcepcion()
    {
        // Preparar: la API nunca contesta; el cliente espera 50 ms.
        var tasas = Cliente(new ApiFalsa(tardar: true), clave: "clave-de-prueba", espera: TimeSpan.FromMilliseconds(50));

        // Actuar
        var resultado = await tasas.ObtenerAsync("DOP", "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoDisponible, resultado.Fallo);
    }

    private static ITasasDeCambio Cliente(ApiFalsa api, string? clave, TimeSpan? espera = null) =>
        new TasasExchangeRateApi(new HttpClient(api),
            new OpcionesTasasDeCambio { Clave = clave, UrlBase = "https://api.prueba/v6/", Espera = espera ?? TimeSpan.FromSeconds(5) },
            NullLogger<TasasExchangeRateApi>.Instance);

    /// <summary>Hace de servidor de la API: responde lo que la prueba le diga, falla la conexión o no contesta nunca.</summary>
    private sealed class ApiFalsa : HttpMessageHandler
    {
        private readonly HttpStatusCode estado;
        private readonly string cuerpo = "";
        private readonly Exception? error;
        private readonly bool tardar;

        public ApiFalsa(HttpStatusCode estado, string cuerpo)
        {
            this.estado = estado;
            this.cuerpo = cuerpo;
        }

        public ApiFalsa(Exception error) => this.error = error;

        public ApiFalsa(bool tardar) => this.tardar = tardar;

        public List<Uri> Pedidas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            Pedidas.Add(peticion.RequestUri!);
            if (error is not null)
                throw error;
            if (tardar)
                await Task.Delay(Timeout.Infinite, ct);

            return new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        }
    }
}
