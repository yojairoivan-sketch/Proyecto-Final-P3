using System.Runtime.InteropServices;
using Inventario.Core.ColaCorreos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Enviador de la cola de correos (RF-NOT-09). Uso:
//   Inventario.Enviador              una pasada: envía los pendientes y termina
//   Inventario.Enviador --una-vez    lo mismo; sirve para `docker compose run --rm enviador --una-vez`,
//                                    que reemplaza el --vigilar 5 del servicio
//   Inventario.Enviador --vigilar N  repite la pasada cada N segundos (30 si no se indica)

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
// El fallo de una consulta ya se informa abajo con un mensaje corto; EF Core no lo repite con todo el detalle.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

var cadena = builder.Configuration.GetConnectionString("Inventario");
if (string.IsNullOrWhiteSpace(cadena))
    return Fallar("Falta la cadena de conexión (variable ConnectionStrings__Inventario).");

ConfiguracionSmtp smtp;
OpcionesCola opciones;
try
{
    // RF-NOT-13: las credenciales del servidor de correo salen solo de variables de entorno.
    smtp = ConfiguracionSmtp.DesdeVariables(nombre => builder.Configuration[nombre]);
    // RF-NOT-10: cuántas veces se intenta un correo antes de dejarlo Fallido.
    opciones = OpcionesCola.DesdeVariable(builder.Configuration[OpcionesCola.VariableMaximoIntentos]);
}
catch (InvalidOperationException ex)
{
    return Fallar(ex.Message);
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddColaCorreos(cadena);
builder.Services.AddProcesadorCola(smtp, opciones);

using var host = builder.Build();

var posicion = Array.IndexOf(args, "--vigilar");
var vigilar = posicion >= 0;
var segundos = vigilar && posicion + 1 < args.Length && int.TryParse(args[posicion + 1], out var n) && n > 0 ? n : 30;

using var cancelar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelar.Cancel();
};
// docker compose stop / down mandan SIGTERM: se termina la pasada en curso y se sale sin esperar a que lo maten.
using var alTerminar = PosixSignalRegistration.Create(PosixSignal.SIGTERM, contexto =>
{
    contexto.Cancel = true;
    cancelar.Cancel();
});

Console.WriteLine($"Servidor SMTP: {smtp}. Máximo de intentos por correo: {opciones.MaximoIntentos}.");

try
{
    do
    {
        try
        {
            await using var alcance = host.Services.CreateAsyncScope();
            var procesador = alcance.ServiceProvider.GetRequiredService<ProcesadorCola>();
            var resumen = await procesador.ProcesarPendientesAsync(cancelar.Token);
            // Vigilando, solo se escribe cuando hubo algo que enviar: si no, el log se llenaría cada pocos segundos.
            if (!vigilar || resumen is not { Enviados: 0, ConError: 0, Fallidos: 0 })
                Console.WriteLine(resumen is { Enviados: 0, ConError: 0, Fallidos: 0 }
                    ? $"[{Hora()}] No hay correos pendientes."
                    : $"[{Hora()}] Enviados: {resumen.Enviados}. Con error (se reintentan): {resumen.ConError}. " +
                      $"Fallidos (agotaron los {opciones.MaximoIntentos} intentos): {resumen.Fallidos}.");
        }
        catch (Exception ex) when (vigilar && ex is not OperationCanceledException)
        {
            // Por ejemplo, la base todavía no tiene la tabla de la cola porque la API no terminó de arrancar.
            // Vigilando, se avisa y se reintenta en la próxima pasada en vez de terminar el proceso.
            Console.Error.WriteLine($"[{Hora()}] No se pudo procesar la cola: {ex.Message} Se reintenta en {segundos} s.");
        }

        if (vigilar)
            await Task.Delay(TimeSpan.FromSeconds(segundos), cancelar.Token);
    }
    while (vigilar);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Enviador detenido.");
}
catch (Exception ex)
{
    return Fallar($"No se pudo procesar la cola: {ex.Message}");
}

return 0;

static string Hora() => $"{TimeProvider.System.GetUtcNow():HH:mm:ss} UTC";

static int Fallar(string mensaje)
{
    Console.Error.WriteLine(mensaje);
    return 1;
}
