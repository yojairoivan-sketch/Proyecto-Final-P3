using System.Globalization;
using Inventario.Core.ControlAcceso;

namespace Inventario.Core.Reportes;

/// <summary>Un mes del negocio dentro del rango: su nombre (AAAA-MM) y sus límites expresados en UTC, recortados al rango.</summary>
public sealed record Periodo(string Mes, DateTimeOffset Inicio, DateTimeOffset FinExclusivo);

/// <summary>
/// RF-REP-05: el rango de fechas de un reporte. <see cref="Desde"/> y <see cref="Hasta"/> son días del calendario del negocio
/// (hora de República Dominicana, <see cref="DesfaseNegocio"/>) y los dos se incluyen completos: el rango va de Desde a las 00:00
/// hasta el día siguiente a Hasta a las 00:00, sin incluirlo. Los límites se devuelven en UTC, como se guardan las fechas (RD-11).
/// Hallazgo 3 (Asignación 3): antes los días se cortaban a las 00:00 UTC, que en Santo Domingo son las 20:00 del día anterior.
/// </summary>
public sealed record RangoFechas(DateOnly Desde, DateOnly Hasta)
{
    public const string Formato = "yyyy-MM-dd";

    /// <summary>
    /// El día del negocio: República Dominicana, UTC−4 todo el año (no tiene horario de verano). Un reporte de «noviembre»
    /// cuenta lo que pasó en noviembre en Santo Domingo, no en Greenwich.
    /// </summary>
    public static readonly TimeSpan DesfaseNegocio = TimeSpan.FromHours(-4);

    /// <summary>Hasta dos años por consulta, para que un reporte nunca recorra la base entera sin querer.</summary>
    public const int MaximoDias = 731;

    /// <summary>Años que se aceptan. Fuera de ellos no hay datos, y cerca de los extremos de DateOnly el fin del rango no existe.</summary>
    public const int PrimerAnio = 1900;
    public const int UltimoAnio = 2100;

    public DateTimeOffset Inicio => Medianoche(Desde);

    public DateTimeOffset FinExclusivo => Medianoche(Hasta.AddDays(1));

    /// <summary>Lee y valida las dos fechas. Cada error dice qué corregir (RD-07).</summary>
    public static Resultado<RangoFechas> Leer(string? desde, string? hasta)
    {
        if (string.IsNullOrWhiteSpace(desde) || string.IsNullOrWhiteSpace(hasta))
            return Error($"Indica el rango de fechas con desde y hasta, en formato AAAA-MM-DD (por ejemplo ?desde=2026-11-01&hasta=2026-11-30).");

        if (!Fecha(desde, out var inicio))
            return Error($"La fecha desde «{desde.Trim()}» no es válida. Usa el formato AAAA-MM-DD, por ejemplo 2026-11-01.");
        if (!Fecha(hasta, out var fin))
            return Error($"La fecha hasta «{hasta.Trim()}» no es válida. Usa el formato AAAA-MM-DD, por ejemplo 2026-11-30.");

        if (inicio.Year is < PrimerAnio or > UltimoAnio || fin.Year is < PrimerAnio or > UltimoAnio)
            return Error($"Las fechas deben estar entre {PrimerAnio} y {UltimoAnio}.");
        if (inicio > fin)
            return Error($"La fecha desde ({inicio:yyyy-MM-dd}) es posterior a hasta ({fin:yyyy-MM-dd}). Invierte las fechas.");
        if (fin.DayNumber - inicio.DayNumber + 1 > MaximoDias)
            return Error($"El rango no puede pasar de {MaximoDias} días. Pide un rango más corto.");

        return Resultado<RangoFechas>.Ok(new RangoFechas(inicio, fin));
    }

    /// <summary>Los meses que toca el rango, en orden, cada uno recortado a los límites del rango.</summary>
    public IReadOnlyList<Periodo> Meses()
    {
        var meses = new List<Periodo>();
        var primero = new DateOnly(Desde.Year, Desde.Month, 1);
        for (var mes = primero; mes <= Hasta; mes = mes.AddMonths(1))
        {
            var inicio = Medianoche(mes) < Inicio ? Inicio : Medianoche(mes);
            var fin = Medianoche(mes.AddMonths(1)) > FinExclusivo ? FinExclusivo : Medianoche(mes.AddMonths(1));
            meses.Add(new Periodo($"{mes:yyyy-MM}", inicio, fin));
        }
        return meses;
    }

    private static bool Fecha(string texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(texto.Trim(), Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    /// <summary>Medianoche de ese día en la hora del negocio, expresada en UTC (Npgsql solo acepta desfase 0 en timestamptz).</summary>
    private static DateTimeOffset Medianoche(DateOnly dia) =>
        new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), DesfaseNegocio).ToUniversalTime();

    private static Resultado<RangoFechas> Error(string mensaje) => Resultado<RangoFechas>.Falla(TipoFallo.Validacion, mensaje);
}
