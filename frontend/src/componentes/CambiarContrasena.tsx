import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import { useSesion } from '../sesion'
import Aviso, { type EstadoAviso } from './Aviso'

export default function CambiarContrasena() {
  const { olvidar } = useSesion()
  const navegar = useNavigate()
  const [contrasenaActual, setContrasenaActual] = useState('')
  const [contrasenaNueva, setContrasenaNueva] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      await llamar('PUT', '/yo/contrasena', { contrasenaActual, contrasenaNueva })
      // El servidor cerró todas las sesiones, también esta: hay que volver a entrar con la nueva.
      olvidar()
      navegar('/iniciar-sesion')
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <>
      <h2>Cambiar contraseña</h2>
      <form onSubmit={enviar} noValidate>
        <label>
          Contraseña actual
          <input
            type="password"
            value={contrasenaActual}
            onChange={(e) => setContrasenaActual(e.target.value)}
            autoComplete="current-password"
          />
        </label>
        <label>
          Contraseña nueva
          <input
            type="password"
            value={contrasenaNueva}
            onChange={(e) => setContrasenaNueva(e.target.value)}
            autoComplete="new-password"
          />
        </label>
        <small className="nota">Al cambiarla se cierran todas tus sesiones, también esta.</small>
        <button disabled={enviando}>Cambiar contraseña</button>
      </form>
      <Aviso aviso={aviso} />
    </>
  )
}
