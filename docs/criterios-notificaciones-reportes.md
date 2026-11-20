# Criterios de aceptación — Notificaciones, cola de correos y reportes (Práctica 5)

Escritos **antes** de delegar. El trabajo del agente se acepta solo si cada criterio se cumple. Cada uno se
comprueba con la API (`http://localhost:8080/api`, con la sesión en `Authorization: Bearer <token>`), con
Mailpit (`/correo/`) o con la base (`docker compose exec db psql -U inventario -d inventario`).

Usuarios de referencia: **A1** y **A2** son Administradores activos; **E1** y **E2** son Estándar activos;
**AX** es un Administrador desactivado.

## Notificaciones (pieza 4)

| # | Requisito | Dado | Cuando | Entonces |
|---|---|---|---|---|
| N-01 | RF-NOT-01 | A1, A2, AX, E1 y E2 | E1 crea una solicitud de permiso | A1 y A2 tienen cada uno **una** notificación nueva sin leer que nombra a E1 y el permiso; AX, E1 y E2 no tienen ninguna nueva. |
| N-02 | RF-NOT-02 | una solicitud Pendiente de E1 | A1 la aprueba | E1 tiene una notificación nueva sin leer que dice «aprobada» y el permiso; A1, A2 y E2 no reciben ninguna por esto. |
| N-03 | RF-NOT-02 | una solicitud Pendiente de E1 | A1 la rechaza con el motivo «No aplica» | E1 tiene una notificación nueva que dice «rechazada» e incluye el texto «No aplica». |
| N-04 | RF-NOT-02 | una solicitud que A1 intenta resolver | la resolución se rechaza (por ejemplo, rechazar sin motivo, o una transición fuera de la tabla) | nadie recibe una notificación nueva. |
| N-05 | RF-NOT-03 | una orden de compra en estado Enviada | E1 la pasa a Recibida | A1 y A2 tienen una notificación nueva que dice el número de la orden, el proveedor y que se recibió; E1 y E2 no. Los pasos para provocarlo están en la sección «Notificaciones» del README. |
| N-06 | RF-NOT-04 | E1 tiene 2 notificaciones y E2 tiene 1 | E1 consulta `GET /api/notificaciones` | recibe exactamente sus 2, cada una con `leida` (true/false); ninguna es de E2. Con `?estado=NoLeidas` solo vienen las no leídas y con `?estado=Leidas` solo las leídas. |
| N-07 | RF-NOT-04 | — | se consulta `GET /api/notificaciones` sin sesión | responde 401 y no devuelve datos. |
| N-08 | RF-NOT-05 | una notificación sin leer de E1 | E1 hace `POST /api/notificaciones/{id}/leida` | responde 200, la notificación queda `leida: true` con su fecha de lectura y ya no sale en `?estado=NoLeidas`. Repetirlo responde 200 y no cambia la fecha. |
| N-09 | RF-NOT-05 | una notificación de E2 | E1 hace `POST /api/notificaciones/{id}/leida` con ese id | responde 404 («La notificación no existe») y la notificación de E2 sigue sin leer. Lo mismo si el que lo intenta es un Administrador. |
| N-10 | RF-NOT-06 | E1 con el canal de correo activo (por defecto) | se le genera una notificación (N-02) | además de la notificación interna, en `cola_correos.correos_en_cola` hay un correo nuevo para el correo de E1 con el mismo asunto, y después de que corre el enviador llega a Mailpit. |
| N-11 | RF-NOT-07 | E1 hace `PUT /api/notificaciones/preferencias` con `{"correo": false}` | se le genera una notificación | la notificación interna aparece en su bandeja y **no** se encola ningún correo para E1 (la cantidad de filas de la cola para su correo no cambia). `GET /api/notificaciones/preferencias` responde `{"correo": false}`. |
| N-12 | RF-NOT-07 | E1 tiene el correo desactivado | A1 recibe una notificación por la misma solicitud | A1 sí recibe su correo: la preferencia es por usuario. |
| N-13 | RD-06 | — | cualquiera de las operaciones de notificaciones sin sesión | 401. |

## Cola de correos

