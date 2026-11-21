#!/usr/bin/env bash
# Asignación 3 — cada prueba de regresión falla sobre el commit anterior a su corrección y pasa sobre la corrección.
# Uso, desde la raíz del repositorio (Git Bash, con el SDK de .NET 10):
#   bash docs/evidencias/asignacion-3/antes-y-despues.sh
# Para cada hallazgo: abre un worktree en el commit anterior a la corrección, le pone SOLO los archivos de prueba del
# commit de la corrección y corre la prueba del hallazgo (debe fallar); luego pasa el worktree a la corrección (debe pasar).
set -uo pipefail
RAIZ=$(git rev-parse --show-toplevel)
WT=$(mktemp -d)/a3
git worktree add -q --detach "$WT" HEAD

for N in 1 2 3 4; do
  C=$(git log --format=%h --grep="(hallazgo $N)" -1)
  PRUEBAS=$(git show --name-only --format= "$C" -- 'backend/tests/*')
  PROYECTO=$(grep -l "Hallazgo${N}_" $PRUEBAS | head -1 | cut -d/ -f1-3)
  echo "== Hallazgo $N: corrección $C, padre $(git rev-parse --short $C^), proyecto $PROYECTO"

  git -C "$WT" checkout -q --detach "$C^"
  git -C "$WT" checkout -q "$C" -- $PRUEBAS
  (cd "$WT" && dotnet test "$PROYECTO" --filter "FullyQualifiedName~Hallazgo${N}_" 2>&1 | grep -E "^(Passed|Failed)!|\[FAIL\]|Assert\.")
  echo "   ^ antes de la corrección (debe decir Failed)"

  git -C "$WT" checkout -q -f --detach "$C"
  (cd "$WT" && dotnet test "$PROYECTO" --filter "FullyQualifiedName~Hallazgo${N}_" 2>&1 | grep -E "^(Passed|Failed)!|\[FAIL\]")
  echo "   ^ con la corrección (debe decir Passed)"
done

git worktree remove --force "$WT"
