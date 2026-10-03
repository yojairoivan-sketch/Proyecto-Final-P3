import { useEffect, useState } from 'react'

export default function Inicio() {
  const [api, setApi] = useState('comprobando…')

  useEffect(() => {
    fetch('/api/salud')
      .then((r) => setApi(r.ok ? 'conectada' : 'no responde'))
      .catch(() => setApi('no responde'))
  }, [])

  return (
    <section>
      <h1>Inventario para negocio pequeño</h1>
      <p>Proyecto final de Programación III (ITLA, 2026-C-3).</p>
      <p className="nota">API: {api}</p>
    </section>
  )
}
