# Sistema de Inventario para Negocio Pequeño

Proyecto Final — **Programación III**
Instituto Tecnológico de las Américas (ITLA) — Cuatrimestre **2026-C-3**

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez |
| Asignatura | Programación III |
| Período | 2026-C-3 |
| Repositorio | https://github.com/yojairoivan-sketch/Proyecto-Final-P3 |
| Proyecto del catálogo | **#6 — Inventario para negocio pequeño** |

Aplicación web para que un negocio pequeño lleve el control de su inventario: **órdenes de compra** a proveedores, **movimientos de stock** y **alertas de stock bajo**. Como todos los proyectos del curso, tiene dos partes: el **Core** (la misma especificación para los 25 proyectos) y el **módulo de negocio** (inventario).

## Estado: Asignación 2 — Gestión de permisos

Entregado en la etiqueta `asignacion-2`:

- **Gestión de permisos** (pieza 2 del Core): un usuario pide permiso para una operación que su rol no le da; un Administrador lo aprueba o lo rechaza con un motivo, y al aprobarlo el sistema se lo otorga en el acto. Cubre RF-GP-01 a RF-GP-07, con la máquina de estados en un solo punto del código (RD-04). Ver [cómo provocar cada criterio](#9-gestión-de-permisos-rf-gp-01-a-07).
- **Primera suite de pruebas de la pieza**: 45 pruebas xUnit que corren sin levantar la aplicación (RD-12). Ver [Pruebas](#pruebas).
- **Bitácora con el agente**: [`docs/bitacora-asignacion-2.md`](docs/bitacora-asignacion-2.md).
- Entró por los pull requests [#13 Gestión de permisos](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/13) y [#14 documentación](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/14).

Antes, en la etiqueta `practica-1`: **Control de acceso completo** (RF-CA-01 a RF-CA-22), la **cola mínima de correos** (RF-NOT-08, 09, 12 y 13) y la **estructura de la máquina de estados del negocio** ([`docs/maquina-de-estados.md`](docs/maquina-de-estados.md)). Cada funcionalidad entró por su pull request: [#4 base](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/4), [#5 cola de correos](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/5), [#6 registro y activación](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/6), [#7 sesión](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/7), [#8 roles y administración](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/8), [#9 contraseñas](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/9), [#10 máquina de estados](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/10), [#11 README](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/11) y, después de la etiqueta, [#12 pruebas de Control de acceso](https://github.com/yojairoivan-sketch/Proyecto-Final-P3/pull/12).

## Diagrama de componentes

Un solo contenedor (la aplicación) con las seis piezas del Core y el módulo de negocio. Cada flecha va de la pieza que **requiere** a la que **provee**, y la etiqueta dice para qué. El Core nunca apunta al módulo de negocio (RD-03).

```mermaid
flowchart LR
    subgraph Core["Core: misma especificación para los 25 proyectos"]
        ACC["<b>Control de acceso</b><br/>¿quién eres y qué rol tienes?<br/>✅ Práctica 1"]
        NOTI["<b>Notificaciones</b><br/>avisos y cola de correos<br/>✅ cola mínima · S11–S12"]
        PERM["<b>Gestión de permisos</b><br/>solicitud → aprobación<br/>✅ Asignación 2"]
        DOC["<b>Manejador de documentos</b><br/>subir, listar, borrado lógico<br/>S9"]
        REP["<b>Reportes</b><br/>agregación filtrada por rol<br/>S12"]
        AUD["<b>Auditoría</b><br/>quién hizo qué y cuándo<br/>S14"]
    end
    subgraph Negocio["Módulo de negocio"]
        MOD["<b>Inventario</b><br/>proveedores, productos y órdenes de compra<br/>✅ entidades y máquina de estados"]
    end

    ACC -->|"encola los correos de activación y recuperación"| NOTI
    PERM -->|"pregunta quién es y aplica el permiso aprobado"| ACC
    PERM -->|"avisa al solicitante"| NOTI
    DOC -->|"pregunta quién es y qué rol tiene"| ACC
    REP -->|"filtra según el rol de quien consulta"| ACC
    MOD -->|"pregunta quién es y qué rol tiene"| ACC
    MOD -->|"dispara avisos del negocio"| NOTI
    MOD -->|"alimenta un reporte"| REP
    MOD -.->|"adjunta documentos (opcional)"| DOC
    MOD -.->|"registra eventos"| AUD
```

Casi todas las piezas registran sus acciones en Auditoría; esa flecha punteada se dibuja una sola vez para no llenar el diagrama. Las piezas marcadas con ✅ ya están construidas; las demás indican la semana en que llegan.

| Pieza | Proyecto | Interfaz que ofrece a las demás |
|---|---|---|
| Control de acceso | `backend/src/Inventario.Core.ControlAcceso` | `IUsuarioActual` («¿quién es y qué rol?»), `IOtorgamientoPermisos` («desde ahora este usuario puede ejecutar esta operación»), `IServicioCuentas`, `IServicioSesiones`, `IServicioContrasenas`, `IServicioUsuarios` |
| Gestión de permisos | `backend/src/Inventario.Core.GestionPermisos` | `IServicioSolicitudes` (pedir, consultar, aprobar y rechazar) y la máquina de estados `TransicionesSolicitud` |
| Notificaciones (cola de correos) | `backend/src/Inventario.Core.ColaCorreos` | `IColaCorreos.EncolarAsync` y `ProcesadorCola` |
| Módulo de negocio | `backend/src/Inventario.Negocio` | `OrdenDeCompra.CambiarEstado` y `TransicionesOrdenCompra` |
| Host de la API | `backend/src/Inventario.Api` | Endpoints HTTP delgados y la tabla de roles por operación (`Acceso/Operaciones.cs`) |
| Enviador | `backend/src/Inventario.Enviador` | Proceso aparte que entrega la cola por SMTP |

Cada pieza tiene su propio esquema en PostgreSQL (`control_acceso`, `gestion_permisos`, `cola_correos` e `inventario`). Ninguna lee las tablas de otra: todo pasa por interfaces. Por ejemplo, Gestión de permisos pregunta quién es el usuario con `IUsuarioActual` y otorga el permiso aprobado con `IOtorgamientoPermisos`; nunca toca las tablas de Control de acceso. Ningún proyecto `Inventario.Core.*` referencia a `Inventario.Negocio`.

## Requisitos

- **Docker Desktop** con Docker Compose v2 (`docker compose version`), ya iniciado.
- **Git**. Los comandos de abajo funcionan en Git Bash y en PowerShell, salvo los de `curl`, que están escritos para **Git Bash** (en Windows PowerShell 5.1, `curl` es otro comando).
- Puertos libres: **8080** (la aplicación) y **8025** (Mailpit). PostgreSQL no publica puerto, así que no choca con otro que tengas instalado.

No hace falta instalar .NET, Node ni PostgreSQL: todo se compila y corre dentro de Docker.

## Cómo ejecutarlo

```bash
git clone https://github.com/yojairoivan-sketch/Proyecto-Final-P3.git
cd Proyecto-Final-P3
git checkout asignacion-2
cp .env.example .env
```

Abre `.env` y completa los valores vacíos (la tabla de abajo dice qué es cada uno):

- `POSTGRES_PASSWORD`: cualquier contraseña.
- `ADMIN_NOMBRE`, `ADMIN_CORREO` y `ADMIN_CONTRASENA`: el primer Administrador. La contraseña debe tener de 8 a 128 caracteres, con letras y números.
- `SMTP_REMITENTE` y, si quieres que el correo llegue a una bandeja real, el resto de `SMTP_*` (ver [Correo real](#correo-real-gmail)). Con los valores por defecto los correos llegan a Mailpit.

Después:

```bash
docker compose up --build -d
```

La primera vez tarda unos minutos, porque descarga las imágenes y compila. Cuando termine:

| Qué | Dónde |
|---|---|
| Aplicación (React) | http://localhost:8080 |
| API | http://localhost:8080/api (por ejemplo, http://localhost:8080/api/salud) |
| Bandeja de Mailpit (correo de pruebas) | http://localhost:8025 |

La portada dice «API: conectada». Para ver los registros de la API: `docker compose logs api`.

**Enviar los correos.** Las operaciones solo dejan los correos en la cola. Para entregarlos, corre el enviador:

```bash
docker compose run --rm enviador
```

Hace una pasada y termina. Si prefieres que los envíe solo cada 10 segundos, déjalo corriendo en otra terminal con `docker compose run --rm enviador --vigilar 10` (Ctrl+C lo detiene).

Para apagar todo: `docker compose down`. Los datos quedan en el volumen y vuelven en el siguiente `up`. `docker compose down -v` sí borra la base.

### Variables de entorno

Todas van en `.env`, que nunca se sube al repositorio. `.env.example` trae los nombres y solo los valores que no son secretos.

| Variable | Para qué | Obligatoria |
|---|---|---|
| `POSTGRES_DB` | Nombre de la base de datos. | Sí (trae `inventario`) |
| `POSTGRES_USER` | Usuario de la base de datos. | Sí (trae `inventario`) |
| `POSTGRES_PASSWORD` | Contraseña de ese usuario. | Sí, la eliges tú |
| `URL_PUBLICA` | Dirección con la que se abre la aplicación; con ella se arman los enlaces de los correos. | Sí (trae `http://localhost:8080`) |
| `ADMIN_NOMBRE` | Nombre del primer Administrador, que se crea al arrancar si no existe. | Sí |
| `ADMIN_CORREO` | Correo con el que entra ese Administrador. | Sí |
| `ADMIN_CONTRASENA` | Su contraseña (8 a 128 caracteres, con letras y números). | Sí |
| `SMTP_HOST` | Servidor de correo saliente. | Sí (trae `mailpit`) |
| `SMTP_PUERTO` | Puerto de ese servidor. | Sí (trae `1025`) |
| `SMTP_SEGURIDAD` | `Ninguna`, `StartTls` o `SslAlConectar`. | Sí (trae `Ninguna`) |
| `SMTP_USUARIO` | Usuario del servidor de correo. | Solo si el servidor pide autenticación |
| `SMTP_CONTRASENA` | Contraseña de ese usuario (en Gmail, una contraseña de aplicación). | Solo si el servidor pide autenticación |
| `SMTP_REMITENTE` | Dirección que aparece como remitente. | Sí |
| `WEB_PUERTO` | Puerto de la aplicación en tu máquina, si el 8080 está ocupado. Si lo cambias, cambia también `URL_PUBLICA`. | No (8080) |
| `MAILPIT_PUERTO` | Puerto de la bandeja de Mailpit, si el 8025 está ocupado. | No (8025) |

Si falta una variable obligatoria, `docker compose` se detiene y dice cuál. Solo el enviador recibe las variables `SMTP_*`: la API no las tiene, así que una operación nunca habla con el servidor de correo.

### Correo real (Gmail)

Para que los correos lleguen a una bandeja real (por ejemplo, al registrarte con tu propio correo), usa una cuenta de Gmail con verificación en dos pasos:

1. En https://myaccount.google.com/apppasswords crea una contraseña de aplicación (16 letras).
2. En `.env` pon:
   - `SMTP_HOST=smtp.gmail.com`
   - `SMTP_PUERTO=587`
   - `SMTP_SEGURIDAD=StartTls`
   - `SMTP_USUARIO` y `SMTP_REMITENTE`: tu dirección de Gmail.
   - `SMTP_CONTRASENA`: la contraseña de aplicación, sin espacios.
3. Después de cada operación que manda correo, corre `docker compose run --rm enviador` (o déjalo con `--vigilar 10`). El correo llega a la bandeja del destinatario y su enlace abre `URL_PUBLICA`.

No hace falta reiniciar la API: el enviador lee `.env` cada vez que se ejecuta.

## Cómo provocar cada criterio de aceptación

Todo se puede hacer desde la interfaz (http://localhost:8080) o armando la petición a mano con `curl`. Los `curl` de abajo son para **Git Bash**. En los textos que mandes con `curl` (por ejemplo un motivo de rechazo), no uses tildes: en Windows, `curl.exe` manda los argumentos en otra codificación y la API responde «La petición no tiene el formato esperado». Desde la interfaz las tildes funcionan bien.

**Lo nuevo de la Asignación 2 está en la [sección 9, Gestión de permisos](#9-gestión-de-permisos-rf-gp-01-a-07).**

**Preparación.** Para leer la base de datos:

```bash
docker compose exec db psql -U inventario -d inventario
```

(Si cambiaste `POSTGRES_USER` o `POSTGRES_DB`, usa tus valores.) Dentro de `psql`, `\q` sale.

Para guardar la credencial de sesión de un usuario en la variable `TOKEN` (cambia correo y contraseña):

```bash
TOKEN=$(curl -s http://localhost:8080/api/acceso/sesion -H 'Content-Type: application/json' -d '{"correo":"tu@correo.com","contrasena":"TuClave123"}' | sed -nE 's/.*"token":"([^"]+)".*/\1/p'); echo $TOKEN
```

### 1. Registro y activación (RF-CA-01, 02, 04, 14, 15, 16 y 17)

| Para comprobar | Haz esto | Resultado esperado |
|---|---|---|
| Registro | En `/registro` crea una cuenta con tu correo y una contraseña válida (por ejemplo `clave1234`). | «Cuenta creada. Te enviamos un correo con el enlace para activarla.» |
| Login antes de activar | En `/iniciar-sesion` entra con esa cuenta. | «La cuenta no está activa. Ábrela con el enlace que te enviamos por correo.» (403) |
| El correo sale por la cola | `docker compose run --rm enviador` y abre el correo (en tu bandeja o en http://localhost:8025). | Llega «Activa tu cuenta de Inventario» con el enlace. |
| Abrir el enlace | Ábrelo. | «Cuenta activada. Ya puedes iniciar sesión.» |
| Abrirlo por segunda vez | Vuelve a abrir el mismo enlace. | «Este enlace de activación ya se usó…» (400). La cuenta no cambia. |
| Correo duplicado | Regístrate otra vez con el mismo correo. | «Ya existe una cuenta con ese correo.» (409) |
| Contraseña de 5 caracteres | Regístrate con la contraseña `ab123`. | «La contraseña debe tener al menos 8 caracteres.» (400) |
| Correo mal formado | Regístrate con el correo `ana@@ejemplo`. | «El correo no tiene un formato válido.» (400) |
| Datos vacíos o JSON roto | `curl -i http://localhost:8080/api/acceso/registro -H 'Content-Type: application/json' -d '{"nombre":'` | 400 con un mensaje en español, sin trazas. |
| Reenvío del enlace | En `/reenviar-activacion` pide el enlace con un correo inexistente y con uno pendiente de activar. | La misma respuesta en los dos casos. El enlace anterior deja de servir: «Este enlace ya no sirve porque se pidió uno nuevo…». |
| Enlace vencido | Pide un enlace nuevo, corre en `psql` `update control_acceso.tokens_activacion set vence_en = now() - interval '1 minute' where not usado;` y abre el enlace. | «Este enlace de activación venció. Pide uno nuevo.» (400) |
| Rol único | En `psql`: `select correo, rol_id from control_acceso.usuarios;` | Cada usuario tiene un `rol_id` (1 = Administrador, 2 = Estándar); la columna no admite nulos. Todo usuario nuevo nace Estándar. |

### 2. Almacenamiento de las contraseñas (RF-CA-02, RD-05)

Registra y activa dos usuarios con la **misma** contraseña y, en `psql`:

```sql
select correo, hash_contrasena from control_acceso.usuarios;
```

La contraseña no aparece en ninguna columna, y los dos hashes son distintos. Es PBKDF2 con sal aleatoria (`PasswordHasher` de ASP.NET Core Identity), así que no se puede revertir.

### 3. Sesión (RF-CA-03, 07, 18 y 19)

| Para comprobar | Haz esto | Resultado esperado |
|---|---|---|
| Rechazos idénticos | Inicia sesión con tu correo y una contraseña incorrecta, y luego con un correo que no existe. | Los dos dan «Correo o contraseña incorrectos.» (401), sin decir cuál dato falló. |
| Sesión correcta | Entra con los datos correctos. | Lleva a `/cuenta` con tu nombre, correo y rol (`GET /api/yo`). |
| Consulta sin sesión | `curl -i http://localhost:8080/api/yo` | 401: «Necesitas iniciar sesión para usar esta operación.» |
| Cerrar sesión | Guarda tu `TOKEN` y ciérrala: `curl -i -X DELETE http://localhost:8080/api/acceso/sesion -H "Authorization: Bearer $TOKEN"` | «Sesión cerrada.» |
| Credencial cerrada | `curl -i http://localhost:8080/api/yo -H "Authorization: Bearer $TOKEN"` | 401: «La sesión no es válida o ya terminó…» |
| Bloqueo | Falla la contraseña cinco veces seguidas y luego usa la correcta. | El sexto intento da «La cuenta está bloqueada por 5 intentos fallidos seguidos. Intenta de nuevo en 15 minutos.» (423), aunque la contraseña sea correcta. |
| Fin del bloqueo y contador | Espera 15 minutos o, en `psql`, `update control_acceso.usuarios set bloqueado_hasta = now() where correo = 'tu@correo.com';`. Entra con la contraseña correcta. | Entra, y `select intentos_fallidos from control_acceso.usuarios where correo = 'tu@correo.com';` da 0. |

### 4. Roles y administración (RF-CA-04, 05, 06, 08, 20 y 21, RD-06)

**Exigencia de rol en un solo punto (RF-CA-05):** [`backend/src/Inventario.Api/Acceso/Operaciones.cs`](backend/src/Inventario.Api/Acceso/Operaciones.cs). Cada operación del sistema está ahí, en una línea, con los roles que la pueden ejecutar. Cada endpoint la declara con `.Requiere(Operaciones.X)`; si un endpoint no la declara, la API no arranca. El servidor verifica sesión y rol en cada petición, antes de leer el cuerpo.

| Operación | Quién |
|---|---|
| ConsultarSalud, Registrarse, ActivarCuenta, ReenviarActivacion, IniciarSesion, SolicitarRecuperacion, RestablecerContrasena | Pública |
| ConsultarMiCuenta, CerrarSesion, CambiarMiContrasena, ConsultarPermisosSolicitables, CrearSolicitudPermiso, ConsultarMisSolicitudes, ConsultarSolicitud | Administrador o Estándar |
| CambiarRol, DesactivarUsuario, ForzarRestablecimiento, ConsultarSolicitudesPendientes, AprobarSolicitud, RechazarSolicitud | Administrador |
| ListarUsuarios, ReactivarUsuario | Administrador, o quien recibió ese permiso con una solicitud aprobada ([sección 9](#9-gestión-de-permisos-rf-gp-01-a-07)) |

| Para comprobar | Haz esto | Resultado esperado |
|---|---|---|
| Estándar invoca una operación de Administrador a mano | Con el `TOKEN` de un usuario Estándar: `curl -i http://localhost:8080/api/usuarios -H "Authorization: Bearer $TOKEN"` | 403: «La operación ListarUsuarios es solo para: Administrador. Tu rol es Estándar.» |
| Estándar intenta cambiar su propio rol | Con su `TOKEN` y su id (sale en `GET /api/yo`): `curl -i -X PUT http://localhost:8080/api/usuarios/<id>/rol -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"rol":"Administrador"}'` | 403. El rol no cambia. |
| Listar usuarios | Entra con el Administrador de `.env` y abre `/usuarios`. | Nombre, correo, rol y estado (Activo, Pendiente de activación o Desactivado). La respuesta de `GET /api/usuarios` no trae hashes ni tokens. |
| Cambiar un rol | En `/usuarios`, cambia el rol de otro usuario con el selector. | «Rol cambiado a …». Con `curl`, el cuerpo es `{"rol":"Administrador"}` o `{"rol":"Estandar"}` (se acepta con o sin tilde). |
| Desactivar a un usuario con sesión abierta | Entra con ese usuario en otro navegador (o guarda su `TOKEN`). Como Administrador, pulsa «Desactivar». Luego usa su sesión: `curl -i http://localhost:8080/api/yo -H "Authorization: Bearer $TOKEN"` | Su sesión da 401, y si intenta entrar: «La cuenta está desactivada. Habla con un administrador.» (403). «Reactivar» le devuelve el acceso. |
| Desactivarse a sí mismo | En `/usuarios`, pulsa «Desactivar» en tu propia fila. | «Un Administrador no puede desactivarse a sí mismo.» (409) |
| Último Administrador | Intenta pasarte a Estándar cuando eres el único Administrador activo. | «No se puede quitar el rol al último Administrador activo.» (409) |

### 5. Contraseñas: recuperación, cambio y restablecimiento (RF-CA-09 a 13 y 22)

| Para comprobar | Haz esto | Resultado esperado |
|---|---|---|
| Respuesta idéntica | En `/recuperar` pide el código con un correo inexistente y con uno registrado. | Los dos: «Si el correo corresponde a una cuenta activa, te enviamos un código…». |
| Código por la cola | `docker compose run --rm enviador` y abre el correo «Código para restablecer tu contraseña…». | Trae el código y el enlace `/restablecer?codigo=...`, que vence en 30 minutos. |
| Usar el código | Abre el enlace y define una contraseña nueva. | «Contraseña cambiada…». |
| Usarlo otra vez | Vuelve a enviar el mismo código. | «Este código de recuperación ya se usó…» (400). La contraseña no cambia. |
| Contraseña vieja y nueva | Inicia sesión con la vieja y luego con la nueva. | La vieja da 401; la nueva entra. |
| Credencial emitida antes | Guarda un `TOKEN` antes de restablecer y úsalo después: `curl -i http://localhost:8080/api/yo -H "Authorization: Bearer $TOKEN"` | 401. |
| Código vencido | Pide otro código, corre en `psql` `update control_acceso.codigos_recuperacion set vence_en = now() - interval '1 minute' where not usado;` y úsalo. | «El código de recuperación venció. Pide uno nuevo.» (400). La contraseña no cambia. |
| Restablecimiento forzado | Como Administrador, en `/usuarios` pulsa «Forzar restablecimiento» en otro usuario. | Su contraseña anterior y sus sesiones dejan de servir en ese momento. Al correr el enviador le llega «Un administrador restableció tu contraseña…» con el código. |
| Cambio con la actual incorrecta | En `/cuenta`, cambia la contraseña escribiendo mal la actual. | «La contraseña actual no es correcta. La contraseña no cambió.» (400) |
| Cambio correcto | Cambia la contraseña con la actual correcta. | Todas las sesiones se cierran (también la actual) y hay que entrar con la nueva. La nueva también debe cumplir la política. |

### 6. Correo por cola (RF-NOT-08, 09, 12 y 13)

1. Apaga el servidor de correo de pruebas: `docker compose stop mailpit`.
2. Registra un usuario nuevo en `/registro`. La operación termina bien: la API nunca habla con el servidor SMTP.
3. En `psql`: `select id, destinatario, estado, intentos, ultimo_error from cola_correos.correos_en_cola order by id;`. El correo está **Pendiente**.
4. Corre el enviador con el servidor apagado: `docker compose run --rm enviador`. El correo cuenta como «Con error (siguen pendientes)». En la tabla sigue Pendiente, con `intentos = 1` y el error en `ultimo_error`.
5. Enciende el servidor (`docker compose start mailpit`) y corre el enviador **dos veces**. La primera lo envía («Enviados: …»); la segunda dice «No hay correos pendientes.». En Mailpit el correo aparece una sola vez.

Si usas Gmail en vez de Mailpit, el paso 1 se hace apuntando el enviador a un servidor que no existe solo para esa ejecución: `docker compose run --rm -e SMTP_HOST=no-existe.invalid enviador`.

Las credenciales SMTP solo están en `.env` (fuera del repositorio) y se leen como variables de entorno.

### 7. Reinicio (RD-09)

```bash
docker compose restart
```

Los usuarios siguen ahí: entra con cualquiera de ellos. También sobreviven a `docker compose down` + `docker compose up -d`, porque la base vive en el volumen `datos-postgres`.

### 8. Máquina de estados del negocio (RF-NEG-03, 04, 05, RD-04)

- Estados, en un solo lugar: [`backend/src/Inventario.Negocio/Ordenes/EstadoOrdenCompra.cs`](backend/src/Inventario.Negocio/Ordenes/EstadoOrdenCompra.cs).
- Transiciones, en un solo lugar, con la prohibida explícita y los terminales: [`backend/src/Inventario.Negocio/Ordenes/TransicionesOrdenCompra.cs`](backend/src/Inventario.Negocio/Ordenes/TransicionesOrdenCompra.cs).
- Tabla de transiciones y diagrama: [`docs/maquina-de-estados.md`](docs/maquina-de-estados.md).
- La entidad y su estado en la base: `\d inventario.ordenes_de_compra` en `psql` (columna `estado`).

### 9. Gestión de permisos (RF-GP-01 a 07)

Un usuario pide permiso para ejecutar una operación de Administrador. Un Administrador lo aprueba o lo rechaza con un motivo, y al aprobarlo el sistema se lo otorga en el acto.

**Qué se puede pedir.** Las operaciones de Administrador marcadas como solicitables en [`Operaciones.cs`](backend/src/Inventario.Api/Acceso/Operaciones.cs):

| Permiso | Qué permite |
|---|---|
| `ListarUsuarios` | Ver la lista de usuarios con su rol y su estado (`GET /api/usuarios`, página `/usuarios`). |
| `ReactivarUsuario` | Devolverle el acceso a un usuario desactivado. |

No se pueden pedir `CambiarRol` (un Estándar nunca cambia roles, RF-CA-08), las operaciones que resuelven solicitudes (RF-GP-02 y 07) ni `DesactivarUsuario` (podría dejar el sistema sin Administradores activos). El catálogo lo arma el host con su tabla de operaciones y se lo pasa a la pieza, así el Core no conoce las operaciones del módulo de negocio (RD-03).

**Máquina de estados (RD-04).** Los estados están en [`EstadoSolicitud.cs`](backend/src/Inventario.Core.GestionPermisos/Solicitudes/EstadoSolicitud.cs). Las transiciones se resuelven en un único punto, [`TransicionesSolicitud.cs`](backend/src/Inventario.Core.GestionPermisos/Solicitudes/TransicionesSolicitud.cs), que dice quién ejecuta cada una, con qué condición y qué deja registrado. Nadie más valida estados.

| Desde | Hacia | Quién | Condición |
|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. Registra quién aprobó y cuándo. |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar: otorga el permiso. |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. Registra quién, cuándo y el motivo. |
| Cualquier otra combinación | — | — | Prohibida: se rechaza (409) y la solicitud no cambia. |

En las transiciones que ejecuta un Administrador, quien resuelve no puede ser quien pidió (RF-GP-07). Aplicada y Rechazada son terminales.

```mermaid
stateDiagram-v2
    [*] --> Pendiente: un usuario pide un permiso
    Pendiente --> Aprobada: un Administrador la aprueba
    Aprobada --> Aplicada: el sistema otorga el permiso
    Pendiente --> Rechazada: un Administrador la rechaza con motivo
    Aplicada --> [*]
    Rechazada --> [*]
```

**Dónde queda guardado.** Las solicitudes, en `gestion_permisos.solicitudes_permiso`. El permiso otorgado, en `control_acceso.permisos_otorgados`: Control de acceso es quien responde qué puede hacer cada usuario, y lee esos permisos en cada petición. Por eso sirven sin volver a iniciar sesión.

**Preparación.** Necesitas dos sesiones: la del Administrador de `.env` y la de un usuario Estándar. Si todavía no tienes uno, regístralo y actívalo como en la [sección 1](#1-registro-y-activación-rf-ca-01-02-04-14-15-16-y-17). En Git Bash, con tus correos y contraseñas:

```bash
token() { curl -s http://localhost:8080/api/acceso/sesion -H 'Content-Type: application/json' -d "{\"correo\":\"$1\",\"contrasena\":\"$2\"}" | sed -nE 's/.*"token":"([^"]+)".*/\1/p'; }
TOKEN_ADMIN=$(token admin@tu-correo.com TuClaveDeAdmin1)
TOKEN_EST=$(token estandar@tu-correo.com clave1234)
echo "$TOKEN_ADMIN / $TOKEN_EST"
```

En la interfaz todo está en **`/permisos`** (enlace «Permisos» en la barra). Un Estándar ve «Pedir un permiso» y «Mis solicitudes»; un Administrador ve además «Solicitudes pendientes», con «Aprobar» y una caja para el motivo junto a «Rechazar». Para ver la base: `docker compose exec db psql -U inventario -d inventario`.

Sigue las filas en orden: cada una usa lo que dejó la anterior. `<id>`, `<id2>` y `<id3>` son los números que devuelve cada solicitud al crearse.

| Para comprobar | Haz esto | Resultado esperado |
|---|---|---|
| Antes de pedir (RF-GP-05) | `curl -i http://localhost:8080/api/usuarios -H "Authorization: Bearer $TOKEN_EST"` | 403: «La operación ListarUsuarios es solo para: Administrador. Tu rol es Estándar. Puedes pedir el permiso ListarUsuarios con una solicitud.» |
| Crear una solicitud (RF-GP-01) | En `/permisos`, como el Estándar, elige `ListarUsuarios` y pulsa «Enviar solicitud». Con `curl`: `curl -i http://localhost:8080/api/solicitudes -H "Authorization: Bearer $TOKEN_EST" -H 'Content-Type: application/json' -d '{"permiso":"ListarUsuarios","justificacion":"Hago el conteo mensual"}'` | 201: «Solicitud creada. Queda Pendiente hasta que un Administrador la apruebe o la rechace.», con `"estado":"Pendiente"`. Anota su `id`: es `<id>`. |
| Entrada inválida (RD-07) | Repite el `curl` anterior con `-d '{"permiso":"Volar"}'`, con `-d '{}'` y, por último, tal cual. | 400: «El permiso «Volar» no se puede pedir. Los que existen son: ListarUsuarios, ReactivarUsuario.»; 400: «Elige el permiso que pides: …»; 409: «Ya tienes una solicitud del permiso ListarUsuarios sin resolver.» |
| El Administrador consulta las pendientes (RF-GP-02) | En `/permisos`, como Administrador, mira «Solicitudes pendientes». Con `curl`: `curl -i http://localhost:8080/api/solicitudes/pendientes -H "Authorization: Bearer $TOKEN_ADMIN"` | 200 con la solicitud `<id>`, quién la pidió y su justificación. |
| Un Estándar no ve las pendientes (RF-GP-02, RD-06) | `curl -i http://localhost:8080/api/solicitudes/pendientes -H "Authorization: Bearer $TOKEN_EST"` | 403: «La operación ConsultarSolicitudesPendientes es solo para: Administrador. Tu rol es Estándar.» En cambio, `curl -s http://localhost:8080/api/solicitudes/mias -H "Authorization: Bearer $TOKEN_EST"` le devuelve solo las suyas. |
| Un Estándar no aprueba (RD-06) | `curl -i -X POST http://localhost:8080/api/solicitudes/<id>/aprobar -H "Authorization: Bearer $TOKEN_EST"` | 403: «La operación AprobarSolicitud es solo para: Administrador…». La solicitud sigue Pendiente. |
| Aprobar (RF-GP-03) | Pulsa «Aprobar» en la fila. Con `curl`: `curl -i -X POST http://localhost:8080/api/solicitudes/<id>/aprobar -H "Authorization: Bearer $TOKEN_ADMIN"` | 200: «Solicitud aprobada. … ya puede usar ListarUsuarios.» La solicitud trae `"estado":"Aplicada"`, `resueltaPorId`, `resueltaPorNombre` y `resueltaEn`: quién aprobó y cuándo. Anota ese `resueltaEn`. En `psql`: `select id, estado, resuelta_por_id, resuelta_por_nombre, resuelta_en, aplicada_en from gestion_permisos.solicitudes_permiso;` |
| Se aplica sola (RF-GP-05) | Repite la primera fila con el **mismo** `TOKEN_EST`, sin volver a iniciar sesión. Luego: `curl -s http://localhost:8080/api/yo -H "Authorization: Bearer $TOKEN_EST"` | 200 con la lista de usuarios. `/api/yo` trae `"permisos":["ListarUsuarios"]`. En la interfaz, el Estándar ve el enlace «Usuarios» y «Mi cuenta» dice «Permisos otorgados: ListarUsuarios». Nadie hizo nada aparte de aprobar. En `psql`: `select * from control_acceso.permisos_otorgados;` |
| Aprobar dos veces (RF-GP-06) | Repite el `curl` de aprobar `<id>`. Luego: `curl -s http://localhost:8080/api/solicitudes/<id> -H "Authorization: Bearer $TOKEN_ADMIN"` | 409: «Una solicitud Aplicada no puede pasar a Aprobada: solo se permite Pendiente → Aprobada → Aplicada y Pendiente → Rechazada. La solicitud sigue Aplicada.» La consulta muestra el mismo estado y el mismo `resueltaEn` que devolvió la primera aprobación. |
| Rechazar sin motivo (RF-GP-04) | Como el Estándar, pide `ReactivarUsuario` (el `curl` de crear con `-d '{"permiso":"ReactivarUsuario"}'`); su `id` es `<id2>`. Como Administrador, pulsa «Rechazar» con la caja de motivo vacía, o: `curl -i -X POST http://localhost:8080/api/solicitudes/<id2>/rechazar -H "Authorization: Bearer $TOKEN_ADMIN"` (sin cuerpo) y otra vez con `-H 'Content-Type: application/json' -d '{"motivo":"   "}'` | 400: «Escribe el motivo del rechazo: sin motivo la solicitud no se rechaza.» `GET /api/solicitudes/<id2>` sigue en Pendiente. |
| Rechazar con motivo (RF-GP-04) | Escribe un motivo y pulsa «Rechazar». Con `curl`: `curl -i -X POST http://localhost:8080/api/solicitudes/<id2>/rechazar -H "Authorization: Bearer $TOKEN_ADMIN" -H 'Content-Type: application/json' -d '{"motivo":"Pideselo a tu supervisor primero"}'` | 200: «Solicitud rechazada. El solicitante verá el motivo.», con `"estado":"Rechazada"` y `motivoRechazo`. El Estándar lo ve en «Mis solicitudes». |
| De Rechazada a Aplicada (RF-GP-06) | `curl -i -X POST http://localhost:8080/api/solicitudes/<id2>/aprobar -H "Authorization: Bearer $TOKEN_ADMIN"`. Luego consulta `<id2>` y `/api/yo` del Estándar. | 409: «Una solicitud Rechazada no puede pasar a Aprobada: … La solicitud sigue Rechazada.» Sigue Rechazada, con `aplicadaEn` en `null`, y el Estándar no recibió `ReactivarUsuario`. |
| Nadie resuelve la suya (RF-GP-07) | Como Administrador, pide un permiso: `curl -i http://localhost:8080/api/solicitudes -H "Authorization: Bearer $TOKEN_ADMIN" -H 'Content-Type: application/json' -d '{"permiso":"ReactivarUsuario"}'`; su `id` es `<id3>`. Intenta aprobarla y rechazarla con su propio token (`-d '{"motivo":"no"}'` para rechazar). En la interfaz, su fila dice «(tú)». | 403 en los dos intentos: «Nadie resuelve su propia solicitud: otro Administrador debe aprobarla o rechazarla.» `<id3>` sigue Pendiente. Solo otro Administrador puede resolverla (pásale el rol a otro usuario en `/usuarios`). |
| Un Estándar no ve las de otros (RF-GP-02) | `curl -i http://localhost:8080/api/solicitudes/<id3> -H "Authorization: Bearer $TOKEN_EST"` | 404: «La solicitud no existe.» No revela que existe; el Administrador sí la ve. |
| Reinicio (RD-09) | `docker compose restart` y repite las consultas de `<id>`, `<id2>` y `/api/usuarios` con `TOKEN_EST`. | Las tres solicitudes siguen con su estado y sus datos, y el Estándar conserva el permiso. |

Sobre «Rechazada → Aplicada»: ninguna operación de la API pasa una solicitud a Aplicada a mano. Eso lo hace el sistema justo después de aprobar, así que desde afuera la única forma de intentarlo es aprobar la solicitud rechazada, que es lo que hace la fila anterior. La prueba `PasarDeRechazadaAAplicadaSeRechazaYElEstadoNoCambia` intenta la transición directa contra la máquina de estados (ver [Pruebas](#pruebas)).

Si la aplicación se detuviera justo entre aprobar y otorgar el permiso, la solicitud quedaría Aprobada. Al arrancar, la API aplica las que hayan quedado así.

### 10. Historial

```bash
git log --oneline --graph --all
git ls-files
git log -p | grep -iE "SMTP_CONTRASENA=|POSTGRES_PASSWORD=|ADMIN_CONTRASENA="
```

Cada funcionalidad entró por su pull request, fusionado con merge commit. `.env` no está en el repositorio. La búsqueda solo encuentra las líneas vacías de `.env.example` y el propio comando de arriba en este README.

## Pruebas

Un solo comando, desde la raíz del repositorio. Funciona en un clon recién hecho: no necesita `.env`, ni la base de datos, ni levantar la aplicación (RD-12).

```bash
docker compose -f docker-compose.pruebas.yml run --rm --build pruebas
```

Compila dentro de Docker y corre las dos suites. La primera vez tarda unos minutos. Termina con:

```
Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45, ... - Inventario.Core.GestionPermisos.Pruebas.dll (net10.0)
Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44, ... - Inventario.Core.ControlAcceso.Pruebas.dll (net10.0)
```

Las dos líneas pueden salir en otro orden. Si tienes el SDK de .NET 10 instalado, también sirve `dotnet test backend/Inventario.slnx`.

Para correr una sola prueba, agrega un filtro al final del mismo comando:

```bash
docker compose -f docker-compose.pruebas.yml run --rm --build pruebas --filter "FullyQualifiedName~AprobarDosVeces"
```

### Gestión de permisos (45 pruebas)

Están en `backend/tests/Inventario.Core.GestionPermisos.Pruebas` y prueban la pieza directamente, sin la API ni PostgreSQL. Usan SQLite en memoria y un reloj controlado. En lugar de Control de acceso usan dos dobles de sus interfaces: `UsuarioActualFalso` dice quién hace la petición y `OtorgamientoFalso` anota los permisos otorgados.

| Archivo | Qué prueba |
|---|---|
| [`TransicionesPruebas.cs`](backend/tests/Inventario.Core.GestionPermisos.Pruebas/TransicionesPruebas.cs) | La máquina de estados sola, sin base de datos (25 pruebas). |
| [`SolicitudesPruebas.cs`](backend/tests/Inventario.Core.GestionPermisos.Pruebas/SolicitudesPruebas.cs) | RF-GP-01 a 07 a través de `IServicioSolicitudes`, como los usa la API. Lo guardado se lee con un contexto nuevo (20 pruebas). |

Las que pide el enunciado:

- **Transición válida** (RF-GP-03 y 04): `AprobarUnaPendienteLaDejaAprobadaConQuienAproboYCuando`, `AprobarDejaGuardadoQuienAproboYCuando`, `RechazarUnaPendienteConMotivoLaDejaRechazadaConQuienCuandoYElMotivo` y `RechazarConMotivoDejaGuardadosElEstadoYElMotivo`. Verifican el estado resultante y lo que la transición registra: quién, cuándo y el motivo.
- **Transición prohibida** (RF-GP-06): `AprobarDosVecesLaMismaSolicitudSeRechazaYElEstadoNoCambia`, `PasarDeRechazadaAAplicadaSeRechazaYElEstadoNoCambia` y `UnaTransicionFueraDeLaTablaSeRechazaYLaSolicitudNoCambia`, que recorre las 13 combinaciones que no están en la tabla. Verifican el rechazo y que la solicitud quedó igual que antes.

Cada prueba marca sus tres fases con comentarios (`// Preparar`, `// Actuar`, `// Verificar`), verifica un solo comportamiento y su nombre dice cuál. Cada una arma su propio entorno, con una base en memoria nueva, así que no comparten datos: pasan en cualquier orden y por separado.

Cada prueba lleva también el requisito que verifica, con `[Trait("Requisito", "RF-GP-0X")]`:

| Requisito | Pruebas |
|---|---|
| RF-GP-01 (6) | `UnEstandarCreaUnaSolicitudQueNaceEnPendiente`, `CrearConUnPermisoQueNoSePuedePedirSeRechazaSinGuardarNada` (4 casos), `NoSeCreaUnaSegundaSolicitudDelMismoPermisoMientrasLaPrimeraNoSeResuelve` |
| RF-GP-02 (3) | `ElAdministradorVeSoloLasSolicitudesPendientes`, `UnEstandarSoloVeSusPropiasSolicitudes`, `UnEstandarNoPuedeConsultarLaSolicitudDeOtroUsuario` |
| RF-GP-03 (2) | `AprobarUnaPendienteLaDejaAprobadaConQuienAproboYCuando`, `AprobarDejaGuardadoQuienAproboYCuando` |
| RF-GP-04 (7) | `RechazarUnaPendienteConMotivoLaDejaRechazadaConQuienCuandoYElMotivo`, `RechazarConMotivoDejaGuardadosElEstadoYElMotivo`, `RechazarSinMotivoNoSeCompletaYLaSolicitudSiguePendiente` (3 casos), `RechazarSinMotivoNoCambiaLaSolicitudGuardada`, `UnMotivoDeMasDe500CaracteresNoSeAcepta` |
| RF-GP-05 (5) | `ElSistemaAplicaUnaAprobadaYRegistraCuando`, `AlAprobarElSistemaOtorgaElPermisoAlSolicitanteSinOtroPaso`, `AlRechazarNoSeOtorgaNingunPermiso`, `SiOtorgarFallaLaSolicitudQuedaAprobadaSinAplicar`, `AlArrancarSeAplicanLasSolicitudesQueQuedaronAprobadas` |
| RF-GP-06 (18) | `AprobarDosVecesLaMismaSolicitudSeRechazaYElEstadoNoCambia`, `AprobarUnaSolicitudRechazadaSeRechazaYSigueRechazadaSinPermiso`, `PasarDeRechazadaAAplicadaSeRechazaYElEstadoNoCambia`, `UnaTransicionFueraDeLaTablaSeRechazaYLaSolicitudNoCambia` (13 casos), `UnAdministradorNoPuedeMarcarAplicadaUnaSolicitudPorqueEsoLoHaceElSistema`, `LosUnicosEstadosTerminalesSonAplicadaYRechazada` |
| RF-GP-07 (4) | `UnAdministradorNoApruebaSuPropiaSolicitud`, `UnAdministradorNoRechazaSuPropiaSolicitud`, `UnAdministradorNoPuedeAprobarSuPropiaSolicitud`, `OtroAdministradorSiPuedeAprobarLaSolicitudDeUnAdministrador` |

Para correr solo las de un requisito:

```bash
docker compose -f docker-compose.pruebas.yml run --rm --build pruebas --filter "Requisito=RF-GP-06"
```

La suite de Control de acceso no tiene pruebas con ese filtro, así que para ella el comando dice «No test matches the given testcase filter». Es normal: la línea que importa es la de `Inventario.Core.GestionPermisos.Pruebas.dll`, con 18 pruebas.

Para comprobar que verifican lo que dicen, cada regla se rompió a propósito (15 mutantes; por ejemplo, agregar Rechazada → Aplicada a la tabla o quitar la regla de RF-GP-07). En todos los casos falló la prueba que nombra esa regla. El detalle está en la [bitácora](docs/bitacora-asignacion-2.md).

### Control de acceso (44 pruebas)

Están en `backend/tests/Inventario.Core.ControlAcceso.Pruebas`. En lugar de PostgreSQL y SMTP usan SQLite en memoria, un reloj falso para provocar vencimientos y bloqueos, y una cola de correos falsa que guarda los enlaces. Cubren la política de contraseña y la validación de datos, el hash, registro y activación, sesión y bloqueo, recuperación, cambio y restablecimiento forzado de contraseña, y administración de usuarios.

## Estructura del repositorio

```
.
├── backend/
│   ├── Inventario.slnx                 # solución .NET 10
│   ├── Dockerfile                      # etapas api, enviador y pruebas
│   ├── src/
│   │   ├── Inventario.Api/             # host: endpoints, Operaciones.cs, errores
│   │   ├── Inventario.Core.ControlAcceso/
│   │   ├── Inventario.Core.GestionPermisos/   # pieza 2: solicitudes y su máquina de estados
│   │   ├── Inventario.Core.ColaCorreos/
│   │   ├── Inventario.Enviador/        # consola que entrega la cola
│   │   └── Inventario.Negocio/         # módulo de inventario
│   └── tests/
│       ├── Inventario.Core.ControlAcceso.Pruebas/     # pruebas xUnit (RD-12)
│       └── Inventario.Core.GestionPermisos.Pruebas/   # pruebas xUnit (RD-12)
├── frontend/                           # React 19 + TypeScript + Vite, servido por nginx
├── docs/                               # máquina de estados y bitácoras
├── docker-compose.yml
├── docker-compose.pruebas.yml          # las pruebas, sin .env
└── .env.example
```

## Tecnologías

| Capa | Tecnología |
|---|---|
| Backend | C# / .NET 10 (LTS) — ASP.NET Core, Minimal APIs |
| ORM | Entity Framework Core 10 con Npgsql |
| Frontend | React 19 + TypeScript + Vite, servido por nginx |
| Base de datos | PostgreSQL 17 |
| Correo | MailKit; Mailpit para pruebas locales |
| Entorno | Docker Compose |

En la declaración de la semana 1 el stack decía .NET 9. Se cambió a **.NET 10**, la versión LTS actual: el soporte de .NET 9 termina el 10 de noviembre de 2026, antes de que acabe el cuatrimestre.

## Notas de diseño

- **La lógica no vive en los endpoints (RD-02):** los endpoints solo traducen HTTP. Las reglas están en los servicios de cada pieza, que devuelven un resultado con un mensaje controlado.
- **Errores (RD-08):** toda respuesta de error es un ProblemDetails en español, sin trazas, rutas ni consultas.
- **Fechas (RD-11):** todas las piezas toman la hora de un único `TimeProvider` y la guardan en UTC (`timestamptz`).
- **Credenciales:** los tokens de sesión, de activación y de recuperación son 32 bytes aleatorios. En la base solo queda su SHA-256, así que leer las tablas no permite usarlos.
- **Dos máquinas de estados independientes (RF-NEG-09):** la de Gestión de permisos vive en el Core y la de la orden de compra en el módulo de negocio. No comparten código, y cambiar una no obliga a tocar la otra.
- **Bitácoras con el agente:** [`docs/bitacora-asignacion-1.md`](docs/bitacora-asignacion-1.md) y [`docs/bitacora-asignacion-2.md`](docs/bitacora-asignacion-2.md).
