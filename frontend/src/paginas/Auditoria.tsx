import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface Registro {
  id: number
  fechaHora: string
  usuarioId: number
  usuarioCorreo: string
  usuarioRol: string
  accion: string
  entidad: string
  entidadId: string
  valorAnterior: Record<string, unknown>
  valorNuevo: Record<string, unknown>
}

interface Historial {
  total: number
  registros: Registro[]
}

const ENTIDADES = ['Usuario', 'SolicitudPermiso', 'Documento']

// La hora se guarda en UTC (RD-11); aquí se muestra en la del negocio, que es la que usan los filtros de fecha.
const enSantoDomingo = (valor: string) =>
  new Date(valor).toLocaleString('es-DO', { timeZone: 'America/Santo_Domingo' })

const valores = (v: Record<string, unknown>) =>
  Object.entries(v).map(([campo, valor]) => (
    <div key={campo}>
      <span className="nota">{campo}:</span> {String(valor)}
    </div>
  ))

// Historial de auditoría (RF-AUD-07): solo para el Administrador. Esta pantalla solo arma los filtros y muestra lo que
// devuelve la API: quién puede consultar y qué filtro es válido lo decide el servidor (RD-06). No hay nada que editar.
export default function Auditoria() {
  const { usuario, cargando } = useSesion()
  const [filtros, setFiltros] = useState({ usuario: '', entidad: '', entidadId: '', desde: '', hasta: '' })
  const [historial, setHistorial] = useState<Historial | null>(null)
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)

  const consultar = useCallback(async (f: typeof filtros) => {
    setAviso(null)
    try {
      const parametros = new URLSearchParams(Object.entries(f).filter(([, valor]) => valor.trim() !== ''))
      setHistorial(await llamar<Historial>('GET', `/auditoria?${parametros}`))
    } catch (error) {
      setHistorial(null)
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [])

  const esAdministrador = usuario?.rol === 'Administrador'
  useEffect(() => {
    if (esAdministrador) void consultar({ usuario: '', entidad: '', entidadId: '', desde: '', hasta: '' })
  }, [esAdministrador, consultar])

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />
  if (!esAdministrador) return <p>El historial de auditoría es solo para Administradores.</p>

  const cambiar = (campo: keyof typeof filtros) => (e: { target: { value: string } }) =>
    setFiltros({ ...filtros, [campo]: e.target.value })

  function enviar(e: FormEvent) {
    e.preventDefault()
    void consultar(filtros)
  }

  return (
    <section>
      <h1>Historial de auditoría</h1>
      <p className="nota">Quién cambió qué y cuándo. El historial no se puede editar ni borrar.</p>
      <Aviso aviso={aviso} />
      <form onSubmit={enviar}>
        <label>
          Usuario (id o correo)
          <input value={filtros.usuario} onChange={cambiar('usuario')} placeholder="ana@ejemplo.com" />
        </label>
        <label>
          Entidad
          <select value={filtros.entidad} onChange={cambiar('entidad')}>
            <option value="">Todas</option>
            {ENTIDADES.map((e) => (
              <option key={e}>{e}</option>
            ))}
          </select>
        </label>
        <label>
          Id de la entidad
          <input value={filtros.entidadId} onChange={cambiar('entidadId')} placeholder="3" />
        </label>
        <label>
          Desde (hora de Santo Domingo)
          <input type="date" value={filtros.desde} onChange={cambiar('desde')} />
        </label>
        <label>
          Hasta (incluido)
          <input type="date" value={filtros.hasta} onChange={cambiar('hasta')} />
        </label>
        <button type="submit">Consultar</button>
      </form>

      {historial && (
        <>
          <p className="nota">
            {historial.total} registro(s){historial.total > historial.registros.length && `; se muestran los ${historial.registros.length} más recientes`}.
          </p>
          {historial.registros.length === 0 ? (
            <p>No hay registros con esos filtros.</p>
          ) : (
            <div className="tabla">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Fecha y hora</th>
                    <th>Usuario</th>
                    <th>Acción</th>
                    <th>Entidad</th>
                    <th>Valor anterior</th>
                    <th>Valor nuevo</th>
                  </tr>
                </thead>
                <tbody>
                  {historial.registros.map((r) => (
                    <tr key={r.id}>
                      <td>{r.id}</td>
                      <td title={`${r.fechaHora} (UTC)`}>{enSantoDomingo(r.fechaHora)}</td>
                      <td>
                        {r.usuarioCorreo}
                        <div className="nota">
                          id {r.usuarioId}, {r.usuarioRol}
                        </div>
                      </td>
                      <td>{r.accion}</td>
                      <td>
                        {r.entidad} {r.entidadId}
                      </td>
                      <td>{valores(r.valorAnterior)}</td>
                      <td>{valores(r.valorNuevo)}</td>
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
