# Sistema de Inventario para Negocio Pequeño

Proyecto Final — **Programación III**
Instituto Tecnológico de las Américas (ITLA) — Cuatrimestre **2026-C-3**

---

## Datos del estudiante

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez |
| Asignatura | Programación III |
| Período | 2026-C-3 |
| Institución | ITLA |
| Repositorio | https://github.com/yojairoivan-sketch/Proyecto-Final-P3 |

## Proyecto del catálogo

**Proyecto #6 — Inventario para negocio pequeño**

## Descripción

Aplicación web para que un negocio pequeño lleve el control de su inventario.
El sistema permite registrar **órdenes de compra** a proveedores, llevar el
historial de **movimientos de stock** (entradas, salidas y ajustes) y generar
**alertas de stock bajo** cuando la existencia de un producto cae por debajo
de su punto de reorden.

Alcance previsto:

- Catálogo de productos con unidad de medida y punto de reorden.
- Registro de proveedores.
- Órdenes de compra con sus líneas de detalle y estados.
- Movimientos de stock que actualizan la existencia de cada producto.
- Consulta de existencias actuales y panel de alertas de stock bajo.

## Tecnologías

| Capa | Tecnología |
|---|---|
| Backend | C# / .NET 9 — ASP.NET Core Web API |
| ORM | Entity Framework Core 9 |
| Frontend | React 19 + TypeScript + Vite |
| Base de datos | PostgreSQL 17 (proveedor Npgsql) |
| Entorno | Docker Compose |
| Control de versiones | Git + GitHub |

## Estructura del repositorio

```
.
├── backend/    # API en ASP.NET Core (.NET 9)
├── frontend/   # Cliente en React 19 + TypeScript + Vite
└── docs/       # Documentación, diagramas y entregables
```

## Estado: Semana 1 — declaración del proyecto

Entrega correspondiente a la **declaración del proyecto**. En esta semana se
define el tema, el alcance y el stack tecnológico, y se deja el repositorio
preparado con su estructura base.

- [x] Repositorio creado e inicializado.
- [x] Proyecto del catálogo seleccionado (#6 — Inventario para negocio pequeño).
- [x] Stack tecnológico declarado.
- [x] Estructura de carpetas (`backend/`, `frontend/`, `docs/`) y `.gitignore`.
- [ ] Modelo de datos y diagrama entidad-relación.
- [ ] Implementación del backend.
- [ ] Implementación del frontend.

> Aún no se ha agregado código de la aplicación; este commit contiene
> únicamente el andamiaje del repositorio.
