import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { ErrorApi, guardarToken, leerToken, llamar } from './api'

export interface Yo {
  id: number
  nombre: string
  correo: string
  rol: string
}

interface ContextoSesion {
  usuario: Yo | null
  cargando: boolean
  /** Guarda la credencial recibida al iniciar sesión y carga /yo. */
  abrir: (token: string) => Promise<void>
  /** Olvida la credencial local (después de cerrar sesión o si el servidor la rechaza). */
  olvidar: () => void
  refrescar: () => Promise<void>
}

const Contexto = createContext<ContextoSesion | null>(null)

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Yo | null>(null)
  const [cargando, setCargando] = useState(true)

  const refrescar = useCallback(async () => {
    if (!leerToken()) {
      setUsuario(null)
      setCargando(false)
      return
    }
    try {
      setUsuario(await llamar<Yo>('GET', '/yo'))
    } catch (error) {
      if (error instanceof ErrorApi && error.estado === 401) guardarToken(null)
      setUsuario(null)
    } finally {
      setCargando(false)
    }
  }, [])

  useEffect(() => {
    void refrescar()
  }, [refrescar])

  const abrir = useCallback(
    async (token: string) => {
      guardarToken(token)
      await refrescar()
    },
    [refrescar],
  )

  const olvidar = useCallback(() => {
    guardarToken(null)
    setUsuario(null)
  }, [])

  return <Contexto.Provider value={{ usuario, cargando, abrir, olvidar, refrescar }}>{children}</Contexto.Provider>
}

export function useSesion() {
  const contexto = useContext(Contexto)
  if (!contexto) throw new Error('useSesion debe usarse dentro de ProveedorSesion')
  return contexto
}
