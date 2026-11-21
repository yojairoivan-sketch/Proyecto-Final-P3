import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface ReporteSolicitudes {
  desde: string
  hasta: string
  soloLasMias: boolean
  total: number
  porEstado: { estado: string; cantidad: number }[]
  porMes: { mes: string; estado: string; cantidad: number }[]
}

interface ReporteCompras {
  pregunta: string
  soloLasMias: boolean
  ordenesRecibidas: number
  montoTotal: number
  proveedores: { proveedorId: number; proveedor: string; ordenes: number; montoTotal: number; promedioPorOrden: number }[]
}

// Fechas AAAA-MM-DD del calendario del negocio (hora de Santo Domingo), como las interpreta la API (RF-REP-05).
// Hallazgo 3: antes «hoy» salía de toISOString (UTC), y desde las 20:00 de Santo Domingo ya era mañana.
const hoy = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santo_Domingo' }).format(new Date())
const primeroDelMes = () => `${hoy().slice(0, 8)}01`
const dinero = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

// Los dos reportes (RF-REP-01 a 05). La agrupación y las sumas las hace el servidor en la base:
// esta pantalla solo pide el rango y muestra las filas que devuelve.
export default function Reportes() {
  const { usuario, cargando } = useSesion()
  const [desde, setDesde] = useState(primeroDelMes)
  const [hasta, setHasta] = useState(hoy)
  const [solicitudes, setSolicitudes] = useState<ReporteSolicitudes | null>(null)
  const [compras, setCompras] = useState<ReporteCompras | null>(null)
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)

  const consultar = useCallback(async (inicio: string, fin: string) => {
    setAviso(null)
    try {
      const rango = `desde=${encodeURIComponent(inicio)}&hasta=${encodeURIComponent(fin)}`
      setSolicitudes(await llamar<ReporteSolicitudes>('GET', `/reportes/solicitudes?${rango}`))
      setCompras(await llamar<ReporteCompras>('GET', `/reportes/compras-por-proveedor?${rango}`))
    } catch (error) {
      setSolicitudes(null)
      setCompras(null)
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [])

  useEffect(() => {
    if (usuario) void consultar(primeroDelMes(), hoy())
  }, [usuario, consultar])

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />

  function enviar(e: FormEvent) {
    e.preventDefault()
    void consultar(desde, hasta)
  }

  return (
    <section>
      <h1>Reportes</h1>
      <Aviso aviso={aviso} />
      <form onSubmit={enviar}>
        <label>
          Desde (hora de Santo Domingo)
          <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} />
        </label>
        <label>
          Hasta (incluido)
          <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} />
        </label>
        <button type="submit">Consultar</button>
      </form>

      {solicitudes && (
        <>
          <h2>Solicitudes de permiso por mes y estado</h2>
          <p className="nota">
            {solicitudes.soloLasMias ? 'Solo tus solicitudes.' : 'Las solicitudes de todos los usuarios.'} Total:{' '}
            {solicitudes.total}.
          </p>
          {solicitudes.porMes.length === 0 ? (
            <p>No hay solicitudes en ese rango.</p>
          ) : (
            <div className="tabla">
              <table>
                <thead>
                  <tr>
                    <th>Mes</th>
                    <th>Estado</th>
                    <th>Cantidad</th>
                  </tr>
                </thead>
                <tbody>
                  {solicitudes.porMes.map((f) => (
                    <tr key={`${f.mes}-${f.estado}`}>
                      <td>{f.mes}</td>
                      <td>{f.estado}</td>
                      <td>{f.cantidad}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {compras && (
        <>
          <h2>{compras.pregunta}</h2>
          <p className="nota">
            {compras.soloLasMias ? 'Solo las órdenes que creaste tú.' : 'Las órdenes de todos los usuarios.'} Órdenes
            recibidas: {compras.ordenesRecibidas}. Total: {dinero(compras.montoTotal)} DOP.
          </p>
          {compras.proveedores.length === 0 ? (
            <p>No se recibieron órdenes en ese rango.</p>
          ) : (
            <div className="tabla">
              <table>
                <thead>
                  <tr>
                    <th>Proveedor</th>
                    <th>Órdenes recibidas</th>
                    <th>Monto total (DOP)</th>
                    <th>Promedio por orden (DOP)</th>
                  </tr>
                </thead>
                <tbody>
                  {compras.proveedores.map((p) => (
                    <tr key={p.proveedorId}>
                      <td>{p.proveedor}</td>
                      <td>{p.ordenes}</td>
                      <td>{dinero(p.montoTotal)}</td>
                      <td>{dinero(p.promedioPorOrden)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  )
}
