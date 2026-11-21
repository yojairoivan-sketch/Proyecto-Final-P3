using Inventario.Core.ControlAcceso;
using Inventario.Core.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.GestionPermisos.Solicitudes;

/// <summary>Lo que se muestra de una solicitud.</summary>
public sealed record SolicitudVista(
    int Id,
    int SolicitanteId,
    string SolicitanteNombre,
    string SolicitanteCorreo,
    string Permiso,
    string? Justificacion,
    EstadoSolicitud Estado,
    DateTimeOffset CreadaEn,
    int? ResueltaPorId,
    string? ResueltaPorNombre,
    DateTimeOffset? ResueltaEn,
    string? MotivoRechazo,
    DateTimeOffset? AplicadaEn);

/// <summary>
/// Solicitud y aprobación de permisos. Quién puede llamar a cada operación lo decide el host (RF-CA-05);
/// quién es el usuario se le pregunta a Control de acceso por <see cref="IUsuarioActual"/>.
/// </summary>
public interface IServicioSolicitudes
{
    /// <summary>Los permisos que se pueden pedir.</summary>
    IReadOnlyList<PermisoSolicitable> PermisosSolicitables { get; }

    /// <summary>RF-GP-01: el usuario actual pide un permiso. La solicitud nace en Pendiente.</summary>
    Task<Resultado<SolicitudVista>> CrearAsync(string? permiso, string? justificacion, CancellationToken ct = default);

    /// <summary>Las solicitudes del usuario actual, de cualquier estado. Nunca las de otro (RF-GP-02).</summary>
    Task<Resultado<IReadOnlyList<SolicitudVista>>> ListarMiasAsync(CancellationToken ct = default);

    /// <summary>RF-GP-02: las solicitudes que esperan resolución, de todos los usuarios.</summary>
    Task<IReadOnlyList<SolicitudVista>> ListarPendientesAsync(CancellationToken ct = default);

    /// <summary>Una solicitud. Un Estándar solo encuentra las suyas; las de otros no existen para él (RF-GP-02).</summary>
    Task<Resultado<SolicitudVista>> ConsultarAsync(int id, CancellationToken ct = default);

    /// <summary>RF-GP-03 y RF-GP-05: el Administrador actual la aprueba y el sistema aplica el permiso enseguida.</summary>
    Task<Resultado<SolicitudVista>> AprobarAsync(int id, CancellationToken ct = default);

    /// <summary>RF-GP-04: el Administrador actual la rechaza. Sin motivo escrito no se rechaza.</summary>
    Task<Resultado<SolicitudVista>> RechazarAsync(int id, string? motivo, CancellationToken ct = default);

    /// <summary>
    /// Aplica las solicitudes que quedaron Aprobadas sin aplicar (por ejemplo, si la aplicación se detuvo justo después de aprobar).
    /// El host lo llama al arrancar. Devuelve cuántas aplicó.
    /// </summary>
    Task<int> AplicarAprobadasAsync(CancellationToken ct = default);
}

