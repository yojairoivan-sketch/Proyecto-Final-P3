# Bitácora de delegación — Asignación 3 (auditoría de la Práctica 5)

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez (`yojairoivan-sketch`) |
| Fecha de la auditoría | 2026-11-21 (sábado) |
| Qué se auditó | Lo que el agente construyó en la Práctica 5: notificaciones, cola de correos y reportes (etiqueta `practica-5` = `fc677a6`) |
| Contra qué | `docs/criterios-notificaciones-reportes.md` tal como estaba en `practica-5`, RF-NOT-01 a 13, RF-REP-01 a 05, RD-02, 03, 06, 07, 08, 10 y 11, y los casos que esos criterios no cubrían |
| Cómo | Un clon aparte (`cp .env.example .env`, `docker compose up --build -d`) con `practica-5`, provocando cada caso con `curl`. Los comandos están en [`evidencias/asignacion-3/reproducir.sh`](evidencias/asignacion-3/reproducir.sh) y lo que devolvieron en los archivos `h*-antes.txt` / `h*-despues.txt` de esa carpeta. |

## Resumen

| # | Hallazgo | Pieza | Incumple | Gravedad | Commit |
|---|---|---|---|---|---|
| 1 | Un destinatario inválido gasta los 5 intentos con el mismo error | Cola de correos | RF-NOT-10, RF-NOT-11 | Media | `f179fc1` |
| 2 | Dos solicitudes simultáneas del mismo permiso crean dos y avisan dos veces | Notificaciones / Gestión de permisos | RF-NOT-01 (N-01), RF-GP-01 | Media | `2faf5fb` |
| 3 | Los reportes cortan los días a las 00:00 UTC (20:00 en Santo Domingo) | Reportes | RF-REP-05, RF-REP-02, RD-11 (criterio R-06 equivocado) | Media | `43cda63` |
| 4 | Dos cambios simultáneos de preferencias responden 500 | Notificaciones | RF-NOT-07, RD-07, RD-08 | Baja | `0928331` |

Los cuatro están corregidos, cada uno en su propio commit, y cada uno tiene una prueba nueva que cita el hallazgo en el
nombre (`Hallazgo1_…` a `Hallazgo4_…`) y en un comentario. Cada prueba falla sobre el commit anterior a su corrección y pasa
sobre la corrección: [`evidencias/asignacion-3/antes-y-despues.sh`](evidencias/asignacion-3/antes-y-despues.sh) lo comprueba
y su salida está en [`antes-y-despues.txt`](evidencias/asignacion-3/antes-y-despues.txt). Con las cuatro correcciones, el
proyecto levanta con el comando del README sobre una base vacía, la revisión acumulada de seis frentes da 67/67 (y 5/5 después
de `docker compose down` + `up`), el recorrido de la Práctica 5 da 73/73 y el comando de «Pruebas» del README, desde un clon
limpio, pasa las 375 pruebas de las ocho suites ([`seis-frentes.txt`](evidencias/asignacion-3/seis-frentes.txt),
[`seis-frentes-tras-reinicio.txt`](evidencias/asignacion-3/seis-frentes-tras-reinicio.txt),
[`e2e-p5.txt`](evidencias/asignacion-3/e2e-p5.txt), [`comando-pruebas.txt`](evidencias/asignacion-3/comando-pruebas.txt)).

---

## Hallazgo 1 — Un destinatario inválido gasta los 5 intentos con el mismo error

**Gravedad: media.** La persona queda registrada, recibe «Te enviamos un correo», pero el correo nunca puede salir; la cola
lo reintenta cinco veces como si fuera un fallo pasajero.

**Qué se pidió.** En `AGENTS.md`: «completar la cola de correos (RF-NOT-10 y 11)». El criterio C-03 decía: «Mailpit detenido
y un correo `Pendiente` → cada pasada suma 1 a `intentos` y guarda `ultimo_error`; al llegar al máximo … queda `Fallido`».
RF-NOT-10: «Un correo que falla se reintenta un número limitado de veces y luego queda marcado como fallido».

