import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

export default function Restablecer() {
  const [parametros] = useSearchParams()
  const { olvidar } = useSesion()
  const [codigo, setCodigo] = useState(parametros.get('codigo') ?? '')
  const [contrasenaNueva, setContrasenaNueva] = useState('')
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setAviso(null)
    try {
      const r = await llamar('POST', '/acceso/restablecer', { codigo, contrasenaNueva })
      // El servidor cerró todas las sesiones de la cuenta; si había una guardada aquí, ya no sirve.
      olvidar()
      setAviso({ tipo: 'exito', texto: r.mensaje })
      setContrasenaNueva('')
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <h1>Restablecer contraseña</h1>
      <form onSubmit={enviar} noValidate>
        <label>
          Código del correo
          <input value={codigo} onChange={(e) => setCodigo(e.target.value)} autoComplete="one-time-code" />
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
        <small className="nota">De 8 a 128 caracteres, con letras y números.</small>
        <button disabled={enviando}>Guardar contraseña</button>
      </form>
      <Aviso aviso={aviso} />
      <p className="nota">
        <Link to="/iniciar-sesion">Iniciar sesión</Link> · <Link to="/recuperar">Pedir otro código</Link>
      </p>
    </section>
  )
}
