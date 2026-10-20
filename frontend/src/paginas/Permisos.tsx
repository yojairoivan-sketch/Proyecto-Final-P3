import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface PermisoSolicitable {
  nombre: string
  descripcion: string
}

interface Solicitud {
  id: number
  solicitanteId: number
  solicitanteNombre: string
  solicitanteCorreo: string
  permiso: string
  justificacion: string | null
  estado: 'Pendiente' | 'Aprobada' | 'Aplicada' | 'Rechazada'
  creadaEn: string
  resueltaPorNombre: string | null
  resueltaEn: string | null
  motivoRechazo: string | null
  aplicadaEn: string | null
}

function fecha(valor: string | null) {
  return valor ? new Date(valor).toLocaleString() : '—'
}

// Pedir permisos y, para un Administrador, resolver los pendientes. Todas las reglas las aplica el servidor.
export default function Permisos() {
  const { usuario, cargando, refrescar } = useSesion()
  const [catalogo, setCatalogo] = useState<PermisoSolicitable[]>([])
  const [mias, setMias] = useState<Solicitud[]>([])
  const [pendientes, setPendientes] = useState<Solicitud[]>([])
  const [permiso, setPermiso] = useState('')
  const [justificacion, setJustificacion] = useState('')
  const [motivos, setMotivos] = useState<Record<number, string>>({})
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const esAdministrador = usuario?.rol === 'Administrador'

  const cargar = useCallback(async () => {
    try {
      setCatalogo(await llamar<PermisoSolicitable[]>('GET', '/permisos/solicitables'))
      setMias(await llamar<Solicitud[]>('GET', '/solicitudes/mias'))
      if (esAdministrador) setPendientes(await llamar<Solicitud[]>('GET', '/solicitudes/pendientes'))
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [esAdministrador])

  useEffect(() => {
    if (usuario) void cargar()
  }, [usuario, cargar])

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />

  async function ejecutar(metodo: string, ruta: string, cuerpo?: unknown) {
    setAviso(null)
    try {
      const r = await llamar(metodo, ruta, cuerpo)
      setAviso({ tipo: 'exito', texto: r.mensaje })
      return true
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
      return false
    } finally {
      await cargar()
      await refrescar()
    }
  }

  async function pedir(e: FormEvent) {
    e.preventDefault()
    if (await ejecutar('POST', '/solicitudes', { permiso, justificacion })) {
      setPermiso('')
      setJustificacion('')
    }
  }

  return (
    <section>
      <h1>Permisos</h1>
      <Aviso aviso={aviso} />

      <h2>Pedir un permiso</h2>
      <form onSubmit={pedir}>
        <label>
          Permiso
          <select value={permiso} onChange={(e) => setPermiso(e.target.value)}>
            <option value="">Elige uno…</option>
            {catalogo.map((p) => (
              <option key={p.nombre} value={p.nombre}>
                {p.nombre}: {p.descripcion}
              </option>
            ))}
          </select>
        </label>
        <label>
          Por qué lo necesitas (opcional)
          <textarea value={justificacion} maxLength={500} onChange={(e) => setJustificacion(e.target.value)} />
        </label>
        <button type="submit">Enviar solicitud</button>
      </form>

      <h2>Mis solicitudes</h2>
      {mias.length === 0 ? (
        <p>Todavía no has pedido ningún permiso.</p>
      ) : (
        <div className="tabla">
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Permiso</th>
                <th>Estado</th>
                <th>Creada</th>
                <th>Resuelta</th>
                <th>Motivo del rechazo</th>
              </tr>
            </thead>
            <tbody>
              {mias.map((s) => (
                <tr key={s.id}>
                  <td>{s.id}</td>
                  <td>{s.permiso}</td>
                  <td>{s.estado}</td>
                  <td>{fecha(s.creadaEn)}</td>
                  <td>
                    {s.resueltaEn ? `${fecha(s.resueltaEn)} por ${s.resueltaPorNombre}` : '—'}
                  </td>
                  <td>{s.motivoRechazo ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {esAdministrador && (
        <>
          <h2>Solicitudes pendientes</h2>
          {pendientes.length === 0 ? (
            <p>No hay solicitudes pendientes.</p>
          ) : (
            <div className="tabla">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Solicitante</th>
                    <th>Permiso</th>
                    <th>Justificación</th>
                    <th>Creada</th>
                    <th>Resolver</th>
                  </tr>
                </thead>
                <tbody>
                  {pendientes.map((s) => (
                    <tr key={s.id}>
                      <td>{s.id}</td>
                      <td>
                        {s.solicitanteNombre} ({s.solicitanteCorreo}){s.solicitanteId === usuario.id && ' (tú)'}
                      </td>
                      <td>{s.permiso}</td>
                      <td>{s.justificacion ?? '—'}</td>
                      <td>{fecha(s.creadaEn)}</td>
                      <td>
                        <div className="acciones">
                          <button onClick={() => ejecutar('POST', `/solicitudes/${s.id}/aprobar`)}>Aprobar</button>
                          <input
                            placeholder="Motivo del rechazo"
                            value={motivos[s.id] ?? ''}
                            onChange={(e) => setMotivos({ ...motivos, [s.id]: e.target.value })}
                          />
                          <button
                            className="secundario"
                            onClick={() => ejecutar('POST', `/solicitudes/${s.id}/rechazar`, { motivo: motivos[s.id] ?? '' })}
                          >
                            Rechazar
                          </button>
                        </div>
                      </td>
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
