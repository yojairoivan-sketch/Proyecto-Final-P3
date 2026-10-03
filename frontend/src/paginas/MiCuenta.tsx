import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import CambiarContrasena from '../componentes/CambiarContrasena'
import { useSesion } from '../sesion'

export default function MiCuenta() {
  const { usuario, cargando, olvidar } = useSesion()
  const navegar = useNavigate()
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />

  async function cerrarSesion() {
    try {
      await llamar('DELETE', '/acceso/sesion')
      olvidar()
      navegar('/iniciar-sesion')
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }

  return (
    <section>
      <h1>Mi cuenta</h1>
      <table>
        <tbody>
          <tr>
            <th>Nombre</th>
            <td>{usuario.nombre}</td>
          </tr>
          <tr>
            <th>Correo</th>
            <td>{usuario.correo}</td>
          </tr>
          <tr>
            <th>Rol</th>
            <td>{usuario.rol}</td>
          </tr>
        </tbody>
      </table>
      <p>
        <button onClick={cerrarSesion}>Cerrar sesión</button>
      </p>
      <Aviso aviso={aviso} />
      <CambiarContrasena />
    </section>
  )
}
