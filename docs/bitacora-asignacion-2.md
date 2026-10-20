# Bitácora de sesión con el agente — Asignación 2

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez (`yojairoivan-sketch`) |
| Fecha de la sesión | 2026-10-19 (lunes), por la noche |

## Tareas que le delegué

| # | Tarea | Resultado |
|---|---|---|
| 1 | Leer el enunciado, los Requerimientos del Core (pieza 2) y el diseño de componentes de la semana 2, y proponer cómo construir la pieza | Hecha |
| 2 | Construir Gestión de permisos: la pieza del Core, la API y la pantalla | Hecha; su script de comprobación tenía errores (caso A) |
| 3 | Proponer los casos de prueba y redactar la suite | Hecha: 45 pruebas |
| 4 | Comprobar que las pruebas verifican lo que dicen, rompiendo cada regla a propósito | Hecha; el primer informe era falso (caso C) |
| 5 | Comprobar que el comando de la sección «Pruebas» funciona desde un clon limpio | Hecha; encontró un error suyo del PR #12 (caso B) |
| 6 | Redactar el README y esta bitácora | Hecha |
| 7 | Revisar todo contra la rúbrica antes de publicar | Hecha; encontró cinco errores suyos y cometió otro al corregirlos (caso D) |

## Qué le pedí

Le pasé el PDF del enunciado y escribí:

> `@"C:\Users\User\Downloads\Asignacion2_GestionPermisos_PIII.pdf"` hagamos la asignacion 2

Lo interrumpí y le dije «vamos». Cuando me propuso publicar, le pedí antes una revisión completa (caso D).

## Qué devolvió

Primero leyó el enunciado, los Requerimientos del Core y la presentación del diseño de componentes. De la presentación tomó dos ideas que guiaron el diseño:

- Gestión de permisos responde «¿qué acceso elevado pediste y en qué estado está?».
- La nota del profesor: «la prueba unitaria de Permisos va a reemplazar a Control de acceso por un doble. Eso solo funciona si Permisos habla por la interfaz y no por la tabla».

Con eso propuso y construyó:

- **Qué es un permiso.** Una operación de Administrador marcada como solicitable en `Operaciones.cs` (`ListarUsuarios` y `ReactivarUsuario`). Dejó fuera `CambiarRol`, porque rompería RF-CA-08 (un Estándar no cambia roles), las operaciones de solicitudes, por RF-GP-02, y `DesactivarUsuario`, porque podría dejar el sistema sin Administradores.
- **Dónde se aplica.** Control de acceso guarda los permisos otorgados (`control_acceso.permisos_otorgados`) y los lee en cada petición, así que sirven sin volver a iniciar sesión. Gestión de permisos los otorga por una interfaz nueva, `IOtorgamientoPermisos`, y nunca toca esas tablas.
- **Una pieza nueva**, `Inventario.Core.GestionPermisos`, con su propio esquema. Los estados están en `EstadoSolicitud` y las transiciones en un único punto, `TransicionesSolicitud` (RD-04). Ahí también están las reglas de RF-GP-04 (motivo obligatorio) y RF-GP-07 (nadie resuelve la suya).
- **La API y la pantalla `/permisos`.**
- **La suite**, en un proyecto aparte: 25 pruebas de la máquina sola y 20 del servicio. Control de acceso se reemplaza por dos dobles: `UsuarioActualFalso` y `OtorgamientoFalso`.

Los commits, en orden:

```
16f5eca feat(RF-GP-05): guarda los permisos otorgados
336d39d feat(RD-04): declara la máquina de permisos
0d14636 feat(RF-GP-01): crea y resuelve solicitudes
498f734 feat(RF-GP-02): expone las solicitudes en la API
41897ee feat(RF-GP-01): agrega la pantalla de permisos
db0837b test(RF-GP-06): prueba las transiciones
a8fe930 test(RF-GP-01): prueba las solicitudes
e1fff3e chore(RD-12): corre las pruebas sin .env
107c9c6 test(RF-GP-06): marca cada prueba con su requisito    (caso D)
cf07fbf test(RF-GP-01): deja una conducta por prueba           (caso D)
7df475d fix(RD-11): da la hora con la precisión guardada      (caso D)
```

## Caso A — el script de comprobación mandaba un motivo con tilde