**Qué entregó el agente.** Un reintento igual para cualquier error. El registro valida el correo con `System.Net.Mail` y una
expresión regular, pero el transporte usa MimeKit (`MailboxAddress.Parse`), que es más estricto. `prueba.@ejemplo.com` pasa lo
primero y no lo segundo. Evidencia ([`h1-antes.txt`](evidencias/asignacion-3/h1-antes.txt), `reproducir.sh 1` sobre `practica-5`):

```
$ registro con prueba1795275144.@ejemplo.com
{"mensaje":"Cuenta creada. Te enviamos un correo con el enlace para activarla."} [201]
$ a los 15s: GET /api/cola-correos
{… "estado":"Pendiente","intentos":3, … "ultimoError":"ParseException: Invalid local-part at offset 0"}
$ a los 45s: GET /api/cola-correos
{… "estado":"Fallido","intentos":5, … "ultimoError":"ParseException: Invalid local-part at offset 0"}
```

Cinco intentos, los cinco con el mismo error, que no depende de nada externo. Se comparó qué direcciones aceptan los dos
validadores con un script de `dotnet fsi` (23 direcciones): `prueba.@ejemplo.com` es la que el registro acepta y MimeKit no.

**Por qué pasó.** *Criterio ausente.* C-03 solo pensaba en el servidor caído, que es un error pasajero; no había ningún
criterio para un error permanente, y el enunciado de la asignación nombraba justo este caso («un correo con destinatario
inválido») como uno que mis criterios no cubrían. El agente cumplió C-03 al pie de la letra.

**Corrección.** La hizo el agente, en una sesión nueva, con esta instrucción (resumida): «audita lo que el agente construyó
en la Práctica 5 contra los criterios y los casos no cubiertos; los hallazgos deben ser reales, encontrados ejecutando el
proyecto; pista a comprobar: un destinatario inválido falla `MailboxAddress.Parse` y se reintenta 5 veces igual; un commit
por hallazgo que cite su número y una prueba xUnit que falle antes y pase después». Commit `f179fc1`: la cola comprueba la
dirección al encolar (`CorreoEnCola.DestinatarioEntregable`, con el mismo MimeKit que usa el transporte); si no se puede leer,
el correo queda `Fallido` con `intentos = 0` y el motivo. El procesador hace la misma comprobación con los Pendientes que ya
estuvieran en la cola, así la regla no queda solo en un lugar. Prueba:
`ColaCorreosPruebas.Hallazgo1_UnCorreoConDestinatarioInvalidoQuedaFallidoSinGastarIntentos`. Después
([`h1-despues.txt`](evidencias/asignacion-3/h1-despues.txt)):

```
{… "estado":"Fallido","intentos":0, … "ultimoError":"El destinatario «prueba1795275641.@ejemplo.com» no es una dirección de correo válida. No se intentará enviar."}
```

Queda sin cambiar, a propósito: el registro (Control de acceso, Práctica 1) sigue aceptando esa dirección. No es código de la
Práctica 5 y la cola ya no la reintenta; el Administrador la ve como Fallida en `GET /api/cola-correos`.

**Qué cambias.** Criterio nuevo **C-09** en `docs/criterios-notificaciones-reportes.md` (destinatario inválido → Fallido
enseguida, sin intentos). En `AGENTS.md`, convención «Cola de correos»: solo se reintenta lo que puede salir bien después, y en
«Antes de dar algo por terminado», probar un correo para una dirección rara.

---

## Hallazgo 2 — Dos solicitudes simultáneas del mismo permiso crean dos y avisan dos veces

**Gravedad: media.** Se rompe la regla «una solicitud sin resolver por permiso» y cada Administrador recibe el aviso y el
correo dos veces. Basta con un doble clic o un reintento del navegador.

**Qué se pidió.** Criterio N-01: «E1 crea una solicitud de permiso → A1 y A2 tienen cada uno **una** notificación nueva».
`AGENTS.md` le pedía no tocar las reglas ya entregadas de Gestión de permisos y solo **agregar** el aviso después de guardar.

