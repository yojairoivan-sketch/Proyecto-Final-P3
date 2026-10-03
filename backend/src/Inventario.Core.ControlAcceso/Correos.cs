using System.Globalization;

namespace Inventario.Core.ControlAcceso;

/// <summary>Textos de los correos que envía Control de acceso. Todos salen por la cola (RF-NOT-08).</summary>
internal static class Correos
{
    public static (string Asunto, string Cuerpo) Activacion(string nombre, string enlace, DateTimeOffset venceEn) => (
        "Activa tu cuenta de Inventario",
        $"""
        Hola, {nombre}:

        Para activar tu cuenta abre este enlace:
        {enlace}

        El enlace sirve una sola vez y vence el {Fecha(venceEn)}.
        Si no creaste esta cuenta, ignora este correo.
        """);

    public static (string Asunto, string Cuerpo) Recuperacion(string nombre, string enlace, string codigo, DateTimeOffset venceEn) => (
        "Código para restablecer tu contraseña de Inventario",
        $"""
        Hola, {nombre}:

        Pediste restablecer tu contraseña. Abre este enlace para definir una nueva:
        {enlace}

        O escribe este código en la página «Restablecer contraseña»:
        {codigo}

        El código sirve una sola vez y vence el {Fecha(venceEn)}.
        Si no lo pediste, ignora este correo: tu contraseña no cambia.
        """);

    public static string Fecha(DateTimeOffset fecha) =>
        fecha.ToUniversalTime().ToString("dd/MM/yyyy 'a las' HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string Enlace(OpcionesControlAcceso opciones, string ruta, string parametro, string valor) =>
        $"{opciones.UrlPublica.TrimEnd('/')}/{ruta}?{parametro}={Uri.EscapeDataString(valor)}";
}
