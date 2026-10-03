import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'

export default function Registro() {
  const [nombre, setNombre] = useState('')
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      const r = await llamar('POST', '/acceso/registro', { nombre, correo, contrasena })
      setAviso({ tipo: 'exito', texto: r.mensaje })
      setContrasena('')
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <h1>Crear cuenta</h1>
      {/* noValidate: la validación la hace el servidor (RD-07), igual que si la petición se arma a mano. */}
      <form onSubmit={enviar} noValidate>
        <label>
          Nombre
          <input value={nombre} onChange={(e) => setNombre(e.target.value)} autoComplete="name" />
        </label>
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
            autoComplete="new-password"
          />
        </label>
        <small className="nota">De 8 a 128 caracteres, con letras y números.</small>
        <button disabled={enviando}>Crear cuenta</button>
      </form>
      <Aviso aviso={aviso} />
      <p className="nota">
        ¿No te llegó el enlace? <Link to="/reenviar-activacion">Pide uno nuevo</Link>.
      </p>
    </section>
  )
}
