import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { llamar, mensajeDe } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'

// El enlace del correo abre esta página, que confirma la activación con un POST.
// Así un simple GET (por ejemplo, el antivirus que revisa los enlaces) no consume el token.
export default function Activar() {
  const [parametros] = useSearchParams()
  const token = parametros.get('token') ?? ''
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const enviado = useRef(false)

  useEffect(() => {
    if (enviado.current) return
    enviado.current = true
    llamar('POST', '/acceso/activar', { token })
      .then((r) => setAviso({ tipo: 'exito', texto: r.mensaje }))
      .catch((error) => setAviso({ tipo: 'error', texto: mensajeDe(error) }))
  }, [token])

  return (
    <section>
      <h1>Activar cuenta</h1>
      {aviso ? <Aviso aviso={aviso} /> : <p>Activando tu cuenta…</p>}
      <p className="nota">
        <Link to="/iniciar-sesion">Iniciar sesión</Link> · <Link to="/reenviar-activacion">Pedir otro enlace</Link>
      </p>
    </section>
  )
}
