import { useCallback, useEffect, useState } from 'react'
import { Navigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface Notificacion {
  id: number
  titulo: string
  mensaje: string
  leida: boolean
  creadaEn: string
  leidaEn: string | null
}

interface Bandeja {
  noLeidas: number
  notificaciones: Notificacion[]
}

interface Correo {
  id: number
  destinatario: string
  asunto: string
  estado: 'Pendiente' | 'Enviado' | 'Fallido'
  intentos: number
  creadoEn: string
  ultimoIntentoEn: string | null
  enviadoEn: string | null
  ultimoError: string | null
}

interface Cola {
  pendientes: number
  enviados: number
  fallidos: number
  correos: Correo[]
}

const fecha = (valor: string | null) => (valor ? new Date(valor).toLocaleString() : '—')

// La bandeja interna del usuario (RF-NOT-04 a 07) y, para un Administrador, la cola de correos (RF-NOT-11).
// Qué notificaciones ve y marca cada uno lo decide el servidor: esta pantalla solo muestra lo que la API devuelve.
export default function Notificaciones() {
  const { usuario, cargando } = useSesion()
  const [filtro, setFiltro] = useState('')
  const [bandeja, setBandeja] = useState<Bandeja | null>(null)
  const [correoActivo, setCorreoActivo] = useState<boolean | null>(null)
  const [filtroCola, setFiltroCola] = useState('')
  const [cola, setCola] = useState<Cola | null>(null)
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const esAdministrador = usuario?.rol === 'Administrador'

  const cargar = useCallback(async () => {
    try {
      setBandeja(await llamar<Bandeja>('GET', `/notificaciones${filtro ? `?estado=${filtro}` : ''}`))
      setCorreoActivo((await llamar<{ correo: boolean }>('GET', '/notificaciones/preferencias')).correo)
      if (esAdministrador)
        setCola(await llamar<Cola>('GET', `/cola-correos${filtroCola ? `?estado=${filtroCola}` : ''}`))
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [filtro, filtroCola, esAdministrador])

  useEffect(() => {
    if (usuario) void cargar()
  }, [usuario, cargar])

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />

  async function marcar(n: Notificacion) {
    setAviso(null)
    try {
      await llamar('POST', `/notificaciones/${n.id}/leida`)
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
    await cargar()
  }

  async function cambiarCorreo(activo: boolean) {
    setAviso(null)
    try {
      const r = await llamar('PUT', '/notificaciones/preferencias', { correo: activo })
      setAviso({ tipo: 'exito', texto: r.mensaje })
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
    await cargar()
  }

  return (
    <section>
      <h1>Notificaciones</h1>
      <Aviso aviso={aviso} />

      <p>
        {bandeja ? `Tienes ${bandeja.noLeidas} sin leer.` : ''}{' '}
        <label className="nota">
          Ver{' '}
          <select value={filtro} onChange={(e) => setFiltro(e.target.value)}>
            <option value="">todas</option>
            <option value="NoLeidas">solo las no leídas</option>
            <option value="Leidas">solo las leídas</option>
          </select>
        </label>
      </p>

      {!bandeja || bandeja.notificaciones.length === 0 ? (
        <p>No hay notificaciones.</p>
      ) : (
        <div className="tabla">
          <table>
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Aviso</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              {bandeja.notificaciones.map((n) => (
                <tr key={n.id}>
                  <td>{fecha(n.creadaEn)}</td>
                  <td>
                    {n.leida ? n.titulo : <strong>{n.titulo}</strong>}
                    <br />
                    {n.mensaje}
                  </td>
                  <td>
                    {n.leida ? (
                      `Leída el ${fecha(n.leidaEn)}`
                    ) : (
                      <button onClick={() => marcar(n)}>Marcar como leída</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2>Cómo recibirlas</h2>
      {correoActivo !== null && (
        <label className="nota">
          <input type="checkbox" checked={correoActivo} onChange={(e) => cambiarCorreo(e.target.checked)} /> Recibir también
          un correo por cada notificación. Las notificaciones siempre aparecen aquí.
        </label>
      )}

      {esAdministrador && cola && (
        <>
          <h2>Cola de correos</h2>
          <p>
            Pendientes: {cola.pendientes} · Enviados: {cola.enviados} · Fallidos: {cola.fallidos}{' '}
            <label className="nota">
              Ver{' '}
              <select value={filtroCola} onChange={(e) => setFiltroCola(e.target.value)}>
                <option value="">todos</option>
                <option value="Pendiente">Pendientes</option>
                <option value="Enviado">Enviados</option>
                <option value="Fallido">Fallidos</option>
              </select>
            </label>
          </p>
          {cola.correos.length === 0 ? (
            <p>No hay correos.</p>
          ) : (
            <div className="tabla">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Para</th>
                    <th>Asunto</th>
                    <th>Estado</th>
                    <th>Intentos</th>
                    <th>Creado</th>
                    <th>Último intento</th>
                    <th>Último error</th>
                  </tr>
                </thead>
                <tbody>
                  {cola.correos.map((c) => (
                    <tr key={c.id}>
                      <td>{c.id}</td>
                      <td>{c.destinatario}</td>
                      <td>{c.asunto}</td>
                      <td>{c.estado}</td>
                      <td>{c.intentos}</td>
                      <td>{fecha(c.creadoEn)}</td>
                      <td>{fecha(c.ultimoIntentoEn)}</td>
                      <td>{c.ultimoError ?? '—'}</td>
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
