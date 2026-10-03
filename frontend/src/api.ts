// Cliente mínimo de la API. Toda regla vive en el servidor: aquí solo se envía y se muestra la respuesta.

export class ErrorApi extends Error {
  readonly estado: number

  constructor(estado: number, mensaje: string) {
    super(mensaje)
    this.estado = estado
  }
}

const CLAVE_SESION = 'inventario.sesion'

export function leerToken(): string | null {
  return localStorage.getItem(CLAVE_SESION)
}

export function guardarToken(token: string | null) {
  if (token) localStorage.setItem(CLAVE_SESION, token)
  else localStorage.removeItem(CLAVE_SESION)
}

export interface Mensaje {
  mensaje: string
}

export async function llamar<T = Mensaje>(metodo: string, ruta: string, cuerpo?: unknown): Promise<T> {
  const cabeceras: Record<string, string> = {}
  const token = leerToken()
  if (token) cabeceras.Authorization = `Bearer ${token}`
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json'

  let respuesta: Response
  try {
    respuesta = await fetch(`/api${ruta}`, {
      method: metodo,
      headers: cabeceras,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
    })
  } catch {
    throw new ErrorApi(0, 'No se pudo conectar con el servidor.')
  }

  const texto = await respuesta.text()
  let datos: unknown = null
  try {
    datos = texto ? JSON.parse(texto) : null
  } catch {
    datos = null
  }

  if (!respuesta.ok) {
    const problema = datos as { detail?: string; title?: string } | null
    throw new ErrorApi(respuesta.status, problema?.detail ?? problema?.title ?? `Error ${respuesta.status}`)
  }
  return datos as T
}

export function mensajeDe(error: unknown): string {
  return error instanceof Error ? error.message : 'Ocurrió un error inesperado.'
}
