import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { descargarArchivo, llamar, mensajeDe, subirArchivo, type Mensaje } from '../api'
import Aviso, { type EstadoAviso } from '../componentes/Aviso'
import { useSesion } from '../sesion'

interface Documento {
  id: number
  nombre: string
  tipo: string
  tamanoBytes: number
  subidoEn: string
  subidoPorId: number
  subidoPorNombre: string
  eliminadoEn: string | null
}

function tamano(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// Subir, listar, descargar y eliminar documentos. Qué documentos ve cada usuario y quién puede descargarlos
// o eliminarlos lo decide el servidor: esta pantalla solo muestra lo que la API devuelve.
export default function Documentos() {
  const { usuario, cargando } = useSesion()
  const [documentos, setDocumentos] = useState<Documento[]>([])
  const [archivo, setArchivo] = useState<File | null>(null)
  const [verEliminados, setVerEliminados] = useState(false)
  const [aviso, setAviso] = useState<EstadoAviso | null>(null)
  const selector = useRef<HTMLInputElement>(null)
  const esAdministrador = usuario?.rol === 'Administrador'

  const cargar = useCallback(async () => {
    try {
      setDocumentos(await llamar<Documento[]>('GET', `/documentos${verEliminados ? '?eliminados=true' : ''}`))
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }, [verEliminados])

  useEffect(() => {
    if (usuario) void cargar()
  }, [usuario, cargar])

  if (cargando) return <p>Cargando…</p>
  if (!usuario) return <Navigate to="/iniciar-sesion" replace />

  async function subir(e: FormEvent) {
    e.preventDefault()
    setAviso(null)
    if (!archivo) {
      setAviso({ tipo: 'error', texto: 'Elige un archivo.' })
      return
    }
    try {
      const r = await subirArchivo<Mensaje>('/documentos', archivo)
      setAviso({ tipo: 'exito', texto: r.mensaje })
      setArchivo(null)
      if (selector.current) selector.current.value = ''
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
    await cargar()
  }

  async function descargar(d: Documento) {
    setAviso(null)
    try {
      await descargarArchivo(`/documentos/${d.id}/contenido`, d.nombre)
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
  }

  async function eliminar(d: Documento) {
    if (!window.confirm(`¿Eliminar «${d.nombre}»? Dejará de aparecer en el listado.`)) return
    setAviso(null)
    try {
      const r = await llamar('DELETE', `/documentos/${d.id}`)
      setAviso({ tipo: 'exito', texto: r.mensaje })
    } catch (error) {
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    }
    await cargar()
  }

  return (
    <section>
      <h1>Documentos</h1>
      <Aviso aviso={aviso} />

      <h2>Subir un documento</h2>
      <form onSubmit={subir}>
        <label>
          Archivo (hasta 10 MB)
          <input ref={selector} type="file" onChange={(e) => setArchivo(e.target.files?.[0] ?? null)} />
        </label>
        <button type="submit">Subir</button>
      </form>

      <h2>{esAdministrador ? 'Documentos de todos los usuarios' : 'Mis documentos'}</h2>
      {esAdministrador && (
        <label className="nota">
          <input type="checkbox" checked={verEliminados} onChange={(e) => setVerEliminados(e.target.checked)} /> Mostrar
          también los eliminados
        </label>
      )}
      {documentos.length === 0 ? (
        <p>No hay documentos.</p>
      ) : (
        <div className="tabla">
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Nombre</th>
                <th>Tipo</th>
                <th>Tamaño</th>
                <th>Subido</th>
                <th>Por</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {documentos.map((d) => (
                <tr key={d.id}>
                  <td>{d.id}</td>
                  <td>{d.nombre}</td>
                  <td>{d.tipo}</td>
                  <td>{tamano(d.tamanoBytes)}</td>
                  <td>{new Date(d.subidoEn).toLocaleString()}</td>
                  <td>
                    {d.subidoPorNombre}
                    {d.subidoPorId === usuario.id && ' (tú)'}
                  </td>
                  <td>
                    {d.eliminadoEn ? (
                      `Eliminado el ${new Date(d.eliminadoEn).toLocaleString()}`
                    ) : (
                      <div className="acciones">
                        <button onClick={() => descargar(d)}>Descargar</button>
                        <button className="secundario" onClick={() => eliminar(d)}>
                          Eliminar
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