**Qué entregó el agente.** El aviso, bien puesto después del `SaveChanges`, sobre una comprobación de duplicado que es una
consulta seguida de un `INSERT`, sin nada en la base que lo impida. Evidencia ([`h2-antes.txt`](evidencias/asignacion-3/h2-antes.txt),
`reproducir.sh 2` sobre `practica-5`; `curl -Z` manda las dos peticiones a la vez):

```
$ curl -Z: dos POST /api/solicitudes {"permiso":"ListarUsuarios"} a la vez
201
{"mensaje":"Solicitud creada. …","solicitud":{"id":25, …}}201
$ GET /api/solicitudes/mias (id y estado de cada una)
"id":24 "estado":"Pendiente" "id":25 "estado":"Pendiente"
$ avisos «Nueva solicitud» del Administrador por sim1795275132@ejemplo.com
2
$ correos nuevos en la cola para el Administrador por estas dos peticiones
2
```

Con `curl -Z` pasó en 8 de 8 usuarios distintos (en la base, cada par creado con 13 µs a 1.4 ms de diferencia). Con dos `curl`
en segundo plano pasó 1 de 7 veces: por eso no salía en el recorrido de la Práctica 5, que manda las peticiones una tras otra.

**Por qué pasó.** *Criterio ausente y revisión insuficiente.* Ningún criterio pedía dos peticiones a la vez (el enunciado lo
nombra como caso no cubierto), y en la Práctica 5 revisé todo con peticiones en secuencia. La comprobación venía de la
Práctica 2, y `AGENTS.md` le prohibía al agente cambiarla, así que tampoco la iba a cuestionar.

**Corrección.** La hizo el agente con la misma instrucción del hallazgo 1 (pista: «dos creaciones simultáneas de la misma
solicitud podrían pasar la comprobación de duplicado»). Commit `2faf5fb`: índice único parcial en
`gestion_permisos.solicitudes_permiso (solicitante_id, permiso) WHERE estado <> 'Rechazada'` (migración
`UnaSolicitudVivaPorPermiso`) y, si el guardado choca con él, el servicio responde el mismo 409 de siempre y no avisa. Prueba:
`AvisosPruebas.Hallazgo2_DosSolicitudesSimultaneasDelMismoPermisoCreanUnaSolaYAvisanUnaVez`, que hace guardar otra solicitud
igual justo entre la comprobación y el guardado (un interceptor de EF), sin levantar la API. Después
([`h2-despues.txt`](evidencias/asignacion-3/h2-despues.txt)), seis veces seguidas: una 201 y una 409, una solicitud, un aviso,
un correo; en la base, ningún par repetido.

**Qué cambias.** Criterio nuevo **N-14** (dos `POST` a la vez → 201 + 409, una solicitud, un aviso por Administrador). En
`AGENTS.md`, convención «Concurrencia» (las reglas «no puede haber dos» las garantiza la base) y el caso «dos peticiones iguales
a la vez con `curl -Z`» en la lista de antes de terminar.

---

## Hallazgo 3 — Los reportes cortan los días a las 00:00 UTC

**Gravedad: media.** El reporte del negocio responde «¿cuánto le compramos a cada proveedor en el período?», y lo que se
recibe de 20:00 a 24:00 en Santo Domingo cae en el día (o el mes) siguiente. No se pierde nada, pero los totales por mes salen
mal, que es justo la pregunta del reporte.

**Qué se pidió.** Criterio R-06: «`desde` y `hasta` son fechas `AAAA-MM-DD` **en UTC** y los dos días se incluyen
completos». En `AGENTS.md`: «Todas las fechas en UTC (RD-11)».

**Qué entregó el agente.** Exactamente eso: `RangoFechas` va de `desde` 00:00 UTC al día siguiente de `hasta` 00:00 UTC, y la
pantalla `/reportes` toma «hoy» con `new Date().toISOString().slice(0, 10)` y rotula «Desde (UTC)». Evidencia
([`h3-antes.txt`](evidencias/asignacion-3/h3-antes.txt), `reproducir.sh 3` sobre `practica-5`). La orden se recibe por la API y
solo se mueve su hora de recepción a las 22:30 del 31 de octubre en Santo Domingo (es la única escritura directa en la base,
para no esperar a la noche):

