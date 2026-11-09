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
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json'
  const respuesta = await pedir(metodo, ruta, cabeceras, cuerpo === undefined ? undefined : JSON.stringify(cuerpo))
  return leerJson<T>(respuesta)
}

/** Sube un archivo en un formulario multipart, en el campo «archivo». El navegador pone el Content-Type con su boundary. */
export async function subirArchivo<T>(ruta: string, archivo: File): Promise<T> {
  const formulario = new FormData()
  formulario.append('archivo', archivo)
  return leerJson<T>(await pedir('POST', ruta, {}, formulario))
}

/** Descarga con la credencial de sesión (un enlace normal no la manda) y la guarda con el nombre indicado. */
export async function descargarArchivo(ruta: string, nombre: string): Promise<void> {
  const respuesta = await pedir('GET', ruta, {})
  if (!respuesta.ok) await leerJson(respuesta)
  const enlace = document.createElement('a')
  enlace.href = URL.createObjectURL(await respuesta.blob())
  enlace.download = nombre
  enlace.click()
  // Se libera después: si se libera en el acto, algunos navegadores cancelan la descarga.
  setTimeout(() => URL.revokeObjectURL(enlace.href), 10_000)
}

async function pedir(metodo: string, ruta: string, cabeceras: Record<string, string>, cuerpo?: BodyInit): Promise<Response> {
  const token = leerToken()
  if (token) cabeceras.Authorization = `Bearer ${token}`
  try {
    return await fetch(`/api${ruta}`, { method: metodo, headers: cabeceras, body: cuerpo })
  } catch {
    throw new ErrorApi(0, 'No se pudo conectar con el servidor.')
  }
}

/** El cuerpo JSON de una respuesta correcta, o un ErrorApi con el mensaje del ProblemDetails. */
async function leerJson<T>(respuesta: Response): Promise<T> {
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
