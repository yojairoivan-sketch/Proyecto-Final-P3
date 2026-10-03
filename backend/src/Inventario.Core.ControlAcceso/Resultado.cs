namespace Inventario.Core.ControlAcceso;

/// <summary>Por qué falló una operación. El host decide cómo mostrarlo (por ejemplo, el código HTTP).</summary>
public enum TipoFallo
{
    Validacion,
    Conflicto,
    NoAutenticado,
    CuentaInactiva,
    Bloqueado,
    Prohibido,
    NoEncontrado,
}

/// <summary>
/// Respuesta de los servicios de Control de acceso: éxito o un fallo con un mensaje controlado para el usuario.
/// Las reglas viven en los servicios; los endpoints solo traducen esto a HTTP (RD-02).
/// </summary>
public class Resultado
{
    protected Resultado(bool exito, TipoFallo? fallo, string mensaje)
    {
        Exito = exito;
        Fallo = fallo;
        Mensaje = mensaje;
    }

    public bool Exito { get; }
    public TipoFallo? Fallo { get; }
    public string Mensaje { get; }

    public static Resultado Ok(string mensaje) => new(true, null, mensaje);

    public static Resultado Falla(TipoFallo fallo, string mensaje) => new(false, fallo, mensaje);
}

public sealed class Resultado<T> : Resultado
{
    private Resultado(bool exito, TipoFallo? fallo, string mensaje, T? valor)
        : base(exito, fallo, mensaje) => Valor = valor;

    public T? Valor { get; }

    public static Resultado<T> Ok(T valor, string mensaje = "") => new(true, null, mensaje, valor);

    public static new Resultado<T> Falla(TipoFallo fallo, string mensaje) => new(false, fallo, mensaje, default);
}
