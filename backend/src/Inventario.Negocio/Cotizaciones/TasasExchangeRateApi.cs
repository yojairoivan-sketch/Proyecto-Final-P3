using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Core.ControlAcceso;
using Microsoft.Extensions.Logging;

namespace Inventario.Negocio.Cotizaciones;

/// <summary>
/// Tasas de cambio de ExchangeRate-API (https://www.exchangerate-api.com), que exige una clave. Usa la consulta
/// «pair»: <c>GET {UrlBase}/{clave}/pair/{desde}/{hacia}</c>. La clave va en la ruta, así que la URL nunca se escribe
/// en el log ni en un mensaje.
/// </summary>
internal sealed class TasasExchangeRateApi(HttpClient http, OpcionesTasasDeCambio opciones, ILogger<TasasExchangeRateApi> registro)
    : ITasasDeCambio
{
    public string Fuente => "ExchangeRate-API";

    public async Task<Resultado<TasaDeCambio>> ObtenerAsync(string desde, string hacia, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(opciones.Clave))
            return NoDisponible($"La cotización en otra moneda no está disponible: falta la clave de la API de tasas de cambio (variable {OpcionesTasasDeCambio.VariableClave} en .env).");

        var url = $"{opciones.UrlBase.TrimEnd('/')}/{Uri.EscapeDataString(opciones.Clave.Trim())}/pair/{desde}/{hacia}";
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(opciones.Espera);

        RespuestaPar? cuerpo;
        try
        {
            using var respuesta = await http.GetAsync(url, limite.Token);
            cuerpo = await LeerAsync(respuesta, limite.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException)
        {
            // Sin la URL: lleva la clave.
            registro.LogWarning("La API de tasas de cambio no respondió ({Tipo}).", ex.GetType().Name);
            return NoDisponible("El servicio de tasas de cambio no respondió. Intenta de nuevo en unos minutos.");
        }

        if (cuerpo is { Resultado: "success", Tasa: > 0 } exito)
            return Resultado<TasaDeCambio>.Ok(new TasaDeCambio(desde, hacia, exito.Tasa.Value,
                DateTimeOffset.FromUnixTimeSeconds(exito.ActualizadaUnix ?? 0)));

        registro.LogWarning("La API de tasas de cambio respondió con error: {Error}.", cuerpo?.TipoError ?? "respuesta inesperada");
        return cuerpo?.TipoError switch
        {
            "invalid-key" => NoDisponible($"El servicio de tasas de cambio rechazó la clave de {OpcionesTasasDeCambio.VariableClave}. Revisa que sea una clave válida de exchangerate-api.com."),
            "inactive-account" => NoDisponible("La cuenta del servicio de tasas de cambio no está activa todavía (falta confirmar el correo del registro)."),
            "quota-reached" => NoDisponible("Se agotaron las consultas del mes en el servicio de tasas de cambio. Intenta de nuevo más adelante."),
            "unsupported-code" => Resultado<TasaDeCambio>.Falla(TipoFallo.Validacion, $"El servicio de tasas de cambio no conoce la moneda {hacia}."),
            _ => NoDisponible("El servicio de tasas de cambio respondió algo inesperado. Intenta de nuevo más tarde."),
        };
    }

    /// <summary>El JSON de la respuesta, sea de éxito o de error (la API manda el motivo en el cuerpo), o null si no es JSON.</summary>
    private static async Task<RespuestaPar?> LeerAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<RespuestaPar>(ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static Resultado<TasaDeCambio> NoDisponible(string mensaje) => Resultado<TasaDeCambio>.Falla(TipoFallo.NoDisponible, mensaje);

    private sealed record RespuestaPar(
        [property: JsonPropertyName("result")] string? Resultado,
        [property: JsonPropertyName("error-type")] string? TipoError,
        [property: JsonPropertyName("conversion_rate")] decimal? Tasa,
        [property: JsonPropertyName("time_last_update_unix")] long? ActualizadaUnix);
}
