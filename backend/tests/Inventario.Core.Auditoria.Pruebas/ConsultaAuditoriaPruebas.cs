namespace Inventario.Core.Auditoria.Pruebas;

/// <summary>
/// RF-AUD-07 y RD-07: la consulta del historial a través de IConsultaAuditoria, como la usa la API. Los registros se dejan
/// en la base con <see cref="HistorialAuditoria.Auditar"/>, como lo hace cada pieza auditada.
/// </summary>
public sealed class ConsultaAuditoriaPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    // ---------- RF-AUD-07: solo el Administrador ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task UnEstandarNoConsultaElHistorial()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Estandar, new FiltroAuditoria());

        // Verificar
        Assert.Null(resultado.Vista);
        Assert.Equal(FalloConsulta.Prohibido, resultado.Fallo);
        Assert.Equal("La operación ConsultarAuditoria es solo para: Administrador. Tu rol es Estándar.", resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task UnEstandarConUnFiltroInvalidoRecibeElMismoRechazoYNoUnoDeFiltro()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Estandar, new FiltroAuditoria(Entidad: "Inventada"));

        // Verificar
        Assert.Null(resultado.Vista);
        Assert.Equal(FalloConsulta.Prohibido, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task SinSesionNoSeConsultaElHistorial()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(null, new FiltroAuditoria());

        // Verificar
        Assert.Null(resultado.Vista);
        Assert.Equal(FalloConsulta.Prohibido, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task ElAdministradorVeTodosLosRegistrosDelMasRecienteAlMasAntiguo()
    {
        // Preparar
        var primero = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));
        var segundo = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21));
        var tercero = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria());

        // Verificar
        Assert.Null(resultado.Fallo);
        Assert.Equal(3, resultado.Vista!.Total);
        Assert.Equal(new[] { tercero, segundo, primero }, resultado.Vista.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task CadaRegistroSaleConSusSieteDatosYLosValoresComoJson()
    {
        // Preparar
        var cuando = Entorno.HoraDelNegocio(2026, 11, 22, 9, 30);
        entorno.Registrar(cuando, Entorno.Carla, "CambiarRol", "Usuario", 7);

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria());

        // Verificar
        var registro = Assert.Single(resultado.Vista!.Registros);
        Assert.Equal(1, registro.UsuarioId);
        Assert.Equal("carla@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Administrador", registro.UsuarioRol);
        Assert.Equal("CambiarRol", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal("7", registro.EntidadId);
        Assert.Equal(cuando.ToUniversalTime(), registro.FechaHora);
        Assert.Equal("Estándar", registro.ValorAnterior.GetProperty("rol").GetString());
        Assert.Equal("Administrador", registro.ValorNuevo.GetProperty("rol").GetString());
    }

    // ---------- RF-AUD-07: cada filtro ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task ElFiltroDeUsuarioAceptaSuIdOSuCorreoYDejaSoloLoQueHizoEseUsuario()
    {
        // Preparar
        var deCarla = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20), Entorno.Carla);
        var deAna = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21), Entorno.Ana, "SubirDocumento", "Documento", 3);

        // Actuar
        var porId = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Usuario: "10"));
        var porCorreo = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Usuario: " Carla@Ejemplo.com "));

        // Verificar
        Assert.Equal(new[] { deAna }, porId.Vista!.Registros.Select(r => r.Id));
        Assert.Equal(new[] { deCarla }, porCorreo.Vista!.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task ElFiltroDeEntidadDejaSoloEseTipoYConEntidadIdSoloEsaCosa()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20), accion: "CambiarRol", entidad: "Usuario", entidadId: 3);
        var subida = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21), accion: "SubirDocumento", entidad: "Documento", entidadId: 3);
        var otraSubida = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21), accion: "SubirDocumento", entidad: "Documento", entidadId: 4);
        var eliminacion = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22), accion: "EliminarDocumento", entidad: "Documento", entidadId: 3);

        // Actuar
        var documentos = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Entidad: "documento"));
        var elTres = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Entidad: "Documento", EntidadId: "3"));

        // Verificar
        Assert.Equal(new[] { eliminacion, otraSubida, subida }, documentos.Vista!.Registros.Select(r => r.Id));
        Assert.Equal(new[] { eliminacion, subida }, elTres.Vista!.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task ElRangoDeFechasIncluyeElPrimerYElUltimoDiaCompletosEnElDiaDelNegocio()
    {
        // Preparar: dos registros justo dentro del rango y dos justo fuera, en hora de Santo Domingo.
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 10, 31, 23, 59, 59));
        var alEmpezar = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 1, 0, 0, 0));
        // 30 de noviembre a las 23:59:59 en Santo Domingo = 1 de diciembre a las 03:59:59 UTC: es del último día.
        var alTerminar = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 30, 23, 59, 59));
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 12, 1, 0, 0, 0));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador,
            new FiltroAuditoria(Desde: "2026-11-01", Hasta: "2026-11-30"));

        // Verificar
        Assert.Equal(new[] { alTerminar, alEmpezar }, resultado.Vista!.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task UnSoloDiaDejaTodoLoDeEseDiaYNadaMas()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 21, 23, 0));
        var temprano = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22, 0, 30));
        var tarde = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22, 22, 30));
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 23, 1, 0));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador,
            new FiltroAuditoria(Desde: "2026-11-22", Hasta: "2026-11-22"));

        // Verificar
        Assert.Equal(new[] { tarde, temprano }, resultado.Vista!.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task DosRangosDistintosDanResultadosDistintosSobreLosMismosRegistros()
    {
        // Preparar
        var octubre = entorno.Registrar(Entorno.HoraDelNegocio(2026, 10, 15));
        var noviembre = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 15));

        // Actuar
        var deOctubre = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Desde: "2026-10-01", Hasta: "2026-10-31"));
        var desdeNoviembre = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Desde: "2026-11-01"));
        var hastaOctubre = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Hasta: "2026-10-31"));

        // Verificar
        Assert.Equal(new[] { octubre }, deOctubre.Vista!.Registros.Select(r => r.Id));
        Assert.Equal(new[] { noviembre }, desdeNoviembre.Vista!.Registros.Select(r => r.Id));
        Assert.Equal(new[] { octubre }, hastaOctubre.Vista!.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task LosFiltrosSeCombinan()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20), Entorno.Ana, "SubirDocumento", "Documento", 1);
        var buscado = entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22), Entorno.Ana, "SubirDocumento", "Documento", 2);
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22), Entorno.Carla, "EliminarDocumento", "Documento", 2);
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 22), Entorno.Ana, "CambiarContrasena", "Usuario", 10);

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador,
            new FiltroAuditoria(Usuario: "ana@ejemplo.com", Entidad: "Documento", Desde: "2026-11-22", Hasta: "2026-11-22"));

        // Verificar
        Assert.Equal(new[] { buscado }, resultado.Vista!.Registros.Select(r => r.Id));
        Assert.Equal(1, resultado.Vista.Total);
    }

    // ---------- RD-07: un filtro inválido produce un rechazo controlado ----------

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData("abc", null, null, null, null, "El filtro usuario «abc» no es válido")]
    [InlineData("0", null, null, null, null, "El usuario «0» no existe")]
    [InlineData("99999999999", null, null, null, null, "El usuario «99999999999» no existe")]
    [InlineData("1,2", null, null, null, null, "El filtro usuario «1,2» no es válido")]
    [InlineData(null, "Producto", null, null, null, "La entidad «Producto» no existe en el historial. Usa una de estas: Documento, SolicitudPermiso, Usuario.")]
    [InlineData(null, null, "3", null, null, "Para filtrar por entidadId indica también la entidad")]
    [InlineData(null, "Documento", "3 OR 1=1", null, null, "El identificador «3 OR 1=1» no es válido")]
    [InlineData(null, null, null, "2026-13-01", null, "La fecha desde «2026-13-01» no es válida")]
    [InlineData(null, null, null, null, "ayer", "La fecha hasta «ayer» no es válida")]
    [InlineData(null, null, null, "22/11/2026", null, "La fecha desde «22/11/2026» no es válida")]
    [InlineData(null, null, null, "0001-01-01", null, "Las fechas deben estar entre 1900 y 2100.")]
    [InlineData(null, null, null, null, "9999-12-31", "Las fechas deben estar entre 1900 y 2100.")]
    [InlineData(null, null, null, "2026-11-30", "2026-11-01", "La fecha desde (2026-11-30) es posterior a hasta (2026-11-01).")]
    public async Task UnFiltroInvalidoSeRechazaConUnMensajeQueDiceQueCorregir(
        string? usuario, string? entidad, string? entidadId, string? desde, string? hasta, string mensaje)
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador,
            new FiltroAuditoria(usuario, entidad, entidadId, desde, hasta));

        // Verificar
        Assert.Null(resultado.Vista);
        Assert.Equal(FalloConsulta.FiltroInvalido, resultado.Fallo);
        Assert.StartsWith(mensaje, resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-07")]
    public async Task UnFiltroValidoSinCoincidenciasDaUnaListaVaciaYNoUnError()
    {
        // Preparar
        entorno.Registrar(Entorno.HoraDelNegocio(2026, 11, 20));

        // Actuar
        var resultado = await entorno.Consulta().ConsultarAsync(Entorno.Administrador, new FiltroAuditoria(Usuario: "nadie@ejemplo.com"));

        // Verificar
        Assert.Null(resultado.Fallo);
        Assert.Equal(0, resultado.Vista!.Total);
        Assert.Empty(resultado.Vista.Registros);
    }
}