**Qué hizo.** Para probar la pieza contra la aplicación levantada, el agente escribió un script en Git Bash que recorre cada RF-GP con `curl`. En la primera corrida dio 43 OK y 8 fallas:

```
== RF-GP-04: rechazar exige motivo
  OK    sin cuerpo (400)
  OK    cuerpo vacío (400)
  OK    motivo en blanco (400)
  OK    sigue Pendiente (Pendiente)
  FALLA rechazo con motivo: esperaba '200', llegó '400'
  FALLA queda Rechazada: esperaba 'Rechazada', llegó ''
  FALLA guarda el motivo: esperaba 'Pídeselo a tu supervisor primero', llegó ''
== RF-GP-06: de Rechazada no se llega a Aplicada
  FALLA aprobar una rechazada: esperaba '409', llegó '200'
  FALLA sigue Rechazada: esperaba 'Rechazada', llegó 'Aplicada'
  ...
Resultado: 43 OK, 8 fallas
```

**Por qué estaba mal.** El motivo del rechazo era «Pídeselo a tu supervisor primero». En Windows, `curl.exe` recibe los argumentos en otra codificación (cp1252), así que la API recibió un JSON inválido y respondió 400. Este problema ya estaba anotado en mis notas de la materia de la Práctica 1, y el agente no lo tuvo en cuenta. Las demás fallas venían de esa: como el rechazo no se hizo, «aprobar una rechazada» aprobó una solicitud que seguía Pendiente. Así Beto, un usuario de prueba, terminó con el permiso `ReactivarUsuario` en mi base local.

**Cómo se detectó.** El agente no tocó el código de la pieza. Primero repitió la petición a mano: con la tilde en el argumento, y con el mismo texto guardado en un archivo UTF-8:

```
--- con tilde:
{"title":"Petición inválida","status":400,"detail":"La petición no tiene el formato esperado. Revisa que el cuerpo sea JSON válido."}
--- mismo texto desde archivo UTF-8:
{"title":"No encontrado","status":404,"detail":"La solicitud no existe."}
```

Con el archivo, la API leyó el JSON sin problema (el 404 es porque ese id no existía). El error estaba en el script, no en la pieza.

**Cómo se corrigió.** El motivo pasó a escribirse sin tildes en el script, y el README avisa lo mismo a quien use `curl`. La misma corrida tenía otro error del script: comparaba la hora de la respuesta (7 decimales) con la guardada en PostgreSQL (6 decimales). Se arregló tomando la referencia de una consulta hecha después de aprobar. La segunda corrida falló en RF-GP-07 porque el script no se podía repetir: el Administrador ya tenía una solicitud propia Pendiente de la corrida anterior, y la API rechazó bien la nueva por duplicada. Desde entonces el script crea cada vez a una Administradora nueva. La tercera corrida:

```
Resultado: 53 OK, 0 fallas
```

## Caso B — el comando de «Pruebas» no funcionaba en un clon limpio