```
$ GET /api/ordenes/2 (recibidaEn)
"recibidaEn":"2026-11-01T02:30:00+00:00"
$ GET /api/reportes/compras-por-proveedor?desde=2026-10-01&hasta=2026-10-31
(Molinos 1795275142 no aparece)
$ GET /api/reportes/compras-por-proveedor?desde=2026-11-01&hasta=2026-11-30
{"proveedorId":2,"proveedor":"Molinos 1795275142","ordenes":1,"montoTotal":8500,"promedioPorOrden":8500}
$ node: expresión de Reportes.tsx evaluada el 30/11/2026 a las 21:00 en Santo Domingo
desde por defecto: 2026-12-01 | hasta por defecto: 2026-12-01
```

La bandeja de notificaciones, en cambio, muestra las horas en la hora local del navegador: el mismo instante se ve como
«31 de octubre» en una pantalla y cuenta como «noviembre» en el reporte (RD-11: el mismo criterio en todo el sistema).

**Por qué pasó.** *Criterio equivocado.* El defecto está en mi criterio R-06, no en cómo lo siguió el agente: confundí cómo se
**guardan** las fechas (UTC, bien) con cómo se **interpreta un día** que escribe el usuario. Y en la revisión todas las
pruebas usaron horas de la mañana o el mediodía.

**Corrección.** La hizo el agente con la misma instrucción del hallazgo 1. Commit `43cda63`: `RangoFechas` interpreta `desde` y
`hasta` como días de Santo Domingo (UTC−4, sin horario de verano) y devuelve los límites en UTC. Los dos reportes lo usan, así
que la causa se corrige en un solo lugar; la pantalla calcula «hoy» con `timeZone: 'America/Santo_Domingo'`. Tres pruebas que
fijaban los límites en UTC pasan a los de Santo Domingo con la misma exigencia (por ejemplo, 03:59 UTC del 1 de diciembre
cuenta en noviembre y 04:00 ya no). Prueba nueva: `ReporteComprasPruebas.Hallazgo3_UnaOrdenRecibidaLaNocheDelUltimoDiaCuentaEnEseDiaDelNegocio`.
Después ([`h3-despues.txt`](evidencias/asignacion-3/h3-despues.txt)): la orden sale en octubre y no en noviembre.

**Qué cambias.** El criterio **R-06** ahora dice «calendario del negocio (hora de Santo Domingo, UTC−4)», con una nota de por
qué cambió, y hay un criterio nuevo **R-09** (la orden de las 22:30 del 31 de octubre cuenta en octubre; la pantalla propone
el mes correcto el 30 de noviembre a las 21:00). En `AGENTS.md`, la convención de fechas distingue guardar (UTC) de un día que
el usuario escribe o lee, y la lista de antes de terminar incluye «el último día del mes después de las 20:00».

---

## Hallazgo 4 — Dos cambios simultáneos de preferencias responden 500

**Gravedad: baja.** Hace falta mandar dos cambios a la vez, y la preferencia queda bien guardada; pero el usuario ve
«Ocurrió un error inesperado» (RD-07: un rechazo controlado, no una excepción sin manejar).

**Qué se pidió.** Criterio N-11 (RF-NOT-07): `PUT /api/notificaciones/preferencias` con `{"correo": false}` desactiva el canal
de correo. `AGENTS.md`: «Entradas inválidas dan 400 con mensaje, nunca 500».

**Qué entregó el agente.** El mismo patrón del hallazgo 2, ahora en código suyo: busca la fila del usuario y, si no está, la
agrega; la clave de la tabla es el usuario. Lo encontré al revisar si la causa del hallazgo 2 se repetía en otro lugar, como
pide la asignación. Evidencia ([`h4-antes.txt`](evidencias/asignacion-3/h4-antes.txt), `reproducir.sh 4` sobre `practica-5`;
antes, tres veces de tres sobre el commit `43cda63`, que no cambia ese archivo):

```
$ curl -Z: dos PUT /api/notificaciones/preferencias {"correo":false} a la vez
{"mensaje":"Recibirás tus notificaciones solo dentro del sistema. …","correo":false} [200]
{"title":"Error del servidor","status":500,"detail":"Ocurrió un error inesperado. Intenta de nuevo más tarde."} [500]
$ docker compose logs api
---> Npgsql.PostgresException (0x80004005): 23505: duplicate key value violates unique constraint "pk_preferencias"
```

