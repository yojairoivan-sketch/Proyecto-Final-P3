import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'

export default function Recuperar() {
  const [correo, setCorreo] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      const r = await llamar('POST', '/acceso/recuperacion', { correo })
      setAviso({ tipo: 'exito', texto: r.mensaje })
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <h1>Recuperar contraseña</h1>
      <p className="nota">Te enviaremos un código de un solo uso que vence en 30 minutos.</p>
      <form onSubmit={enviar} noValidate>
        <label>
          Correo
          <input type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} autoComplete="email" />
        </label>
        <button disabled={enviando}>Enviar código</button>
      </form>
      <Aviso aviso={aviso} />
      <p className="nota">
        ¿Ya tienes el código? <Link to="/restablecer">Define tu contraseña nueva</Link>.
      </p>
    </section>
  )
}
