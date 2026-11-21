# AGENTS.md — contexto para el agente de código

## Propósito
Sistema de **inventario para un negocio pequeño** (proyecto #6 de Programación III, ITLA). Tiene un Core reutilizable
(Control de acceso, Gestión de permisos, Cola de correos, Manejador de documentos y, desde la Práctica 5,
Notificaciones y Reportes) y un módulo de negocio (proveedores, productos, órdenes de compra, existencias).

## Lenguaje y framework
- Backend: C# sobre **.NET 10**, ASP.NET Core Minimal APIs, EF Core 10 con Npgsql (PostgreSQL 17), xUnit 2.
- Frontend: React + TypeScript + Vite, servido por nginx.
- Todo corre en Docker Compose. Paquetes NuGet con versión central en `backend/Directory.Packages.props`.

## Cómo correrlo
```bash
cp .env.example .env
docker compose up --build
```
La aplicación queda en http://localhost:8080, la API en http://localhost:8080/api y Mailpit en http://localhost:8080/correo/.
Administrador de ejemplo: el de `.env.example`.

## Cómo probarlo
```bash
docker compose -f docker-compose.pruebas.yml run --rm --build pruebas
```
Con el SDK instalado también sirve `dotnet test backend/Inventario.slnx`. Las pruebas **no** levantan la API ni
PostgreSQL (RD-12): cada suite usa SQLite en memoria y dobles de las interfaces de las otras piezas.
Antes de dar algo por terminado: compila sin avisos y pasan **todas** las suites, no solo la nueva.

## Estructura
- `backend/src/Inventario.Core.*`: una pieza del Core por proyecto, cada una con su `DbContext`, su esquema de
  PostgreSQL y sus migraciones. Las piezas se hablan **solo por interfaces** (`IUsuarioActual`, `IColaCorreos`, ...).
- `backend/src/Inventario.Negocio`: el módulo de negocio. Depende del Core; **el Core nunca depende del negocio** (RD-03).
- `backend/src/Inventario.Api`: el host. Endpoints delgados que traducen `Resultado` a HTTP (RD-02); la tabla
  `Acceso/Operaciones.cs` declara el rol de cada operación y todo endpoint debe usar `.Requiere(Operaciones.X)`.
- `backend/src/Inventario.Enviador`: proceso aparte que entrega la cola de correos.
- `backend/tests/*.Pruebas`: una suite por pieza más la de integración.

## Convenciones
- Código, nombres, mensajes y commits en **español**. Mensajes de error para el usuario, sin trazas, rutas ni SQL (RD-08).
- Las reglas viven en los servicios de cada pieza, que devuelven `Resultado`/`Resultado<T>` con `TipoFallo`; el endpoint no decide nada.
- Toda operación con datos de un usuario exige sesión y comprueba en el servidor que el dato es suyo (RD-06).
  Un recurso ajeno responde como si no existiera (404).
- Entradas inválidas dan 400 con mensaje, nunca 500 (RD-07).
- Una sola fuente de hora: `TimeProvider`. Todas las fechas se **guardan** en UTC (`DateTimeOffset`, `timestamptz`) (RD-11).
  Pero un **día** que el usuario escribe o lee (rango de un reporte, «hoy» en una pantalla) es del calendario del negocio:
  hora de Santo Domingo, UTC−4 todo el año. Se convierte a UTC en un solo lugar (`RangoFechas`); en el frontend, «hoy» se
  calcula con `timeZone: 'America/Santo_Domingo'`, nunca con `toISOString().slice(0, 10)`.
- **Concurrencia.** Una regla «no puede haber dos» (una solicitud viva por permiso, una fila de preferencias por usuario) la
  garantiza la base con un índice único o una clave, no una consulta seguida de un `INSERT`. Si el guardado choca, el servicio
  responde lo mismo que si la otra petición hubiera llegado antes (409, o vuelve a leer y actualiza); nunca 500.
- **Cola de correos.** Se reintenta solo lo que puede salir bien después (el servidor no responde). Un error que se repetirá
  igual (el destinatario no es una dirección válida) deja el correo `Fallido` enseguida, con el motivo.
- Tablas y columnas en snake_case (`UseSnakeCaseNamingConvention`). Cambios de modelo → migración de EF nueva en la pieza.
- Pruebas: nombre que dice el comportamiento, fases marcadas `// Preparar`, `// Actuar`, `// Verificar`,
  `[Trait("Requisito", "RF-XXX-NN")]`, cada prueba con su propio entorno (independientes y en cualquier orden).
- Commits pequeños en imperativo con prefijo (`feat:`, `fix:`, `test:`, `docs:`), autor `yojairoivan-sketch`.

## Restricciones
- **Nunca leas el archivo `.env`** ni lo muestres, y **nunca escribas credenciales** (contraseñas SMTP, claves de API,
  cadenas de conexión reales) en el código, en archivos versionados ni en mensajes de commit (RD-10). Solo variables de entorno.
- No modifiques las reglas ya entregadas de Control de acceso, Gestión de permisos (su máquina de estados),
  el Manejador de documentos ni la máquina de estados de la orden. Puedes **agregar** llamadas a interfaces nuevas.
- No cambies migraciones ya aplicadas; agrega migraciones nuevas.
- No cambies el comando de arranque ni el de pruebas, ni los puertos publicados (solo `web`, 8080).
- No borres ni debilites pruebas existentes para que algo pase.
- No hagas `git push`, no crees etiquetas ni PRs: eso lo hace el dueño del repositorio.

## Antes de dar algo por terminado (agregado en la Asignación 3)
Además de las suites, ejecuta el proyecto y provoca estos casos en cada operación nueva; los cuatro hallazgos de la
Asignación 3 (`docs/bitacora-asignacion-3.md`) salieron de aquí y ninguno lo detectaba una prueba existente:
- Dos peticiones iguales **a la vez** (`curl -Z --parallel-immediate ... URL URL`), no una detrás de otra.
- Entradas vacías, mal formadas y fuera de rango; un rango de fechas sin datos.
- Un correo para una dirección que pasa el registro pero es rara (`prueba.@ejemplo.com`).
- El último día del mes después de las 20:00 en Santo Domingo (02:00 UTC del día siguiente).
- Para cada caso, una prueba que falle sin la corrección: escríbela primero y compruébalo.

## Tarea delegada en la Práctica 5
Notificaciones (RF-NOT-01 a 07), completar la cola de correos (RF-NOT-10 y 11) y dos reportes con agregación
(RF-REP-01 a 05). Los criterios de aceptación están en `docs/criterios-notificaciones-reportes.md`: el trabajo se
acepta solo si cumple cada uno.
