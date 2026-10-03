namespace Inventario.Core.ControlAcceso;

/// <summary>Configuración de la pieza. La URL pública sale de la variable URL_PUBLICA.</summary>
public sealed class OpcionesControlAcceso
{
    /// <summary>Dirección con la que se abre la aplicación; con ella se arman los enlaces de los correos.</summary>
    public string UrlPublica { get; set; } = "http://localhost:8080";

    public TimeSpan VigenciaActivacion { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan VigenciaSesion { get; set; } = TimeSpan.FromHours(8);
}
