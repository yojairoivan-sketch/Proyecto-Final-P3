using Inventario.Core.ControlAcceso;

namespace Inventario.Negocio;

/// <summary>
/// RD-07: la entrada externa del negocio se valida antes de usarse. Un dato ausente, vacío o fuera de rango
/// produce un mensaje controlado, nunca una excepción. <c>campo</c> va con su artículo: «la cantidad».
/// </summary>
internal static class ValidacionNegocio
{
    public const decimal CantidadMaxima = 1_000_000m;
    public const decimal CostoMaximo = 100_000_000m;

    /// <summary>Texto obligatorio, sin espacios a los lados.</summary>
    public static (string? Valor, string? Error) Texto(string? valor, string campo, int largoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return (null, $"Falta {campo}.");

        var texto = valor.Trim();
        return texto.Length > largoMaximo
            ? (null, $"{Mayuscula(campo)} no puede pasar de {largoMaximo} caracteres.")
            : (texto, null);
    }

    /// <summary>Texto opcional: vacío cuenta como ausente.</summary>
    public static (string? Valor, string? Error) TextoOpcional(string? valor, string campo, int largoMaximo) =>
        string.IsNullOrWhiteSpace(valor) ? (null, null) : Texto(valor, campo, largoMaximo);

    /// <summary>Un número con hasta dos decimales, entre cero (o más de cero) y <paramref name="maximo"/>.</summary>
    public static string? Numero(decimal? valor, string campo, decimal maximo, bool admiteCero)
    {
        if (valor is not { } numero)
            return $"Falta {campo}.";
        if (numero < 0)
            return $"{Mayuscula(campo)} no puede ser menor que cero.";
        if (numero == 0 && !admiteCero)
            return $"{Mayuscula(campo)} tiene que ser mayor que cero.";
        if (numero > maximo)
            return $"{Mayuscula(campo)} no puede pasar de {maximo:N0}.";
        return decimal.Round(numero, 2) == numero ? null : $"{Mayuscula(campo)} admite como máximo dos decimales.";
    }

    /// <summary>Junta los errores de varios campos en un solo rechazo, o null si no hay ninguno.</summary>
    public static Resultado<T>? Rechazo<T>(params string?[] errores)
    {
        var hay = errores.Where(e => e is not null).ToArray();
        return hay.Length == 0 ? null : Resultado<T>.Falla(TipoFallo.Validacion, string.Join(" ", hay));
    }

    private static string Mayuscula(string texto) => char.ToUpperInvariant(texto[0]) + texto[1..];
}