**Qué pasaba.** Cuando agregó las pruebas de Control de acceso (PR #12, después de la etiqueta `practica-1`), el agente las documentó con `docker compose run --rm pruebas`. El enunciado de esta asignación pide que el comando funcione «desde un clon limpio del repositorio», y un clon limpio no trae `.env`. Antes de reutilizar el comando, el agente lo probó con un `.env` vacío:

```
$ docker compose --env-file vacio.env run --rm pruebas
Error while interpolating services.db.environment.POSTGRES_DB: required variable POSTGRES_DB is missing a value: Falta POSTGRES_DB en .env
```

Compose valida las variables de todo el archivo antes de arrancar cualquier servicio, aunque las pruebas no usen la base. Ese comando solo funcionaba porque yo ya tenía mi `.env`.

**Cómo se corrigió.** El servicio de pruebas pasó a un archivo aparte, `docker-compose.pruebas.yml`, que no pide variables (commit `e1fff3e`). Con el mismo `.env` vacío:

```
$ docker compose -f docker-compose.pruebas.yml --env-file vacio.env run --rm --build pruebas
Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45, Duration: 3 s - Inventario.Core.GestionPermisos.Pruebas.dll (net10.0)
Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44, Duration: 7 s - Inventario.Core.ControlAcceso.Pruebas.dll (net10.0)
```

## Caso C — informó que ninguna prueba detectaba los errores, y era falso

**Qué le pedí.** Las 45 pruebas pasaron a la primera, y eso no demuestra que verifiquen algo. El agente propuso la «regla del mutante»: romper cada regla a propósito, correr la suite y ver si alguna prueba falla. Escribió un script con 15 mutantes; por ejemplo, agregar Rechazada → Aplicada a la tabla, quitar la regla de RF-GP-07 o hacer que aprobar no otorgue el permiso.

**Qué devolvió.** El primer informe decía que los 15 mutantes habían sobrevivido, pero en la misma línea el resumen de `dotnet test` decía lo contrario:

```
M1 agrega Rechazada→Aplicada a la tabla: SOBREVIVIÓ  (Failed!  - Failed:     3, Passed:    42, Skipped:     0, Total:    45, ...)
M2 quita la regla de no resolver la propia (RF-GP-07): SOBREVIVIÓ  (Failed!  - Failed:     3, Passed:    42, ...)
...
Sobrevivientes: ['M1 agrega Rechazada→Aplicada a la tabla', 'M2 quita la regla ...', ... (los 15)]
```

**Cómo se detectó.** Por esa contradicción: «SOBREVIVIÓ» junto a «Failed: 3». El script buscaba los nombres de las pruebas fallidas en la salida, pero con `dotnet test -v q` esos nombres no se imprimen. Al no encontrar nombres, daba el mutante por vivo.

**Cómo se corrigió.** El script pasó a correr con `--logger "console;verbosity=normal"` y a leer también el conteo «Failed: N». La segunda corrida mostró los 15 mutantes muertos, y en cada uno falla la prueba que nombra esa regla:

```
M1 agrega Rechazada→Aplicada a la tabla: MUERTO por 3 prueba(s)
   - LosUnicosEstadosTerminalesSonAplicadaYRechazada
   - PasarDeRechazadaAAplicadaSeRechazaYElEstadoNoCambia
   - UnaTransicionFueraDeLaTablaSeRechazaYLaSolicitudNoCambia(desde:
M2 quita la regla de no resolver la propia (RF-GP-07): MUERTO por 3 prueba(s)
   - UnAdministradorNoPuedeAprobarSuPropiaSolicitud
   - UnAdministradorNoApruebaSuPropiaSolicitud
   - UnAdministradorNoRechazaSuPropiaSolicitud
M3 rechazo sin exigir motivo (RF-GP-04): MUERTO por 2 prueba(s)
M4 aprobar no aplica el permiso (RF-GP-05): MUERTO por 5 prueba(s)
M5 un Estándar consulta solicitudes ajenas (RF-GP-02): MUERTO por 1 prueba(s)
...
M14 cambia el estado aunque la transición no exista: MUERTO por 4 prueba(s)
M15 al arrancar no aplica las aprobadas: MUERTO por 1 prueba(s)

Sobrevivientes: ninguno
```

Después de cada mutante, el script devolvió el archivo a su versión con `git checkout`; el árbol quedó limpio.

En la misma revisión, el agente vio que dos pruebas usaban `Assert.ThrowsAsync` en la fase «Actuar», lo que mezclaba actuar con verificar. Las cambió a `Record.ExceptionAsync`, para que las tres fases queden separadas.

## Caso D — la revisión que le pedí antes de publicar

**Qué le pedí.** Antes de subir nada:

> antes de publicar revisa todo por favor. tengo que sacar todos los puntos

**Qué encontró.** Revisó el código, las pruebas, el README y esta bitácora contra la rúbrica, y encontró cinco errores suyos que se le habían pasado:

1. **Los IDs no estaban en las pruebas.** El enunciado dice que los identificadores se usan «en tus commits y en tus pruebas». Los commits los tenían, pero en las pruebas solo aparecían en los comentarios de las clases.
2. **Dos pruebas verificaban algo que su nombre no dice.** `RechazarUnaPendienteConMotivo…` mandaba el motivo con espacios alrededor, así que también probaba que se recortan. `NoSeCreaUnaSegundaSolicitud…` pedía «listarusuarios» en minúsculas, así que también probaba que el catálogo no distingue mayúsculas. La rúbrica pide que cada prueba verifique un solo comportamiento.
3. **Dos pruebas tenían la fase «Preparar» vacía**, solo con un comentario: `LosUnicosEstadosTerminalesSonAplicadaYRechazada` y `CrearConUnPermisoQueNoSePuedePedir…`.
4. **El README pedía una comparación que fallaba.** La fila «Aprobar dos veces» dice que, después del segundo intento, la solicitud muestra el mismo `resueltaEn`. Pero la respuesta de aprobar traía la hora con 7 decimales y la consulta, con 6: el mismo problema que tuvo el script en el caso A. Quien corrigiera comparando esos dos valores vería horas distintas y podría pensar que la segunda aprobación cambió algo.
5. **Esta bitácora decía que el comando de pruebas roto era de la Práctica 1.** En realidad llegó con el PR #12, después de la etiqueta `practica-1`.

**Cómo se corrigió.**

- Cada prueba lleva `[Trait("Requisito", "RF-GP-0X")]` (commit `107c9c6`). Así se pueden correr las de un requisito: con `--filter "Requisito=RF-GP-06"` corren las 18 de transiciones prohibidas.
- El motivo va sin espacios, el permiso se escribe tal cual y la fase «Preparar» de esas dos pruebas hace algo concreto (commit `cf07fbf`).
- El reloj de la API (`RelojDelSistema`, el único `TimeProvider`, RD-11) entrega la hora cortada a microsegundos, que es lo que guarda PostgreSQL. Ahora lo que devuelve aprobar es exactamente lo que queda en la base (commit `7df475d`).
- El README y esta bitácora quedaron corregidos.

**Un error más, al aplicar la corrección.** Para separar los dos commits de pruebas, el agente usó un script que copiaba solo las líneas `Trait`. El conteo de comprobación no cuadró:

```
SolicitudesPruebas.cs 18
TransicionesPruebas.cs 11
```

Eran 18 líneas para 17 pruebas. La expresión que buscaba los métodos de prueba también encontró `public void Dispose()`, y desde ahí la búsqueda hacia atrás dio la vuelta al archivo y duplicó el trait de la última prueba:

```
     [Fact]
+    [Trait("Requisito", "RF-GP-07")]
+    [Trait("Requisito", "RF-GP-07")]
```

Se quitó el duplicado y se comprobó que cada archivo tuviera un trait por prueba (`17 traits, 17 pruebas` y `11 traits, 11 pruebas`), iguales a los de la versión revisada. Después de las correcciones se repitieron los 15 mutantes (murieron los 15) y la suite (45 + 44 en verde).

## Verificaciones hechas en la sesión

- **Suite completa:** 45 pruebas de Gestión de permisos y 44 de Control de acceso, todas en verde, con `dotnet test` y con el comando de Docker.
- **Independencia:** cada uno de los 28 métodos de prueba se corrió solo, en un proceso aparte y en orden inverso: `Métodos corridos solos: 28, con problemas: 0`.
- **Contra la aplicación levantada:** el script recorre RF-GP-01 a 07, RD-06, RD-07 y el reinicio (RD-09): 53 OK, 0 fallas.
- **Entradas inválidas (RD-07):** permiso numérico o en lista, JSON `null` o roto, cuerpo vacío, sin `Content-Type`, justificación o motivo de 501 caracteres, ids que no existen o no son números, token inventado y método equivocado. Todas dan 400, 401, 404, 405 o 415 con un mensaje en español; ninguna da 500.
- **Como el profesor, después de la revisión:** clon limpio en `C:\Users\User\source\verif-a2`, sin `.env`. El comando de «Pruebas» dio 45 + 44 en verde, y con `--filter "Requisito=RF-GP-06"`, 18. Después: `.env` desde `.env.example`, `docker compose up --build -d` y la sección 9 del README seguida al pie de la letra, con 38 comprobaciones y ninguna falla, incluida la del `resueltaEn` de aprobar igual al guardado. El script completo dio `Resultado: 53 OK, 0 fallas`. En los 15 commits nuevos no hay credenciales y el autor es `yojairoivan-sketch`.
- **Interfaz:** en el navegador integrado, Beto (Estándar) pidió `ListarUsuarios` en `/permisos`. Carla (Administradora) intentó rechazarla sin motivo y la pantalla mostró «Escribe el motivo del rechazo: sin motivo la solicitud no se rechaza.». Luego la aprobó. Al volver a entrar, Beto vio «Permisos otorgados: ListarUsuarios» y la lista de usuarios, sin el selector de rol ni los botones de Administrador.
