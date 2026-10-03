import { useCallback, useEffect, useState } from 'react'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface UsuarioListado {
  id: number
  nombre: string
  correo: string
  rol: string
  estado: string
}

// La API decide quién puede hacer qué: si un Estándar entra aquí, ve el rechazo que devuelve el servidor.
export default function Usuarios() {
  const { usuario: yo, refrescar } = useSesion()
  const [usuarios, setUsuarios] = useState<UsuarioListado[]>([])
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)

  const cargar = useCallback(async () => {
    try {
      setUsuarios(await llamar<UsuarioListado[]>('GET', '/usuarios'))
    } catch (error) {
      setUsuarios([])
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [])

  useEffect(() => {
    void cargar()
  }, [cargar])

  async function ejecutar(metodo: string, ruta: string, cuerpo?: unknown) {
    setAviso(null)
    try {
      const r = await llamar(metodo, ruta, cuerpo)
      setAviso({ tipo: 'exito', texto: r.mensaje })
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
    await cargar()
    await refrescar()
  }

  return (
    <section>
      <h1>Usuarios</h1>
      <Aviso aviso={aviso} />
      <div className="tabla">
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Correo</th>
              <th>Rol</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {usuarios.map((u) => (
              <tr key={u.id}>
                <td>
                  {u.nombre}
                  {u.id === yo?.id && ' (tú)'}
                </td>
                <td>{u.correo}</td>
                <td>
                  <select
                    value={u.rol}
                    onChange={(e) => ejecutar('PUT', `/usuarios/${u.id}/rol`, { rol: e.target.value })}
                  >
                    <option>Administrador</option>
                    <option>Estándar</option>
                  </select>
                </td>
                <td>{u.estado}</td>
                <td>
                  <div className="acciones">
                    {u.estado === 'Activo' && (
                      <button className="secundario" onClick={() => ejecutar('POST', `/usuarios/${u.id}/desactivar`)}>
                        Desactivar
                      </button>
                    )}
                    {u.estado !== 'Pendiente de activación' && (
                      <button
                        className="secundario"
                        onClick={() => ejecutar('POST', `/usuarios/${u.id}/forzar-restablecimiento`)}
                      >
                        Forzar restablecimiento
                      </button>
                    )}
                    {u.estado === 'Desactivado' && (
                      <button className="secundario" onClick={() => ejecutar('POST', `/usuarios/${u.id}/reactivar`)}>
                        Reactivar
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
