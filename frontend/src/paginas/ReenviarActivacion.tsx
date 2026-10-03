import { useState, type FormEvent } from 'react'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'

export default function ReenviarActivacion() {
  const [correo, setCorreo] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      const r = await llamar('POST', '/acceso/reenviar-activacion', { correo })
      setAviso({ tipo: 'exito', texto: r.mensaje })
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <h1>Reenviar enlace de activación</h1>
      <p className="nota">El enlace anterior deja de servir cuando pides uno nuevo.</p>
      <form onSubmit={enviar} noValidate>
        <label>
          Correo
          <input type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} autoComplete="email" />
        </label>
        <button disabled={enviando}>Enviar enlace</button>
      </form>
      <Aviso aviso={aviso} />
    </section>
  )
}
