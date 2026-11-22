using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Auditoria.Pruebas;

/// <summary>
/// RF-AUD-01 (cada registro con todos sus datos), RF-AUD-06 (el historial no se modifica ni se borra) y RD-11 (la hora se
/// guarda en UTC, como el resto del sistema).
/// </summary>
public sealed class HistorialPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    // ---------- RF-AUD-01: un registro sin alguno de sus datos no existe ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-01")]
    public void UnRegistroGuardaQuienQueAccionEntidadIdentificadorFechaValorAnteriorYNuevo()
    {
        // Preparar
        var cuando = Entorno.HoraDelNegocio(2026, 11, 22, 10, 15);
        var db = entorno.NuevoContexto();

        // Actuar
        db.Auditar(Entorno.Carla, "DesactivarUsuario", "Usuario", 12,
            new { estado = "Activo" }, new { estado = "Desactivado", sesionesCerradas = 2 }, cuando);
        db.SaveChanges();

        // Verificar
        var guardado = Assert.Single(entorno.Leer());
        Assert.Equal(1, guardado.UsuarioId);
        Assert.Equal("carla@ejemplo.com", guardado.UsuarioCorreo);
        Assert.Equal("Administrador", guardado.UsuarioRol);
        Assert.Equal("DesactivarUsuario", guardado.Accion);
        Assert.Equal("Usuario", guardado.Entidad);
        Assert.Equal("12", guardado.EntidadId);
        Assert.Equal(cuando, guardado.FechaHora);
        Assert.Equal("""{"estado":"Activo"}""", guardado.ValorAnterior);
        Assert.Equal("""{"estado":"Desactivado","sesionesCerradas":2}""", guardado.ValorNuevo);
    }

    public static TheoryData<string, QuienActua, string, string, object, object?, object?> SinAlgunDato => new()
    {
        { "sin id de usuario", new QuienActua(0, "carla@ejemplo.com", "Administrador"), "CambiarRol", "Usuario", 5, new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin correo", new QuienActua(1, " ", "Administrador"), "CambiarRol", "Usuario", 5, new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin rol", new QuienActua(1, "carla@ejemplo.com", ""), "CambiarRol", "Usuario", 5, new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin acción", Entorno.Carla, "", "Usuario", 5, new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin entidad", Entorno.Carla, "CambiarRol", " ", 5, new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin identificador", Entorno.Carla, "CambiarRol", "Usuario", "", new { rol = "Estándar" }, new { rol = "Administrador" } },
        { "sin valor anterior", Entorno.Carla, "CambiarRol", "Usuario", 5, null, new { rol = "Administrador" } },
        { "sin valor nuevo", Entorno.Carla, "CambiarRol", "Usuario", 5, new { rol = "Estándar" }, null },
        { "valor anterior vacío", Entorno.Carla, "CambiarRol", "Usuario", 5, new { }, new { rol = "Administrador" } },
        { "valor nuevo que no es un objeto", Entorno.Carla, "CambiarRol", "Usuario", 5, new { rol = "Estándar" }, "Administrador" },
    };

    [Theory]
    [Trait("Requisito", "RF-AUD-01")]
    [MemberData(nameof(SinAlgunDato))]
    public void UnRegistroAlQueLeFaltaUnDatoNoSeCreaNiSeGuarda(
        string caso, QuienActua quien, string accion, string entidad, object entidadId, object? anterior, object? nuevo)
    {
        // Preparar
        var db = entorno.NuevoContexto();

        // Actuar
        var error = Record.Exception(() => db.Auditar(quien, accion, entidad, entidadId, anterior!, nuevo!, Entorno.HoraDelNegocio(2026, 11, 22)));
        db.SaveChanges();

        // Verificar
        Assert.True(error is ArgumentException, $"{caso}: se esperaba ArgumentException y fue {error?.GetType().Name ?? "ninguna"}");
        Assert.Empty(entorno.Leer());
    }

    // ---------- RD-11: la hora se guarda en UTC, como el resto del sistema ----------

    [Fact]
    [Trait("Requisito", "RD-11")]
    public void LaFechaYHoraSeGuardaEnUtcYEsElMismoInstanteDelCambio()
    {
        // Preparar: el cambio ocurre el 22 a las 21:30 en Santo Domingo, que en UTC ya es el 23 a la 01:30.
        var cuando = Entorno.HoraDelNegocio(2026, 11, 22, 21, 30);

        // Actuar
        entorno.Registrar(cuando);

        // Verificar
        var guardado = Assert.Single(entorno.Leer());
        Assert.Equal(TimeSpan.Zero, guardado.FechaHora.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 1, 30, 0, TimeSpan.Zero), guardado.FechaHora);
        Assert.Equal(cuando, guardado.FechaHora);
    }

    // ---------- RF-AUD-06: el historial no se modifica ni se borra ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-06")]
    public async Task NingunaOperacionExpuestaModificaNiBorraRegistros()
    {
        // Preparar: tres registros y una copia de cómo están.
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20), Entorno.Carla, "CambiarRol", "Usuario", 5);
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21), Entorno.Ana, "SubirDocumento", "Documento", 3);
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22), Entorno.Carla, "AprobarSolicitud", "SolicitudPermiso", 8);
        var antes = entorno.Leer().Select(Copia).ToList();

        // Actuar: todas las operaciones que la pieza expone. Consultar (con cada filtro, con filtros inválidos, como Estándar
        // y sin sesión) y agregar un registro nuevo, que es lo único que puede escribir.
        var consulta = entorno.Consulta();
        foreach (var filtro in new[]
                 {
                     new FiltroAuditoria(), new FiltroAuditoria(Usuario: "1"), new FiltroAuditoria(Usuario: "ana@ejemplo.com"),
                     new FiltroAuditoria(Entidad: "Documento", EntidadId: "3"), new FiltroAuditoria(Desde: "2026-11-21", Hasta: "2026-11-21"),
                     new FiltroAuditoria(Usuario: "x'; DELETE FROM historial; --"), new FiltroAuditoria(Entidad: "historial"),
                 })
        {
            await consulta.ConsultarAsync(Entorno.Administrador, filtro);
            await consulta.ConsultarAsync(Entorno.Estandar, filtro);
            await consulta.ConsultarAsync(null, filtro);
        }
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22, 18, 0), Entorno.Ana, "EliminarDocumento", "Documento", 3);

        // Verificar: los tres de antes siguen idénticos, en el mismo orden, y solo se agregó el nuevo.
        var despues = entorno.Leer().Select(Copia).ToList();
        Assert.Equal(antes.Count + 1, despues.Count);
        Assert.Equal(antes, despues.Take(antes.Count));
        Assert.Contains("EliminarDocumento", despues[^1]);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-06")]
    public void LaPiezaNoTieneNingunaOperacionDeEdicionNiDeBorradoNiSiquieraOculta()
    {
        // Preparar: todos los métodos que escribimos en la pieza de Auditoría, públicos o no (sin los que genera el compilador).
        var pieza = typeof(IConsultaAuditoria).Assembly;
        var editaOBorra = new Regex("modific|edit|actualiz|cambi|elimin|borr|quit|vaci|limpi|update|delete|remove|truncate|clear",
            RegexOptions.IgnoreCase);
        const BindingFlags Todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        // Actuar
        var metodos = pieza.GetTypes()
            .Where(tipo => !tipo.Name.StartsWith('<'))
            .SelectMany(tipo => tipo.GetMethods(Todos)
                .Where(m => !m.IsSpecialName && !m.Name.StartsWith('<'))
                .Select(m => $"{tipo.Name}.{m.Name}"))
            .ToList();
        var setters = typeof(RegistroAuditoria).GetProperties().Where(p => p.SetMethod?.IsPublic == true).Select(p => p.Name).ToList();

        // Verificar: la consulta solo consulta, el registro no tiene setters públicos y ningún método edita o borra.
        Assert.Contains("HistorialAuditoria.Auditar", metodos);
        Assert.Equal(new[] { "ConsultarAsync" }, typeof(IConsultaAuditoria).GetMethods().Select(m => m.Name));
        Assert.Empty(setters);
        Assert.DoesNotContain(metodos, m => editaOBorra.IsMatch(m));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-06")]
    public void ModificarUnRegistroGuardadoSeRechazaYQuedaComoEstaba()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22));
        var db = entorno.NuevoContexto();
        var registro = db.Historial.Single();

        // Actuar: aunque el código lo intentara por debajo de la pieza, cambiando la columna en el contexto.
        db.Entry(registro).Property(r => r.ValorNuevo).CurrentValue = """{"rol":"Estándar"}""";
        var error = Record.Exception(() => db.SaveChanges());

        // Verificar
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(GuardaHistorial.Mensaje, error.Message);
        Assert.Equal("""{"rol":"Administrador"}""", Assert.Single(entorno.Leer()).ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-06")]
    public async Task BorrarUnRegistroGuardadoSeRechazaYSigueAhi()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22));
        var db = entorno.NuevoContexto();

        // Actuar
        db.Historial.Remove(db.Historial.Single());
        var error = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        // Verificar
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(GuardaHistorial.Mensaje, error.Message);
        Assert.Single(entorno.Leer());
    }

    private static string Copia(RegistroAuditoria r) =>
        $"{r.Id}|{r.FechaHora:O}|{r.UsuarioId}|{r.UsuarioCorreo}|{r.UsuarioRol}|{r.Accion}|{r.Entidad}|{r.EntidadId}|{r.ValorAnterior}|{r.ValorNuevo}";
}
