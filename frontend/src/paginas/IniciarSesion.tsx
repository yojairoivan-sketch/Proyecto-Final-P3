import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface SesionIniciada {
  token: string
  venceEn: string
}

export default function IniciarSesion() {
  const { abrir } = useSesion()
  const navegar = useNavigate()
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      const sesion = await llamar<SesionIniciada>('POST', '/acceso/sesion', { correo, contrasena })
      await abrir(sesion.token)
      navegar('/cuenta')
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <h1>Iniciar sesión</h1>
      <form onSubmit={enviar} noValidate>
        <label>
          Correo
          <input type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} autoComplete="email" />
        </label>
        <label>
          Contraseña
          <input
            type="password"
            value={contrasena}
            onChange={(e) => setContrasena(e.target.value)}
            autoComplete="current-password"
          />
        </label>
        <button disabled={enviando}>Entrar</button>
      </form>
      <Aviso aviso={aviso} />
      <p className="nota">
        ¿No tienes cuenta? <Link to="/registro">Crea una</Link>.
      </p>
    </section>
  )
}
