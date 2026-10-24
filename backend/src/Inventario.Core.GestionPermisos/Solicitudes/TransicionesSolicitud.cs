using Inventario.Core.ControlAcceso;
using static Inventario.Core.GestionPermisos.Solicitudes.EstadoSolicitud;

namespace Inventario.Core.GestionPermisos.Solicitudes;

/// <summary>Quién ejecuta una transición: un Administrador desde la API, o el propio sistema.</summary>
public enum Ejecutor
{
    Administrador,
    Sistema,
}

/// <summary>Quién intenta el cambio. Un Administrador lleva su id y su nombre; el sistema no tiene ninguno.</summary>
public sealed record Actor(Ejecutor Tipo, int? UsuarioId = null, string? Nombre = null)
{
    public static Actor Sistema { get; } = new(Ejecutor.Sistema);

    public static Actor Administrador(int usuarioId, string nombre) => new(Ejecutor.Administrador, usuarioId, nombre);
}

/// <summary>Un intento de cambio de estado: quién, cuándo y, para un rechazo, el motivo.</summary>
public sealed record IntentoTransicion(Actor Actor, DateTimeOffset Ahora, string? Motivo = null);

/// <summary>Una transición permitida: desde, hacia, quién la ejecuta, con qué condición y qué deja registrado.</summary>
/// <param name="Validar">Comprueba la condición; devuelve el motivo del rechazo o null si se cumple.</param>
/// <param name="Registrar">Lo que la transición anota en la solicitud (quién, cuándo, motivo).</param>
public sealed record TransicionSolicitud(
    EstadoSolicitud Desde,
    EstadoSolicitud Hacia,
    Ejecutor Quien,
    string Condicion,
    Action<SolicitudPermiso, IntentoTransicion> Registrar,
    Func<IntentoTransicion, string?>? Validar = null);

/// <summary>
/// RD-04: el único punto del código donde se resuelven las transiciones de una solicitud de permiso.
/// Agregar una transición nueva es agregar una línea a <see cref="Permitidas"/>; nadie más valida estados.
/// Cualquier combinación que no esté aquí está prohibida (RF-GP-06).
/// </summary>
public static class TransicionesSolicitud
{
    public const int LargoMaximoMotivo = 500;

    public static readonly IReadOnlyList<TransicionSolicitud> Permitidas =
    [
        new(Pendiente, Aprobada, Ejecutor.Administrador, "La solicitud está en Pendiente. Queda registrado quién aprobó y cuándo.",
            Registrar: (solicitud, intento) => solicitud.RegistrarResolucion(intento.Actor, intento.Ahora, motivo: null)),
        new(Aprobada, Aplicada, Ejecutor.Sistema, "Automática, inmediatamente después de aprobar y de otorgar el permiso.",
            Registrar: (solicitud, intento) => solicitud.RegistrarAplicacion(intento.Ahora)),
        new(Pendiente, Rechazada, Ejecutor.Administrador, "Exige un motivo escrito. Queda registrado quién rechazó, cuándo y por qué.",
            Registrar: (solicitud, intento) => solicitud.RegistrarResolucion(intento.Actor, intento.Ahora, intento.Motivo!.Trim()),
            Validar: intento => ValidarMotivo(intento.Motivo)),
        new(Aprobada, Pendiente, Ejecutor.Administrador, "Un Administrador la devuelve a Pendiente.",
            Registrar: (solicitud, intento) => { }),
    ];

    /// <summary>Estados de los que no parte ninguna transición: Aplicada y Rechazada.</summary>
    public static IReadOnlyList<EstadoSolicitud> Terminales { get; } =
        Enum.GetValues<EstadoSolicitud>().Where(estado => Permitidas.All(t => t.Desde != estado)).ToList();

    public static TransicionSolicitud? Buscar(EstadoSolicitud desde, EstadoSolicitud hacia) =>
        Permitidas.SingleOrDefault(t => t.Desde == desde && t.Hacia == hacia);

    /// <summary>
    /// Intenta llevar la solicitud a <paramref name="hacia"/>. Si la transición no está en la tabla, la ejecuta quien no debe,
    /// un Administrador resuelve su propia solicitud (RF-GP-07) o no se cumple la condición, se rechaza y nada cambia.
    /// </summary>
    internal static Resultado Resolver(SolicitudPermiso solicitud, EstadoSolicitud hacia, IntentoTransicion intento)
    {
        var desde = solicitud.Estado;

        if (Buscar(desde, hacia) is not { } transicion)
            return Resultado.Falla(TipoFallo.Conflicto,
                $"Una solicitud {desde} no puede pasar a {hacia}: solo se permite Pendiente → Aprobada → Aplicada " +
                $"y Pendiente → Rechazada. La solicitud sigue {desde}.");

        if (transicion.Quien != intento.Actor.Tipo)
            return Resultado.Falla(TipoFallo.Conflicto, transicion.Quien == Ejecutor.Sistema
                ? $"Pasar una solicitud de {desde} a {hacia} lo hace el sistema al aprobarla, no una persona."
                : $"Pasar una solicitud de {desde} a {hacia} lo hace un Administrador, no el sistema.");

        if (intento.Actor.Tipo == Ejecutor.Administrador && intento.Actor.UsuarioId == solicitud.SolicitanteId)
            return Resultado.Falla(TipoFallo.Prohibido,
                "Nadie resuelve su propia solicitud: otro Administrador debe aprobarla o rechazarla.");

        if (transicion.Validar?.Invoke(intento) is { } problema)
            return Resultado.Falla(TipoFallo.Validacion, problema);

        transicion.Registrar(solicitud, intento);
        solicitud.PasarA(hacia);
        return Resultado.Ok($"La solicitud pasó de {desde} a {hacia}.");
    }

    private static string? ValidarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return "Escribe el motivo del rechazo: sin motivo la solicitud no se rechaza.";
        return motivo.Trim().Length > LargoMaximoMotivo
            ? $"El motivo no puede pasar de {LargoMaximoMotivo} caracteres."
            : null;
    }
}
