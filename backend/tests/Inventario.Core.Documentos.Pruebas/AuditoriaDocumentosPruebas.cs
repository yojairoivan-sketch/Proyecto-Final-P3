namespace Inventario.Core.Documentos.Pruebas;

/// <summary>
/// RF-DOC-06 y RF-AUD-03: toda subida y toda eliminación de un documento quedan en el historial, con quién, qué documento,
/// cuándo, y el valor anterior y el nuevo (RF-AUD-01). El registro se lee de la misma base.
/// </summary>
public sealed class AuditoriaDocumentosPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RF-AUD-03")]
    public async Task SubirDejaQuienLoSubioCuandoYQueAntesNoExistia()
    {
        // Preparar
        var ahora = entorno.Reloj.GetUtcNow();
        using var contenido = Entorno.Contenido("Factura 0042");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).SubirAsync("factura-0042.pdf", "application/pdf", contenido);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        Assert.Equal(Entorno.Ana.Id, registro.UsuarioId);
        Assert.Equal("ana@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Estándar", registro.UsuarioRol);
        Assert.Equal("SubirDocumento", registro.Accion);
        Assert.Equal("Documento", registro.Entidad);
        Assert.Equal(resultado.Valor!.Id.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal(entorno.Leer(resultado.Valor.Id).SubidoEn, registro.FechaHora);
        Assert.Equal("""{"estado":"No existía"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Vigente","nombre":"factura-0042.pdf","tipo":"application/pdf","tamanoBytes":12}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-03")]
    public async Task EliminarDejaQuienLoEliminoCuandoYQuePasoDeVigenteAEliminado()
    {
        // Preparar: Ana sube el documento y Carla (Administradora) lo elimina una hora después.
        var id = await entorno.SubirAsync(Entorno.Ana, "conteo.txt");
        entorno.Reloj.Advance(TimeSpan.FromHours(1));
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).EliminarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var historial = entorno.Historial();
        Assert.Equal(new[] { "EliminarDocumento", "SubirDocumento" }, historial.Select(r => r.Accion));
        var registro = historial[0];
        Assert.Equal(Entorno.Carla.Id, registro.UsuarioId);
        Assert.Equal("carla@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Administrador", registro.UsuarioRol);
        Assert.Equal("Documento", registro.Entidad);
        Assert.Equal(id.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal(entorno.Leer(id).EliminadoEn, registro.FechaHora);
        Assert.Equal("""{"estado":"Vigente","nombre":"conteo.txt"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Eliminado","nombre":"conteo.txt"}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-03")]
    public async Task UnaSubidaOUnaEliminacionRechazadaNoDejaRegistro()
    {
        // Preparar: un documento vigente de Ana y otro que Ana ya eliminó.
        var vigente = await entorno.SubirAsync(Entorno.Ana, "vigente.pdf");
        var yaEliminado = await entorno.SubirAsync(Entorno.Ana, "viejo.pdf");
        Assert.True((await entorno.Como(Entorno.Ana).EliminarAsync(yaEliminado)).Exito);
        var antes = entorno.Historial().Select(r => r.Id).ToList();
        using var vacio = Entorno.Contenido("");

        // Actuar: tres operaciones que se rechazan.
        var subidaVacia = await entorno.Como(Entorno.Beto).SubirAsync("vacio.txt", "text/plain", vacio);
        var ajeno = await entorno.Como(Entorno.Beto).EliminarAsync(vigente);
        var repetida = await entorno.Como(Entorno.Ana).EliminarAsync(yaEliminado);

        // Verificar: el historial quedó igual que antes.
        Assert.False(subidaVacia.Exito);
        Assert.False(ajeno.Exito);
        Assert.False(repetida.Exito);
        Assert.Equal(3, antes.Count);
        Assert.Equal(antes, entorno.Historial().Select(r => r.Id));
    }
}