internal sealed class ServicioSolicitudes(
    GestionPermisosDbContext db,
    IUsuarioActual usuarioActual,
    IOtorgamientoPermisos otorgamiento,
    TimeProvider reloj,
    OpcionesGestionPermisos opciones,
    INotificador notificador) : IServicioSolicitudes
{
    public const int LargoMaximoJustificacion = 500;

    public IReadOnlyList<PermisoSolicitable> PermisosSolicitables => opciones.PermisosSolicitables;

    public async Task<Resultado<SolicitudVista>> CrearAsync(string? permiso, string? justificacion, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } solicitante)
            return SinSesion<SolicitudVista>();

        if (string.IsNullOrWhiteSpace(permiso))
            return Resultado<SolicitudVista>.Falla(TipoFallo.Validacion, $"Elige el permiso que pides: {Catalogo()}.");

        var solicitable = opciones.PermisosSolicitables
            .SingleOrDefault(p => string.Equals(p.Nombre, permiso.Trim(), StringComparison.OrdinalIgnoreCase));
        if (solicitable is null)
            return Resultado<SolicitudVista>.Falla(TipoFallo.Validacion,
                $"El permiso «{permiso.Trim()}» no se puede pedir. Los que existen son: {Catalogo()}.");

        justificacion = string.IsNullOrWhiteSpace(justificacion) ? null : justificacion.Trim();
        if (justificacion?.Length > LargoMaximoJustificacion)
            return Resultado<SolicitudVista>.Falla(TipoFallo.Validacion,
                $"La justificación no puede pasar de {LargoMaximoJustificacion} caracteres.");

        if (await DuplicadaAsync(solicitante.Id, solicitable.Nombre, ct) is { } repetida)
            return repetida;

        var solicitud = SolicitudPermiso.Crear(solicitante, solicitable.Nombre, justificacion, reloj.GetUtcNow());
        db.Solicitudes.Add(solicitud);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Hallazgo 2 (Asignación 3): otra petición igual se guardó entre la comprobación y este guardado, y el índice
            // único la frenó. Se responde lo mismo que si hubiera llegado después, y no se avisa a nadie (RF-NOT-01).
            db.ChangeTracker.Clear();
            if (await DuplicadaAsync(solicitante.Id, solicitable.Nombre, ct) is { } carrera)
                return carrera;
            throw;
        }

        // RF-NOT-01: se avisa a los Administradores activos, y a nadie más.
        await notificador.NotificarAdministradoresAsync(
            $"Nueva solicitud de permiso: {solicitud.Permiso}",
            $"{solicitud.SolicitanteNombre} ({solicitud.SolicitanteCorreo}) pidió el permiso {solicitud.Permiso} " +
            $"(solicitud {solicitud.Id}).{(justificacion is null ? "" : $" Justificación: «{justificacion}».")} " +
            "Apruébala o recházala en las solicitudes pendientes.",
            ct);

        return Resultado<SolicitudVista>.Ok(Vista(solicitud),
            "Solicitud creada. Queda Pendiente hasta que un Administrador la apruebe o la rechace.");
    }

    /// <summary>El rechazo si el usuario ya tiene el permiso o una solicitud de él sin resolver; null si puede pedirlo.</summary>
    private async Task<Resultado<SolicitudVista>?> DuplicadaAsync(int solicitanteId, string permiso, CancellationToken ct)
    {
        var anteriores = await db.Solicitudes
            .Where(s => s.SolicitanteId == solicitanteId && s.Permiso == permiso)
            .Select(s => s.Estado)
            .ToListAsync(ct);
        if (anteriores.Contains(EstadoSolicitud.Aplicada))
            return Resultado<SolicitudVista>.Falla(TipoFallo.Conflicto, $"Ya tienes el permiso {permiso}.");
        if (anteriores.Any(estado => !TransicionesSolicitud.Terminales.Contains(estado)))
            return Resultado<SolicitudVista>.Falla(TipoFallo.Conflicto, $"Ya tienes una solicitud del permiso {permiso} sin resolver.");
        return null;
    }

    public async Task<Resultado<IReadOnlyList<SolicitudVista>>> ListarMiasAsync(CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<IReadOnlyList<SolicitudVista>>();

        return Resultado<IReadOnlyList<SolicitudVista>>.Ok(
            await ListarAsync(db.Solicitudes.Where(s => s.SolicitanteId == usuario.Id), ct));
    }

    public Task<IReadOnlyList<SolicitudVista>> ListarPendientesAsync(CancellationToken ct = default) =>
        ListarAsync(db.Solicitudes.Where(s => s.Estado == EstadoSolicitud.Pendiente), ct);

    public async Task<Resultado<SolicitudVista>> ConsultarAsync(int id, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<SolicitudVista>();

        var solicitud = await db.Solicitudes.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        return solicitud is null || (!usuario.EsAdministrador && solicitud.SolicitanteId != usuario.Id)
            ? NoExiste()
            : Resultado<SolicitudVista>.Ok(Vista(solicitud));
    }

    public async Task<Resultado<SolicitudVista>> AprobarAsync(int id, CancellationToken ct = default)
    {
        var aprobada = await ResolverAsync(id, EstadoSolicitud.Aprobada, motivo: null, ct);
        if (!aprobada.Exito)
            return Fallo<SolicitudVista>(aprobada);

        // RF-GP-05: el sistema aplica el permiso en la misma operación, sin que nadie haga nada más.
        var solicitud = aprobada.Valor!;
        await AplicarAsync(solicitud, ct);

        // RF-NOT-02: el solicitante sabe que se aprobó.
        await notificador.NotificarAsync(solicitud.SolicitanteId,
            $"Tu solicitud de permiso fue aprobada: {solicitud.Permiso}",
            $"{solicitud.ResueltaPorNombre} aprobó tu solicitud {solicitud.Id} del permiso {solicitud.Permiso}. Ya puedes usarlo.",
            ct);
        return Resultado<SolicitudVista>.Ok(Vista(solicitud),
            $"Solicitud aprobada. {solicitud.SolicitanteNombre} ya puede usar {solicitud.Permiso}.");
    }

    public async Task<Resultado<SolicitudVista>> RechazarAsync(int id, string? motivo, CancellationToken ct = default)
    {
        var rechazada = await ResolverAsync(id, EstadoSolicitud.Rechazada, motivo, ct);
        if (!rechazada.Exito)
            return Fallo<SolicitudVista>(rechazada);

        // RF-NOT-02: el solicitante sabe que se rechazó y por qué.
        var solicitud = rechazada.Valor!;
        await notificador.NotificarAsync(solicitud.SolicitanteId,
            $"Tu solicitud de permiso fue rechazada: {solicitud.Permiso}",
            $"{solicitud.ResueltaPorNombre} rechazó tu solicitud {solicitud.Id} del permiso {solicitud.Permiso}. " +
            $"Motivo: {solicitud.MotivoRechazo}",
            ct);
        return Resultado<SolicitudVista>.Ok(Vista(solicitud), "Solicitud rechazada. El solicitante verá el motivo.");
    }

    public async Task<int> AplicarAprobadasAsync(CancellationToken ct = default)
    {
        var aprobadas = await db.Solicitudes.Where(s => s.Estado == EstadoSolicitud.Aprobada).ToListAsync(ct);
        foreach (var solicitud in aprobadas)
            await AplicarAsync(solicitud, ct);
        return aprobadas.Count;
    }

    /// <summary>Un Administrador lleva la solicitud a <paramref name="hacia"/>. Si la transición se rechaza, no se guarda nada.</summary>
    private async Task<Resultado<SolicitudPermiso>> ResolverAsync(int id, EstadoSolicitud hacia, string? motivo, CancellationToken ct)
    {
        if (usuarioActual.Usuario is not { } administrador)
            return SinSesion<SolicitudPermiso>();

        var solicitud = await db.Solicitudes.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (solicitud is null)
            return Fallo<SolicitudPermiso>(NoExiste());

        var intento = new IntentoTransicion(Actor.Administrador(administrador.Id, administrador.Nombre), reloj.GetUtcNow(), motivo);
        var cambio = solicitud.CambiarEstado(hacia, intento);
        if (!cambio.Exito)
            return Fallo<SolicitudPermiso>(cambio);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Resultado<SolicitudPermiso>.Falla(TipoFallo.Conflicto,
                "Otro Administrador resolvió esta solicitud al mismo tiempo. Vuelve a consultarla para ver su estado.");
        }

        return Resultado<SolicitudPermiso>.Ok(solicitud);
    }

    /// <summary>Otorga el permiso por Control de acceso y pasa la solicitud de Aprobada a Aplicada.</summary>
    private async Task AplicarAsync(SolicitudPermiso solicitud, CancellationToken ct)
    {
        await otorgamiento.OtorgarAsync(solicitud.SolicitanteId, solicitud.Permiso, ct);

        var cambio = solicitud.CambiarEstado(EstadoSolicitud.Aplicada, new IntentoTransicion(Actor.Sistema, reloj.GetUtcNow()));
        if (!cambio.Exito)
            throw new InvalidOperationException(cambio.Mensaje);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<SolicitudVista>> ListarAsync(IQueryable<SolicitudPermiso> consulta, CancellationToken ct)
    {
        var solicitudes = await consulta.AsNoTracking().OrderBy(s => s.CreadaEn).ThenBy(s => s.Id).ToListAsync(ct);
        return solicitudes.Select(Vista).ToList();
    }

    private string Catalogo() => string.Join(", ", opciones.PermisosSolicitables.Select(p => p.Nombre));

    private static SolicitudVista Vista(SolicitudPermiso s) => new(
        s.Id, s.SolicitanteId, s.SolicitanteNombre, s.SolicitanteCorreo, s.Permiso, s.Justificacion, s.Estado, s.CreadaEn,
        s.ResueltaPorId, s.ResueltaPorNombre, s.ResueltaEn, s.MotivoRechazo, s.AplicadaEn);

    private static Resultado<SolicitudVista> NoExiste() =>
        Resultado<SolicitudVista>.Falla(TipoFallo.NoEncontrado, "La solicitud no existe.");

    private static Resultado<T> Fallo<T>(Resultado fallido) => Resultado<T>.Falla(fallido.Fallo!.Value, fallido.Mensaje);

    private static Resultado<T> SinSesion<T>() =>
        Resultado<T>.Falla(TipoFallo.NoAutenticado, "Necesitas iniciar sesión para usar esta operación.");
}
