# Bitácora de sesión con el agente — Asignación 4 (historial de auditoría)

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez (`yojairoivan-sketch`) |
| Fechas de la sesión | Del 2026-11-22 al 2026-11-26 (una sola sesión, retomada después de suspender el equipo) |
| Rama | `feat/auditoria-asignacion-4`; se entrega en la etiqueta `asignacion-4` |

## Tareas que le delegué

| # | Tarea | Resultado |
|---|---|---|
| 1 | Leer el enunciado, el contexto de la materia y el código que hay que auditar, y proponer un plan | Hecha: un plan de 11 puntos, que aprobé |
| 2 | Construir la pieza 6 del Core: el historial, su consulta y su protección contra cambios | Hecha |
| 3 | Que Control de acceso, Gestión de permisos y Documentos dejen su registro en cada evento exigido | Hecha |
| 4 | Escribir las pruebas (una por evento, la del Estándar y la de que nada modifica ni borra) | Hecha; su primera prueba de RF-AUD-06 no veía operaciones ocultas (caso A) y luego dio un falso positivo (caso B) |
| 5 | Comprobar que las pruebas detectan errores, rompiendo cada regla a propósito | Hecha: 11 de 11 detectados |
| 6 | Escribir la sección «Auditoría» del README y seguirla al pie de la letra en un clon limpio | Hecha; un resultado esperado estaba mal (caso C) |
| 7 | Correr el comando de Pruebas desde el clon limpio y redactar esta bitácora | Hecha |

## Qué le pedí

1. Le pasé el PDF del enunciado (`Asignacion4_HistorialAuditoria_PIII.pdf`) sin más instrucciones. Leyó el enunciado, el
   contexto acumulado de la materia y los servicios que había que auditar, y me propuso un plan de 11 puntos antes de tocar
   el código.
2. Le respondí **«Sí, adelante»**. El plan aprobado fijaba las decisiones que defiendo en el proyecto final:
   - una pieza nueva que **no depende de ninguna otra**, porque Control de acceso tiene que escribir en ella (si dependiera
     de Control de acceso habría un ciclo);
   - el registro **se guarda en la misma transacción que el cambio**;
   - el valor anterior y el nuevo como **JSON con solo lo que cambió**, nunca vacío (`{"estado":"No existía"}` al crear);
   - el código de recuperación guarda **quién lo pidió**, para que el registro diga si el restablecimiento lo inició el
     propio usuario o un Administrador;
   - inmutabilidad en tres capas (sin operaciones, el contexto rechaza modificar o borrar, y un trigger en PostgreSQL);
   - la consulta con sus filtros, reservada al Administrador dentro de la pieza;
   - las pruebas, la pantalla, el README, esta bitácora y la verificación desde un clon limpio, sin publicar nada sin mi
     confirmación.
3. A mitad de la sesión interrumpí una comprobación y le dije **«sigue»**. Encontró Docker Desktop caído con el error de
   siempre («initializing Inference manager») y me pidió permiso para la receta conocida (cerrarlo y renombrar sus carpetas
   `run` y `docker-secrets-engine`). Le respondí **«Sí, aplícala»**.

## Qué devolvió

Siete commits en la rama, cada uno compila sin avisos y pasa sus pruebas por separado (se comprobó en un worktree aparte):

```
29d0cbd feat: agrega la pieza de auditoría
b1471a9 test: agrega la suite de la pieza de auditoría
4db5d71 feat: audita cambios de rol, estado y contraseña
221d95f feat: audita la resolución de solicitudes
5aeb986 feat: audita subir y eliminar documentos
6479c5f feat: consulta el historial en /api/auditoria
fdb9987 feat: agrega la pantalla del historial
```

| Commit | Pruebas al pasar por él |
|---|---|
| `29d0cbd` | 375 en 8 suites (las de antes, sin cambios) |
| `b1471a9` | 416 en 9 suites |
| `4db5d71` | 425 |
| `221d95f` | 430 |
| `5aeb986`, `6479c5f`, `fdb9987` | 433 |

