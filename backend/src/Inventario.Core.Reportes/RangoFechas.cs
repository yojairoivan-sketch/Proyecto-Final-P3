using System.Globalization;
using Inventario.Core.ControlAcceso;

namespace Inventario.Core.Reportes;

/// <summary>Un mes dentro del rango: su nombre (AAAA-MM) y sus límites en UTC, recortados al rango.</summary>
public sealed record Periodo(string Mes, DateTimeOffset Inicio, DateTimeOffset FinExclusivo);

/// <summary>
/// RF-REP-05: el rango de fechas de un reporte. <see cref="Desde"/> y <see cref="Hasta"/> son días en UTC y los dos se
/// incluyen completos: el rango va de Desde a las 00:00 UTC hasta el día siguiente a Hasta a las 00:00 UTC, sin incluirlo (RD-11).
/// </summary>
public sealed record RangoFechas(DateOnly Desde, DateOnly Hasta)
{
    public const string Formato = "yyyy-MM-dd";

    /// <summary>Hasta dos años por consulta, para que un reporte nunca recorra la base entera sin querer.</summary>
    public const int MaximoDias = 731;

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

    private static DateTimeOffset Medianoche(DateOnly dia) => new(dia.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static Resultado<RangoFechas> Error(string mensaje) => Resultado<RangoFechas>.Falla(TipoFallo.Validacion, mensaje);
}
