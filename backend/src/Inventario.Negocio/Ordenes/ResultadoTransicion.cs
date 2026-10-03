namespace Inventario.Negocio.Ordenes;

/// <summary>Resultado de intentar un cambio de estado. Si se rechaza, el estado no cambia.</summary>
public sealed record ResultadoTransicion(bool Exito, string Mensaje)
{
    public static ResultadoTransicion Aplicada(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        new(true, $"La orden pasó de {desde} a {hacia}.");

    public static ResultadoTransicion Rechazada(string motivo) => new(false, motivo);
}