- **La pieza** [`Inventario.Core.Auditoria`](../backend/src/Inventario.Core.Auditoria): el registro, `HistorialAuditoria.Auditar`
  (que cada pieza auditada llama en su propio contexto), la guarda que rechaza modificar o borrar, la consulta con filtros y
  la migración con el trigger.
- **Los eventos:** 10 acciones en tres piezas (ver la tabla de la sección [Auditoría](../README.md#qué-se-audita-rf-aud-02-a-05)
  del README). Para saber quién actúa, los servicios de usuarios y contraseñas reciben ahora `IUsuarioActual`.
- **La API y la pantalla:** `GET /api/auditoria` y `/auditoria`.
- **Las pruebas:** la suite de la pieza (41) y 17 pruebas de eventos repartidas en las suites de Control de acceso (9),
  Gestión de permisos (5) y Documentos (3). Cada una compara todos los campos del registro, no solo que exista.

### Comprobación de que las pruebas sirven

El agente rompió cada regla a propósito con un script que cambia una línea, corre las pruebas del requisito y restaura el
archivo. Con cada cambio falló al menos la prueba que lo cubre:

| Cambio | Pruebas que fallaron |
|---|---|
| El rango de fechas deja fuera el último día | `ElRangoDeFechasIncluyeElPrimerYElUltimoDiaCompletosEnElDiaDelNegocio`, `UnSoloDiaDejaTodoLoDeEseDiaYNadaMas`, `LosFiltrosSeCombinan` |
| La consulta no exige ser Administrador | `UnEstandarNoConsultaElHistorial`, `UnEstandarConUnFiltroInvalidoRecibeElMismoRechazoYNoUnoDeFiltro` |
| La guarda del contexto apagada | `ModificarUnRegistroGuardadoSeRechazaYQuedaComoEstaba`, `BorrarUnRegistroGuardadoSeRechazaYSigueAhi` |
| Un método oculto que borra el historial | `LaPiezaNoTieneNingunaOperacionDeEdicionNiDeBorradoNiSiquieraOculta` |
| El rol anterior se guarda igual al nuevo | `CambiarElRolDejaQuienLoCambioCuandoYElRolAnteriorYElNuevo` |
| Reactivar no deja registro | `ReactivarDejaElEstadoAnteriorYElNuevo` |
| Se ignora quién pidió el código | `DefinirLaContrasenaConElCodigoDeUnRestablecimientoForzadoDiceQueLoInicioUnAdministrador` |
| El hash de la contraseña entra al registro | Las cuatro pruebas de RF-AUD-05 |
| Rechazar se registra como aprobar | `RechazarDejaQuienRechazoQueSolicitudCuandoYElMotivo` |
| Eliminar un documento no deja registro | `EliminarDejaQuienLoEliminoCuandoYQuePasoDeVigenteAEliminado`, `UnaSubidaOUnaEliminacionRechazadaNoDejaRegistro` |
| La subida guarda un valor anterior inventado | `SubirDejaQuienLoSubioCuandoYQueAntesNoExistia` |

## Dónde se equivocó el agente

### Caso A — La prueba de «nada modifica ni borra» no veía las operaciones ocultas

**Qué hizo.** La primera versión de `LaPiezaNoOfreceNingunaOperacionDeEdicionNiDeBorrado` revisaba por reflexión solo los
tipos **públicos** de la pieza (`GetExportedTypes`).

**Cómo se detectó.** Al preparar el cambio «un método oculto que borra el historial» para comprobar las pruebas, el agente
lo escribió como un método `Limpiar` dentro de `ConsultaAuditoria`, que es una clase interna. Antes de correrlo se dio cuenta
de que la prueba nunca lo iba a ver: solo miraba lo público. La rúbrica nombra justo ese caso: «existe una operación que
modifica o elimina registros pero está oculta».

**Cómo se corrigió.** La prueba pasó a revisar todos los métodos de la pieza, públicos o no (sin los que genera el
compilador), y se renombró `LaPiezaNoTieneNingunaOperacionDeEdicionNiDeBorradoNiSiquieraOculta`. Con el método oculto
plantado, ahora falla (fila 4 de la tabla de arriba).

### Caso B — Al ampliar esa prueba, dio un falso positivo con un nombre suyo

**Qué hizo.** Al ampliar la prueba agregó «limpi» a la lista de palabras prohibidas (para atrapar un `Limpiar`). La prueba
falló sobre el código correcto:

```
Assert.Empty() Failure: Collection was not empty
Collection: ["FiltroValido.Limpio"]
```

**Cómo se detectó.** Corriendo la suite de la pieza: 1 de 4 pruebas de RF-AUD-06 en rojo.

**Por qué pasó.** `Limpio` era un ayudante del propio agente que recorta los espacios de un filtro; no borra nada.

**Cómo se corrigió.** El ayudante se renombró `Recortado`, y la suite volvió a 41 de 41. De paso, el compilador avisó
`xUnit2029` («usa Assert.DoesNotContain») en la misma línea; también se corrigió, para que la compilación siguiera sin avisos.

### Caso C — Un resultado esperado del README estaba mal contado

**Qué hizo.** En el paso 12 de la sección «Auditoría» del README escribió que la consulta del día daba `"total":12` la
primera vez que se siguen los pasos.

**Cómo se detectó.** Siguiendo el README al pie de la letra: el agente sacó los 17 bloques de comandos del README y los
corrió en una sola sesión de Git Bash, en un clon limpio con la base vacía. El paso 12 imprimió:

```
"total":11
"total":11
"total":0
```

**Por qué pasó.** Escribió el número sin contarlo. Los pasos dejan 11 registros: 2 de solicitudes, 2 del documento, 2 de rol,
2 de estado y 3 de contraseña.

**Cómo se corrigió.** El README dice ahora `"total":11`, con ese desglose. Los otros 16 pasos dieron exactamente lo que
dice el README, incluidos los mensajes de error, el 403 del Estándar, los 405 y el error del trigger en `psql`.

### Un incidente que no fue del agente

En la primera corrida de los pasos, el código de recuperación recién pedido se rechazaba con «El código de recuperación
venció». En la base, dos códigos pedidos con segundos de diferencia tenían 3 horas de distancia (01:45 y 04:45 UTC): el reloj
de la máquina virtual de Docker se estaba poniendo al día a saltos después de suspender el equipo. El agente comparó
`date -u` de Windows con el del contenedor, ya iguales, vació la base y repitió: los 17 pasos pasaron. No cambió nada del
código ni del README por esto.

## Cómo se verificó antes de entregar

- Cada uno de los 7 commits compila sin avisos y pasa sus pruebas por separado.
- Los 11 cambios de la tabla de arriba hacen fallar sus pruebas.
- En un clon limpio, con `cp .env.example .env` y `docker compose up --build -d`: los 17 pasos de la sección «Auditoría» del
  README dan lo que dice el README, y el historial tiene el mismo total antes y después de `docker compose down` y `up`.
- El comando de la sección «Pruebas», tal como está escrito, desde el clon limpio: 433 pruebas en 9 suites, salida 0.

## Un registro de auditoría frente a un commit

El registro es el número 5 de la corrida en el clon limpio, cuando el Administrador le cambió el rol a Eva:

```json
{"id":5,"fechaHora":"2026-11-26T19:42:35.834951+00:00","usuarioId":1,"usuarioCorreo":"admin@inventario.local",
 "usuarioRol":"Administrador","accion":"CambiarRol","entidad":"Usuario","entidadId":"2",
 "valorAnterior":{"rol":"Estándar"},"valorNuevo":{"rol":"Administrador"}}
```

El commit es el que hizo que ese registro exista:

```
commit 4db5d71bf2efd4b725bdee33bea41a63320c5afa
Author:     yojairoivan-sketch <yojairoivan@gmail.com>
AuthorDate: 2026-11-22T13:48:28-04:00
Parent:     b1471a90e2a15708597be813ac4138c5a28ff39a

    feat: audita cambios de rol, estado y contraseña
 14 files changed, 1022 insertions(+), 18 deletions(-)
```

| En git | En el registro | Cómo se parecen y en qué se diferencian |
|---|---|---|
| **Autor** (`Author: yojairoivan-sketch <yojairoivan@gmail.com>`) | `usuarioId` 1, `usuarioCorreo` `admin@inventario.local` y `usuarioRol` `Administrador` | Los dos dicen quién. El autor de git lo escribe quien hace el commit y se puede poner cualquiera (`git config user.name`); el usuario del registro sale de la sesión validada en el servidor y no se puede elegir. El registro además guarda **con qué rol** actuó. |
| **Mensaje** (`feat: audita cambios de rol, estado y contraseña` y su cuerpo) | `accion` `CambiarRol` | Los dos dicen qué se hizo. El mensaje es texto libre y explica **por qué**; la acción es un nombre de una lista fija, el de la operación de la API, y no explica nada. |
| **Fecha** (`AuthorDate: 2026-11-22T13:48:28-04:00`) | `fechaHora` `2026-11-26T19:42:35.834951+00:00` | Los dos dicen cuándo. Git guarda segundos y la zona del equipo de quien hizo el commit (que se puede cambiar); el registro guarda UTC con microsegundos, del mismo reloj del sistema que el cambio, y es la misma hora que queda en la entidad. |
| **Identificador del commit** (`4db5d71bf2ef…`) | `id` 5, y `entidad` `Usuario` con `entidadId` 2 | El hash de git se calcula con el contenido, el autor, la fecha y el padre: si algo cambia, cambia el hash. El `id` del registro es solo un número que crece. Lo que identifica **qué** cambió es `entidad` y `entidadId`, que en git serían las rutas de los archivos del diff. |
| **Diff** (14 archivos, +1022 −18, línea por línea) | `valorAnterior` `{"rol":"Estándar"}` y `valorNuevo` `{"rol":"Administrador"}` | Los dos dicen qué había antes y qué hay después. El diff compara líneas de texto de muchos archivos a la vez; el registro compara campos con nombre de una sola entidad, y solo los que cambiaron. |

**Lo que guarda el commit y el registro no:**

- **El padre** (`Parent: b1471a9…`): cada commit apunta al anterior y forma una cadena. Los registros no se enlazan entre sí;
  se ordenan por `id`.
- **La foto completa del proyecto** (el árbol, `Tree`): con un commit se puede reconstruir todo el repositorio en ese momento.
  Con un registro solo se sabe cómo quedó una entidad, no el resto de la base.
- **El porqué**: el cuerpo del mensaje.
- **Autor y quien confirma por separado** (`Author` y `Committer`), y varios cambios de muchos archivos en una sola unidad.

**Lo que guarda el registro y el commit no:**

- **El rol** de quien actuó en ese momento. Si mañana le quitan el rol de Administrador, el registro sigue diciendo con cuál lo hizo.
- **Valores con nombre**: `{"rol":"Estándar"}` se lee sin saber de qué archivo o línea venía.
- **Que no se puede reescribir.** En git, la historia sí se puede reescribir: `git rebase` y `git push --force` cambian los
  commits (en este repositorio se hizo una vez, el 2026-09-26, con autorización, para dividir el commit inicial). Los
  commits nuevos tienen otro hash, así que se nota, pero se puede hacer. El historial de auditoría no tiene ninguna operación
  para eso, y la base misma rechaza `UPDATE`, `DELETE` y `TRUNCATE` con un trigger.
- **Que el autor no se elige**: lo pone el servidor a partir de la sesión.

Las consultas también se parecen: `git log --author=…` es `?usuario=…`; `git log -- archivo` es `?entidad=…&entidadId=…`, la
historia completa de una sola cosa; y `git log --since=… --until=…` es `?desde=…&hasta=…`.
