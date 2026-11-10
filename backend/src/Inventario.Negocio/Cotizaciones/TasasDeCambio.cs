using Inventario.Core.ControlAcceso;

namespace Inventario.Negocio.Cotizaciones;

/// <summary>Cuántas unidades de <see cref="Hacia"/> vale una unidad de <see cref="Desde"/>, y cuándo la publicó la fuente.</summary>
public sealed record TasaDeCambio(string Desde, string Hacia, decimal Valor, DateTimeOffset ActualizadaEn);

/// <summary>
/// Interfaz requerida por el negocio: de dónde salen las tasas de cambio. La implementa <see cref="TasasExchangeRateApi"/>,
/// que las pide a una API externa; en las pruebas se reemplaza por un doble.
/// </summary>
public interface ITasasDeCambio
{
    /// <summary>Nombre de la fuente, para mostrarlo junto a la cotización.</summary>
    string Fuente { get; }

    /// <summary>
    /// La tasa vigente de <paramref name="desde"/> a <paramref name="hacia"/> (códigos ISO 4217 de tres letras).
    /// Si la API no está configurada o no responde, un fallo <see cref="TipoFallo.NoDisponible"/> con un mensaje
    /// comprensible; nunca una excepción (RD-07, RD-08).
    /// </summary>
    Task<Resultado<TasaDeCambio>> ObtenerAsync(string desde, string hacia, CancellationToken ct = default);
}

public sealed class OpcionesTasasDeCambio
{
    /// <summary>La variable de entorno que trae la clave.</summary>
    public const string VariableClave = "TASA_CAMBIO_API_CLAVE";

    /// <summary>La clave de la API. Sale solo de la variable de entorno TASA_CAMBIO_API_CLAVE (RD-10), nunca del repositorio.</summary>
    public string? Clave { get; set; }

    public string UrlBase { get; set; } = "https://v6.exchangerate-api.com/v6";

    /// <summary>Cuánto se espera a la API antes de darla por caída.</summary>
    public TimeSpan Espera { get; set; } = TimeSpan.FromSeconds(10);
}