| # | Requisito | Dado | Cuando | Entonces |
|---|---|---|---|---|
| C-01 | RF-NOT-08 | Mailpit detenido (`docker compose stop mailpit`) | E1 crea una solicitud | la operación responde 201 en menos de 2 s y los correos quedan en la cola como `Pendiente`. |
| C-02 | RF-NOT-09 | correos `Pendiente` | corre el enviador (servicio `enviador`, o `docker compose run --rm enviador` para una pasada) | quedan `Enviado` con su fecha de envío, sin repetir la operación que los creó. |
| C-03 | RF-NOT-10 | Mailpit detenido y un correo `Pendiente` | el enviador hace pasadas | cada pasada suma 1 a `intentos` y guarda `ultimo_error`; al llegar al máximo declarado en el README (`COLA_MAX_INTENTOS`, 5 por defecto) el correo queda `Fallido` y no se vuelve a intentar en las pasadas siguientes. El correo no se borra. |
| C-04 | RF-NOT-10 | un correo con 2 intentos fallidos | Mailpit vuelve a estar arriba y corre el enviador | el correo queda `Enviado` con `intentos = 3`. |
| C-05 | RF-NOT-11 | la cola con correos en los tres estados | A1 consulta `GET /api/cola-correos` | recibe cada correo con destinatario, asunto, estado (`Pendiente`, `Enviado` o `Fallido`), intentos, fecha de creación, fecha de envío o del último intento y último error. Con `?estado=Fallido` solo vienen los fallidos. |
| C-06 | RF-NOT-11 | — | E1 consulta `GET /api/cola-correos` | responde 403 y no devuelve datos. |
| C-07 | RF-NOT-12 | correos `Pendiente` | el enviador corre dos veces seguidas | cada correo llega una sola vez a Mailpit y la segunda pasada informa 0 enviados. |
| C-08 | RF-NOT-13 | el repositorio en el tag | se busca la contraseña SMTP o la clave de la API en `git log --all -p` | no aparecen; `.env` no está en ningún commit. |

## Reportes (pieza 5)

| # | Requisito | Dado | Cuando | Entonces |
|---|---|---|---|---|
| R-01 | RF-REP-01, 03 | 6 solicitudes de 3 usuarios, en 2 meses y 3 estados | A1 consulta `GET /api/reportes/solicitudes?desde=...&hasta=...` | recibe filas agrupadas por mes y estado con la cantidad, y un total; hay menos filas que solicitudes y la suma de las cantidades es igual a las solicitudes del rango. |
| R-02 | RF-REP-02, 03 | órdenes recibidas de 2 proveedores (3 de uno, 1 del otro) y otras en Borrador | A1 consulta `GET /api/reportes/compras-por-proveedor?desde=...&hasta=...` | recibe una fila por proveedor con la cantidad de órdenes recibidas y el monto total comprado (suma de cantidad × costo de sus líneas); las órdenes que no están Recibidas no cuentan. Responde «¿cuánto le compramos a cada proveedor en el período?». |
| R-03 | RF-REP-03 | — | se revisa el código de los dos reportes | la agrupación y las sumas se hacen en PostgreSQL (`GROUP BY` generado por la consulta), no trayendo todos los registros a memoria ni en la interfaz. |
| R-04 | RF-REP-04 | E1 tiene 2 solicitudes y E2 tiene 3 | E1 consulta el reporte de solicitudes | los totales solo cuentan las 2 de E1. |
| R-05 | RF-REP-04 | E1 creó 1 orden recibida y A1 creó 2 | E1 consulta el reporte de compras | solo cuenta la orden de E1. A1 ve las 3. |
| R-06 | RF-REP-05 | los datos de R-01 | se consulta con dos rangos distintos (un mes y el otro) | los resultados son distintos y cada uno solo cuenta lo de su rango. `desde` y `hasta` son fechas `AAAA-MM-DD` en UTC y los dos días se incluyen completos. |
| R-07 | RF-REP-05, RD-07 | — | se consulta con `desde` posterior a `hasta`, o con una fecha que no es fecha | responde 400 con un mensaje que dice qué corregir; nunca 500. |
| R-08 | RD-06 | — | cualquiera de los dos reportes sin sesión | 401. |

## Para todo lo anterior
- Cada criterio tiene al menos una prueba automática con `[Trait("Requisito", ...)]`, en el comando de Pruebas del README.
- Los seis frentes de la revisión acumulada siguen funcionando y todas las suites anteriores siguen pasando.
- El README tiene las secciones **Notificaciones** (cómo provocar cada evento, cómo ejecutar el procesador, cómo provocar
  un fallo de envío y el máximo de intentos) y **Reportes** (qué pregunta responde el del negocio y cómo consultar los dos con un rango de fechas).
