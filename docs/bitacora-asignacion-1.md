# Bitácora de sesión con el agente — Asignación 1

| Campo | Valor |
|---|---|
| Estudiante | Yojairo Rodriguez (`yojairoivan-sketch`) |
| Agente | Claude Code, app de escritorio (pestaña Code) |
| Modelo | Claude Sonnet 5 al inicio; Claude Opus 5.5 desde `/model claude-opus-5-5` |
| Fecha de la sesión | 2026-09-26 |

## Tareas que le delegué

| # | Tarea | Resultado |
|---|---|---|
| 1 | Leer el enunciado (PDF) y resumirlo junto con la fecha de entrega | Hecha; los primeros intentos fallaron (caso B) |
| 2 | Revisar el repositorio contra la sección 1.1 y la rúbrica | Hecha; se equivocó en un criterio (caso A) |
| 3 | Dividir el commit inicial en commits atómicos y redactar sus mensajes | Hecha |
| 4 | Redactar la descripción del pull request que agrega esta bitácora | Hecha |

## Caso A — dio por bueno un commit que no cumplía la regla

**Qué le pedí.** Que trabajara la Asignación 1 a partir del enunciado. Como parte de eso,
el agente evaluó el historial del repositorio frente a la sección 1.1: commits atómicos,
asunto en imperativo y de hasta 50 caracteres.

**Qué devolvió.** Anotó en las notas de la materia (`contexto/decisiones.md`, fuera de
este repositorio):

> los commits deben ser imperativos ≤50 caracteres (el commit inicial `chore: ...` es
> válido pero conviene seguir la regla en adelante)

**Por qué estaba mal.** El único commit del repositorio era
`e4f3f4f chore: estructura inicial y README`, y no cumplía la regla:

- El asunto es una frase nominal («estructura inicial y README»), no un imperativo.
- Mezcla tres cambios sin relación: el `.gitignore`, el README y las carpetas del proyecto.

```
$ git show --stat --format=%s e4f3f4f
chore: estructura inicial y README

 .gitignore        | 39 +++++++++++++++++++++++++++++
 README.md         | 73 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 backend/.gitkeep  |  0
 docs/.gitkeep     |  0
 frontend/.gitkeep |  0
 5 files changed, 112 insertions(+)
```

Según la rúbrica, que «al menos un commit mezcla cambios sin relación» deja el criterio
*Historial limpio* en 50 %. Además, ese commit lo había redactado el propio agente en la
sesión del 2026-09-14 (lleva el trailer `Co-Authored-By: Claude Opus 5`).

**Cómo se detectó.** Antes de crear la rama de esta bitácora, el agente volvió a contrastar
el historial con la sección 1.1, punto por punto, sobre la salida de `git show --stat HEAD`.
Ahí vio el error y me lo reportó antes de hacer más commits.

**Cómo se corrigió.**

1. Corrigió la nota en `contexto/decisiones.md`.
2. Con mi autorización, dividió el commit en tres commits atómicos con asunto en
   imperativo. Conservó la fecha de autor original (2026-09-14) y el contenido exacto:

   ```
   c5436af docs: agrega README del proyecto
   f3a0709 chore: crea carpetas backend, frontend y docs
   b098283 chore: agrega .gitignore para .NET y Node
   ```

3. Antes de reemplazar `main`, comprobó que el árbol final era el mismo:

   ```
   árbol de e4f3f4f: 9d15be8ef8fd58b3ef19e05705b9854ffa92868b
   árbol nuevo:      9d15be8ef8fd58b3ef19e05705b9854ffa92868b
   ```

4. Lo publicó con `git push --force-with-lease=main:e4f3f4f… origin main`, que solo
   sobrescribe si el remoto sigue en el commit esperado:

   ```
    + e4f3f4f...c5436af main -> main (forced update)
   ```

## Caso B — intentó leer el PDF con herramientas que no estaban instaladas

**Qué le pedí.** `@"C:\Users\User\Downloads\Asignacion1_ControlVersiones_PIII.docx.pdf"`
y «la fecha de entrega es domingo 27 de septiembre del 2026».

**Qué devolvió.** Primero intentó leer el PDF por rango de páginas y después con
librerías de Python. Los tres intentos fallaron:

```
pdftoppm is not installed. Install poppler-utils (...) to enable PDF page rendering.
ModuleNotFoundError: No module named 'pypdf'
ModuleNotFoundError: No module named 'fitz'
```

**Cómo se detectó.** Por los propios mensajes de error.

**Cómo se corrigió.** El PDF tiene solo 3 páginas, y leerlo completo sin indicar rango
funcionó. El resumen del enunciado y la fecha quedaron en el README de la asignación.
Lección: probar primero la vía más simple antes de buscar dependencias.

## Verificaciones hechas en la sesión

```
$ git log --format=%s main | awk '{ print length($0) ": " $0 }'
32: docs: agrega README del proyecto
45: chore: crea carpetas backend, frontend y docs
41: chore: agrega .gitignore para .NET y Node

$ git log main -S"Password=" --oneline            (sin resultados)
$ git log main -S"ConnectionStrings" --oneline    (sin resultados)
$ git log main -S"POSTGRES_PASSWORD" --oneline    (sin resultados)
```

## Pendiente

- Pull requests con la pareja (secciones 1.2 y 1.3): se agregan a esta bitácora cuando estén.
