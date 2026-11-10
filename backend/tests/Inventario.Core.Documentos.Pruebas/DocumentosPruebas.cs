using Inventario.Core.ControlAcceso;

namespace Inventario.Core.Documentos.Pruebas;

/// <summary>
/// RF-DOC-01 a RF-DOC-04, RD-06 y RD-07 a través de IServicioDocumentos, como lo usa la API, con Control de acceso
/// reemplazado por un doble. Lo guardado se lee con un contexto nuevo y de la carpeta del almacén, no de la memoria del servicio.
/// </summary>
public sealed class DocumentosPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    // ---------- RF-DOC-01: subir registra nombre, tipo, tamaño, fecha y quién ----------

    [Fact]
    [Trait("Requisito", "RF-DOC-01")]
    public async Task SubirRegistraNombreTipoTamanoFechaYQuienLoSubio()
    {
        // Preparar
        var ahora = entorno.Reloj.GetUtcNow();
        using var contenido = Entorno.Contenido("Factura 0042: 12 cajas de agua");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("factura-0042.pdf", "application/pdf", contenido);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = entorno.Leer(resultado.Valor!.Id);
        Assert.Equal("factura-0042.pdf", guardado.Nombre);
        Assert.Equal("application/pdf", guardado.Tipo);
        Assert.Equal(contenido.Length, guardado.TamanoBytes);
        Assert.Equal(ahora, guardado.SubidoEn);
        Assert.Equal(Entorno.Ana.Id, guardado.SubidoPorId);
        Assert.Equal(Entorno.Ana.Nombre, guardado.SubidoPorNombre);
        Assert.Null(guardado.EliminadoEn);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-01")]
    public async Task ElContenidoSubidoQuedaEnElAlmacen()
    {
        // Preparar
        using var contenido = Entorno.Contenido("Conteo físico del lunes");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("conteo.txt", "text/plain", contenido);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = entorno.Leer(resultado.Valor!.Id);
        Assert.Equal("Conteo físico del lunes", await File.ReadAllTextAsync(Path.Combine(entorno.Opciones.Carpeta, guardado.Archivo)));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-01")]
    public async Task ElRegistroSeConsultaConTodosSusDatos()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "lista-precios.pdf", "precios");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).ConsultarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new DocumentoVista(id, "lista-precios.pdf", "application/pdf", "precios".Length,
            entorno.Reloj.GetUtcNow(), Entorno.Ana.Id, Entorno.Ana.Nombre, EliminadoEn: null), resultado.Valor);
    }

    [Theory]
    [Trait("Requisito", "RF-DOC-01")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\Users\ana\Desktop\recibo.png", "recibo.png")]
    [InlineData("  ", "documento")]
    [InlineData(null, "documento")]
    public async Task ElNombreGuardadoNoTraeCarpetas(string? nombre, string esperado)
    {
        // Preparar
        using var contenido = Entorno.Contenido("x");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync(nombre, "image/png", contenido);

        // Verificar: el nombre es solo el del archivo, y en el almacén se guarda con uno que genera la pieza.
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = entorno.Leer(resultado.Valor!.Id);
        Assert.Equal(esperado, guardado.Nombre);
        Assert.Matches("^[0-9a-f]{32}$", guardado.Archivo);
    }

    [Theory]
    [Trait("Requisito", "RF-DOC-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no es un tipo")]
    [InlineData("text/html; <script>")]
    public async Task UnTipoSinFormaValidaSeGuardaComoGenerico(string? tipo)
    {
        // Preparar
        using var contenido = Entorno.Contenido("x");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("datos.bin", tipo, contenido);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal("application/octet-stream", entorno.Leer(resultado.Valor!.Id).Tipo);
    }

    // ---------- RD-07: un archivo ausente, vacío o demasiado grande se rechaza sin guardar nada ----------

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task SubirSinArchivoSeRechazaSinGuardarNada()
    {
        // Preparar
        var servicio = entorno.Como(Entorno.Ana);

        // Actuar
        var resultado = await servicio.SubirAsync("factura.pdf", "application/pdf", contenido: null);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.ContarRegistros());
        Assert.Equal(0, entorno.ContarArchivos());
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task SubirUnArchivoVacioSeRechazaSinGuardarNada()
    {
        // Preparar
        using var vacio = new MemoryStream();

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("vacio.pdf", "application/pdf", vacio);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Contains("vacío", resultado.Mensaje);
        Assert.Equal(0, entorno.ContarRegistros());
        Assert.Equal(0, entorno.ContarArchivos());
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task SubirUnArchivoQuePasaDelMaximoSeRechazaSinGuardarNada()
    {
        // Preparar: el entorno admite 1 KB; este archivo tiene un byte más.
        using var grande = new MemoryStream(new byte[entorno.Opciones.TamanoMaximoBytes + 1]);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("grande.zip", "application/zip", grande);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.ContarRegistros());
        Assert.Equal(0, entorno.ContarArchivos());
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task UnArchivoDelTamanoMaximoSeAcepta()
    {
        // Preparar
        using var justo = new MemoryStream(new byte[entorno.Opciones.TamanoMaximoBytes]);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("justo.bin", "application/octet-stream", justo);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(entorno.Opciones.TamanoMaximoBytes, entorno.Leer(resultado.Valor!.Id).TamanoBytes);
    }

    // ---------- RF-DOC-02: el listado no trae eliminados ni ajenos, salvo para el Administrador ----------

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task UnEstandarSoloVeSusDocumentos()
    {
        // Preparar
        var deAna = await entorno.SubirAsync(Entorno.Ana, "de-ana.pdf");
        await entorno.SubirAsync(Entorno.Beto, "de-beto.pdf");
        await entorno.SubirAsync(Entorno.Carla, "de-carla.pdf");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).ListarAsync();

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new[] { deAna }, resultado.Valor!.Select(d => d.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task ElListadoNoIncluyeLosEliminados()
    {
        // Preparar
        var queda = await entorno.SubirAsync(Entorno.Ana, "queda.pdf");
        var eliminado = await entorno.SubirAsync(Entorno.Ana, "se-va.pdf");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(eliminado)).Exito);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).ListarAsync();

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new[] { queda }, resultado.Valor!.Select(d => d.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task ElAdministradorVeLosDeTodosSinLosEliminados()
    {
        // Preparar
        var deAna = await entorno.SubirAsync(Entorno.Ana, "de-ana.pdf");
        var deBeto = await entorno.SubirAsync(Entorno.Beto, "de-beto.pdf");
        var eliminado = await entorno.SubirAsync(Entorno.Beto, "borrado.pdf");
        Assert.True((await entorno.Como(Entorno.Beto).EliminarAsync(eliminado)).Exito);

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).ListarAsync();

        // Verificar: los más nuevos primero.
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new[] { deBeto, deAna }, resultado.Valor!.Select(d => d.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task ElAdministradorPuedePedirTambienLosEliminados()
    {
        // Preparar
        var queda = await entorno.SubirAsync(Entorno.Ana, "queda.pdf");
        var eliminado = await entorno.SubirAsync(Entorno.Ana, "se-va.pdf");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(eliminado)).Exito);

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).ListarAsync(incluirEliminados: true);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new[] { eliminado, queda }, resultado.Valor!.Select(d => d.Id));
        Assert.NotNull(resultado.Valor!.Single(d => d.Id == eliminado).EliminadoEn);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task UnEstandarNoPuedePedirLosEliminados()
    {
        // Preparar
        var eliminado = await entorno.SubirAsync(Entorno.Ana, "se-va.pdf");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(eliminado)).Exito);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).ListarAsync(incluirEliminados: true);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
    }

    // ---------- RF-DOC-03 y RD-06: solo el dueño o un Administrador descargan ----------

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task ElDuenoDescargaElMismoContenidoQueSubio()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "pedido.txt", "3 sacos de arroz");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).DescargarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal("pedido.txt", resultado.Valor!.Documento.Nombre);
        Assert.Equal("3 sacos de arroz", await Entorno.LeerTodoAsync(resultado.Valor.Contenido));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task UnAdministradorDescargaElDocumentoDeOtro()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "pedido.txt", "3 sacos de arroz");

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).DescargarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal("3 sacos de arroz", await Entorno.LeerTodoAsync(resultado.Valor!.Contenido));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    [Trait("Requisito", "RD-06")]
    public async Task OtroEstandarRecibeUnRechazoAlDescargar()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "pedido.txt");

        // Actuar: Beto llama a la operación directamente, sin pasar por ninguna pantalla.
        var resultado = await entorno.Como(Entorno.Beto).DescargarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    [Trait("Requisito", "RD-06")]
    public async Task OtroEstandarRecibeUnRechazoAlConsultarElRegistro()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "pedido.txt");

        // Actuar
        var resultado = await entorno.Como(Entorno.Beto).ConsultarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task UnDocumentoEliminadoYaNoSeDescarga()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "pedido.txt");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(id)).Exito);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).DescargarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task DescargarUnDocumentoQueNoExisteSeRechaza()
    {
        // Preparar
        var servicio = entorno.Como(Entorno.Carla);

        // Actuar
        var resultado = await servicio.DescargarAsync(999);

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
    }

    // ---------- RF-DOC-04: la eliminación es lógica; solo el dueño o un Administrador ----------

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task EliminarEsLogicoElRegistroYElArchivoSiguenGuardados()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");
        var ahora = entorno.Reloj.GetUtcNow().AddHours(1);
        entorno.Reloj.SetUtcNow(ahora);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).EliminarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = entorno.Leer(id);
        Assert.Equal(ahora, guardado.EliminadoEn);
        Assert.Equal(Entorno.Ana.Id, guardado.EliminadoPorId);
        Assert.Equal("recibo.pdf", guardado.Nombre);
        Assert.True(entorno.Almacen.Existe(guardado.Archivo));
        Assert.Equal(1, entorno.ContarRegistros());
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    [Trait("Requisito", "RD-06")]
    public async Task OtroEstandarRecibeUnRechazoAlEliminarYElDocumentoSigue()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");

        // Actuar
        var resultado = await entorno.Como(Entorno.Beto).EliminarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Null(entorno.Leer(id).EliminadoEn);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task UnAdministradorEliminaElDocumentoDeOtro()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).EliminarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Entorno.Carla.Id, entorno.Leer(id).EliminadoPorId);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task EliminarDosVecesElMismoDocumentoSeRechazaLaSegunda()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(id)).Exito);
        var primeraEliminacion = entorno.Leer(id).EliminadoEn;
        entorno.Reloj.Advance(TimeSpan.FromMinutes(5));

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).EliminarAsync(id);

        // Verificar: no cambia la fecha de la primera eliminación.
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
        Assert.Equal(primeraEliminacion, entorno.Leer(id).EliminadoEn);
    }

    // ---------- Todas las operaciones exigen una sesión válida ----------

    [Fact]
    [Trait("Requisito", "RD-06")]
    public async Task SinSesionNoSeSubeNada()
    {
        // Preparar
        using var contenido = Entorno.Contenido("x");

        // Actuar
        var resultado = await entorno.Como(null).SubirAsync("factura.pdf", "application/pdf", contenido);

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
        Assert.Equal(0, entorno.ContarRegistros());
        Assert.Equal(0, entorno.ContarArchivos());
    }

    [Theory]
    [Trait("Requisito", "RD-06")]
    [InlineData("Listar")]
    [InlineData("Consultar")]
    [InlineData("Descargar")]
    public async Task SinSesionNoSeLeeNingunDocumento(string operacion)
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");
        var sinSesion = entorno.Como(null);

        // Actuar
        Resultado resultado = operacion switch
        {
            "Listar" => await sinSesion.ListarAsync(),
            "Consultar" => await sinSesion.ConsultarAsync(id),
            _ => await sinSesion.DescargarAsync(id),
        };

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RD-06")]
    public async Task SinSesionNoSeElimina()
    {
        // Preparar
        var id = await entorno.SubirAsync(Entorno.Ana, "recibo.pdf");

        // Actuar
        var resultado = await entorno.Como(null).EliminarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
        Assert.Null(entorno.Leer(id).EliminadoEn);
    }
}
