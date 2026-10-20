namespace Inventario.Api;

/// <summary>
/// RD-11: la única fuente de hora de la API. Entrega la hora UTC cortada a microsegundos, que es lo que guarda
/// PostgreSQL (timestamptz). Así la hora que devuelve una operación es exactamente la que queda en la base: por ejemplo,
/// el resueltaEn de «aprobar» es el mismo que se ve al consultar la solicitud después.
/// </summary>
public sealed class RelojDelSistema : TimeProvider
{
    private const long TicksPorMicrosegundo = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow()
    {
        var ahora = System.GetUtcNow();
        return ahora.AddTicks(-(ahora.Ticks % TicksPorMicrosegundo));
    }
}
