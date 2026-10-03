export interface EstadoAviso {
  tipo: 'exito' | 'error'
  texto: string
}

export default function Aviso({ aviso }: { aviso: EstadoAviso | null }) {
  if (!aviso) return null
  return (
    <p className={aviso.tipo} role={aviso.tipo === 'error' ? 'alert' : 'status'}>
      {aviso.texto}
    </p>
  )
}
