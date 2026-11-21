#!/usr/bin/env bash
# Asignación 3 — reproduce los cuatro hallazgos (1 a 4) y los casos revisados sin hallazgo (5) contra el proyecto levantado (http://localhost:8080) con
# `cp .env.example .env && docker compose up --build -d`. Se corre en Git Bash desde la raíz del clon:
#   bash docs/evidencias/asignacion-3/reproducir.sh 1   (o 2, 3, 4, 5)
# Cada corrida usa datos nuevos (sufijo con la hora), así se puede repetir sobre la misma base.
set -uo pipefail
API=http://localhost:8080/api; J='Content-Type: application/json'; S=$(date +%s)
token() { curl -s $API/acceso/sesion -H "$J" -d "{\"correo\":\"$1\",\"contrasena\":\"$2\"}" | sed -nE 's/.*"token":"([^"]+)".*/\1/p'; }
cuenta() {
  curl -s $API/acceso/registro -H "$J" -d "{\"nombre\":\"$1\",\"correo\":\"$2\",\"contrasena\":\"clave1234\"}"; echo
  for i in $(seq 20); do ID=$(curl -s "http://localhost:8080/correo/api/v1/search?query=to:$2" | grep -o '"ID":"[^"]*"' | head -1 | cut -d'"' -f4); [ -n "$ID" ] && break; sleep 1; done
  curl -s $API/acceso/activar -H "$J" -d "{\"token\":\"$(curl -s http://localhost:8080/correo/view/$ID.txt | grep -o 'token=[A-Za-z0-9_-]*' | cut -d= -f2)\"}"; echo
}
primer_id() { tee /dev/stderr | grep -o '"id":[0-9]*' | head -1 | cut -d: -f2; }
AD="Authorization: Bearer $(token admin@inventario.local Admin12345)"
paso() { printf '\n$ %s\n' "$*"; }

case "${1:-}" in
1)
  CORREO="prueba$S.@ejemplo.com"
  paso "registro con $CORREO"
  curl -s -w ' [%{http_code}]\n' $API/acceso/registro -H "$J" -d "{\"nombre\":\"Prueba Punto\",\"correo\":\"$CORREO\",\"contrasena\":\"clave1234\"}"
  for t in 15 45 90; do
    sleep $((t - ${prev:-0})); prev=$t
    paso "a los ${t}s: GET /api/cola-correos (solo el correo de $CORREO)"
    curl -s $API/cola-correos -H "$AD" | grep -o "{[^{}]*\"destinatario\":\"$CORREO\"[^{}]*}"
  done
  ;;
2)
  CORREO="sim$S@ejemplo.com"
  paso "cuenta Estándar $CORREO"; cuenta "Simultanea $S" "$CORREO"
  T="Authorization: Bearer $(token "$CORREO" clave1234)"
  cola_admin() { curl -s $API/cola-correos -H "$AD" | grep -o '"destinatario":"admin@inventario.local","asunto":"Nueva solicitud de permiso: ListarUsuarios"' | wc -l; }
  ANTES=$(cola_admin)
  # curl -Z manda las dos peticiones a la vez desde un mismo proceso (con dos curl en segundo plano la carrera sale a veces).
  paso "curl -Z: dos POST /api/solicitudes {\"permiso\":\"ListarUsuarios\"} a la vez"
  curl -s -Z --parallel-immediate -o /dev/null -w '%{http_code}\n' -H "$T" -H "$J" -d '{"permiso":"ListarUsuarios"}' \
    $API/solicitudes $API/solicitudes
  paso "GET /api/solicitudes/mias (id y estado de cada una)"
  curl -s $API/solicitudes/mias -H "$T" | grep -o '"id":[0-9]*,"solicitanteId"\|"estado":"[A-Za-z]*"' | sed 's/,"solicitanteId"//' | paste -sd' '
  paso "avisos «Nueva solicitud» del Administrador por $CORREO"
  curl -s $API/notificaciones -H "$AD" | grep -o "($CORREO) pidió el permiso ListarUsuarios" | wc -l
  paso "correos nuevos en la cola para el Administrador por estas dos peticiones"
  echo $(( $(cola_admin) - ANTES ))
  ;;
