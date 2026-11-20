using Inventario.Core.ControlAcceso;

namespace Inventario.Core.Reportes.Pruebas;

/// <summary>Criterios R-06 y R-07: el rango de fechas que comparten los dos reportes (RF-REP-05).</summary>
public sealed class RangoFechasPruebas
{
    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public void ElRangoIncluyeCompletosElPrimerYElUltimoDiaEnUtc()
    {
        // Preparar
        var (desde, hasta) = ("2026-11-01", "2026-11-30");

        // Actuar
        var rango = RangoFechas.Leer(desde, hasta).Valor!;

        // Verificar
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), rango.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), rango.FinExclusivo);
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public void UnSoloDiaEsUnRangoValido()
    {
        // Preparar
        var dia = "2026-11-20";

        // Actuar
        var rango = RangoFechas.Leer(dia, dia);

        // Verificar
        Assert.True(rango.Exito, rango.Mensaje);
        Assert.Equal(TimeSpan.FromDays(1), rango.Valor!.FinExclusivo - rango.Valor.Inicio);
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public void LosMesesDelRangoQuedanRecortadosASusLimites()
    {
        // Preparar
        var rango = RangoFechas.Leer("2026-10-15", "2026-12-10").Valor!;

        // Actuar
        var meses = rango.Meses();

        // Verificar
        Assert.Equal(["2026-10", "2026-11", "2026-12"], meses.Select(m => m.Mes));
        Assert.Equal(rango.Inicio, meses[0].Inicio);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), meses[0].FinExclusivo);
        Assert.Equal(rango.FinExclusivo, meses[2].FinExclusivo);
    }

    [Theory]
    [Trait("Requisito", "RF-REP-05")]
    [InlineData(null, "2026-11-30", "Indica el rango")]
    [InlineData("2026-11-01", " ", "Indica el rango")]
    [InlineData("01/11/2026", "2026-11-30", "desde «01/11/2026» no es válida")]
    [InlineData("2026-11-01", "2026-02-30", "hasta «2026-02-30» no es válida")]
    [InlineData("2026-12-01", "2026-11-01", "es posterior a hasta")]
    [InlineData("2024-01-01", "2026-11-30", "no puede pasar de 731 días")]
    public void UnRangoInvalidoSeRechazaConUnMensajeQueDiceQueCorregir(string? desde, string? hasta, string mensaje)
    {
        // Preparar
        var (inicio, fin) = (desde, hasta);

        // Actuar
        var rango = RangoFechas.Leer(inicio, fin);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, rango.Fallo);
        Assert.Contains(mensaje, rango.Mensaje);
    }
}