El mensaje al usuario no muestra la traza (RD-08 se cumple); el problema es el 500.

**Por qué pasó.** *Criterio ausente*, el mismo que en el hallazgo 2: ningún criterio probaba dos peticiones a la vez, y
`AGENTS.md` no decía nada de concurrencia, así que el agente escribió «consultar y luego insertar» también en su código nuevo.

**Corrección.** La hizo el agente, a partir de su propia revisión de la causa del hallazgo 2 (la instrucción decía «corregir
la causa; si el agente hizo mal una regla en un lugar, revisa si la repitió en otro»). Commit `0928331`: si el guardado choca
con la clave, el servicio vuelve a leer la fila que creó la otra petición y la cambia; responde 200 y queda lo que pidió la
última. Prueba: `NotificacionesPruebas.Hallazgo4_DosCambiosSimultaneosDePreferenciasNoFallanYQuedaElUltimo`. Después
([`h4-despues.txt`](evidencias/asignacion-3/h4-despues.txt)): las dos responden 200.

**Qué cambias.** Criterio nuevo **N-15** (dos `PUT` a la vez → 200 los dos, nunca 500). La convención «Concurrencia» de
`AGENTS.md` cubre también «crear si no existe».

---

## Casos revisados sin hallazgo

| Caso | Qué se hizo | Qué se vio |
|---|---|---|
| Sin ningún Administrador activo | Se revisó cómo se puede llegar ahí: `ServicioUsuarios` | No se puede por la API: un Administrador no puede desactivarse a sí mismo y al último no se le puede quitar el rol. Si pasara, `NotificarAdministradoresAsync` no avisa a nadie y la solicitud sigue en las pendientes. |
| Entradas vacías y fuera de rango en los reportes | [`casos-revisados.txt`](evidencias/asignacion-3/casos-revisados.txt) | 400 con un mensaje que dice qué corregir para fechas vacías, `2026-02-30`, `9999-12-31`, `desde` > `hasta` y más de 731 días. |
| Rango de fechas sin datos | Igual | 200 con `total: 0` y listas vacías; la pantalla dice «No hay solicitudes en ese rango». |
| Marcar la notificación de otro (RF-NOT-05, RD-06) | Recorrido de la Práctica 5 ([`e2e-p5.txt`](evidencias/asignacion-3/e2e-p5.txt)) | 404 «La notificación no existe», también para un Administrador. |
| Mailpit caído (RF-NOT-08) y procesador dos veces (RF-NOT-12) | Igual | La operación responde enseguida; los correos se reintentan, quedan Fallidos a los 5 intentos y nada se envía dos veces. |
| Credenciales en el repositorio (RD-10, RF-NOT-13) | `git log --all -p` buscando, sin mostrarlas, la contraseña SMTP, la clave de ExchangeRate-API y la contraseña de PostgreSQL del `.env` de desarrollo | 0 apariciones de cada una; `.env` no está en ningún commit. |

## Cómo se verificaron las correcciones antes de aceptarlas

- Cada prueba nueva, sobre el commit anterior a su corrección (con solo los archivos de prueba del commit) y sobre la
  corrección: falla y pasa ([`antes-y-despues.txt`](evidencias/asignacion-3/antes-y-despues.txt)).
- Cada hallazgo, reproducido otra vez con `reproducir.sh` sobre un clon de la rama con las correcciones y la base vacía:
  ya no aparece (archivos `h*-despues.txt`).
- El comando de la sección «Pruebas» del README, desde un clon limpio de la rama: 375 pruebas en ocho suites, salida 0.
- El proyecto levantado con el comando del README sobre una base vacía: seis frentes 67/67, 5/5 tras `down` + `up`, y el
  recorrido de la Práctica 5 73/73. Una primera corrida del recorrido dio 72/73 porque esa base todavía tenía la orden que dejó
  la reproducción del hallazgo 3 en octubre (el recorrido espera octubre vacío); sobre una base vacía pasa completo.