3)
  PROV=$(curl -s $API/proveedores -H "$AD" -H "$J" -d "{\"nombre\":\"Molinos $S\",\"telefono\":\"809-555-0101\",\"correo\":\"ventas$S@molinos.com\"}" | primer_id); echo
  PROD=$(curl -s $API/productos -H "$AD" -H "$J" -d "{\"nombre\":\"Arroz $S\",\"sku\":\"ARR-$S\",\"unidadMedida\":\"saco\",\"puntoReorden\":5}" | primer_id); echo
  ORD=$(curl -s $API/ordenes -H "$AD" -H "$J" -d "{\"proveedorId\":$PROV}" | primer_id); echo
  curl -s $API/ordenes/$ORD/lineas -H "$AD" -H "$J" -d "{\"productoId\":$PROD,\"cantidad\":10,\"costoUnitario\":850}" >/dev/null
  curl -s $API/ordenes/$ORD/estado -H "$AD" -H "$J" -d '{"hacia":"Enviada"}' >/dev/null
  curl -s $API/ordenes/$ORD/estado -H "$AD" -H "$J" -d '{"hacia":"Recibida"}' | grep -o '"estado":"Recibida"'
  # La orden se recibe ahora; para ver qué pasa con una recepción de noche se mueve su hora a las 22:30 del
  # 31 de octubre en Santo Domingo (UTC-4). Es la única escritura directa en la base.
  paso "UPDATE recibida_en = '2026-10-31 22:30:00-04' (orden $ORD)"
  docker compose exec -T db psql -U inventario -d inventario -c \
    "UPDATE inventario.ordenes_de_compra SET recibida_en = '2026-10-31 22:30:00-04' WHERE id = $ORD;"
  paso "GET /api/ordenes/$ORD (recibidaEn)"
  curl -s $API/ordenes/$ORD -H "$AD" | grep -o '"recibidaEn":"[^"]*"'
  for r in "2026-10-01&hasta=2026-10-31" "2026-11-01&hasta=2026-11-30"; do
    paso "GET /api/reportes/compras-por-proveedor?desde=$r (fila de Molinos $S)"
    curl -s "$API/reportes/compras-por-proveedor?desde=$r" -H "$AD" | grep -o "{[^{}]*\"proveedor\":\"Molinos $S\"[^{}]*}" || echo "(Molinos $S no aparece)"
  done
  ;;
4)
  CORREO="pref$S@ejemplo.com"
  paso "cuenta Estándar $CORREO (sin fila de preferencias todavía)"; cuenta "Preferencias $S" "$CORREO"
  T="Authorization: Bearer $(token "$CORREO" clave1234)"
  paso "curl -Z: dos PUT /api/notificaciones/preferencias {\"correo\":false} a la vez"
  curl -s -Z --parallel-immediate -w ' [%{http_code}]\n' -X PUT -H "$T" -H "$J" -d '{"correo":false}' \
    $API/notificaciones/preferencias $API/notificaciones/preferencias
  paso "GET /api/notificaciones/preferencias"
  curl -s -w ' [%{http_code}]\n' $API/notificaciones/preferencias -H "$T"
  ;;
5)
  # Casos que los criterios no cubrían y que no dieron hallazgo: entradas vacías o fuera de rango, y un rango sin datos.
  for r in "desde=&hasta=" "desde=2026-11-01" "desde=2026-11-01&hasta=2026-02-30" "desde=2026-11-01&hasta=9999-12-31" \
           "desde=2026-12-01&hasta=2026-11-01" "desde=2024-01-01&hasta=2026-12-31" "desde=1990-01-01&hasta=1990-12-31"; do
    for rep in solicitudes compras-por-proveedor; do
      paso "GET /api/reportes/$rep?$r"
      curl -s -w ' [%{http_code}]\n' "$API/reportes/$rep?$r" -H "$AD"
    done
  done
  paso "GET /api/notificaciones?estado=Todas  y  POST /api/notificaciones/99999999999/leida"
  curl -s -w ' [%{http_code}]\n' "$API/notificaciones?estado=Todas" -H "$AD"
  curl -s -w ' [%{http_code}]\n' -X POST "$API/notificaciones/99999999999/leida" -H "$AD"
  paso "PUT /api/notificaciones/preferencias sin cuerpo y con {\"correo\":\"no\"}"
  curl -s -w ' [%{http_code}]\n' -X PUT "$API/notificaciones/preferencias" -H "$AD" -H "$J"
  curl -s -w ' [%{http_code}]\n' -X PUT "$API/notificaciones/preferencias" -H "$AD" -H "$J" -d '{"correo":"no"}'
  paso "GET /api/cola-correos?estado=Perdido"
  curl -s -w ' [%{http_code}]\n' "$API/cola-correos?estado=Perdido" -H "$AD"
  ;;
*) echo "uso: $0 1|2|3|4|5"; exit 2 ;;
esac
