import { NavLink, Route, Routes } from 'react-router'
import Activar from './paginas/Activar'
import IniciarSesion from './paginas/IniciarSesion'
import Inicio from './paginas/Inicio'
import MiCuenta from './paginas/MiCuenta'
import ReenviarActivacion from './paginas/ReenviarActivacion'
import Registro from './paginas/Registro'
import Usuarios from './paginas/Usuarios'
import { useSesion } from './sesion'

export default function App() {
  const { usuario } = useSesion()

  return (
    <>
      <header className="barra">
        <strong>Inventario</strong>
        <nav>
          <NavLink to="/" end>
            Inicio
          </NavLink>
          {usuario ? (
            <>
              <NavLink to="/cuenta">Mi cuenta</NavLink>
              {usuario.rol === 'Administrador' && <NavLink to="/usuarios">Usuarios</NavLink>}
            </>
          ) : (
            <>
              <NavLink to="/iniciar-sesion">Iniciar sesión</NavLink>
              <NavLink to="/registro">Crear cuenta</NavLink>
            </>
          )}
        </nav>
        {usuario && (
          <span>
            {usuario.nombre} ({usuario.rol})
          </span>
        )}
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Inicio />} />
          <Route path="/registro" element={<Registro />} />
          <Route path="/activar" element={<Activar />} />
          <Route path="/reenviar-activacion" element={<ReenviarActivacion />} />
          <Route path="/iniciar-sesion" element={<IniciarSesion />} />
          <Route path="/cuenta" element={<MiCuenta />} />
          <Route path="/usuarios" element={<Usuarios />} />
          <Route path="*" element={<p>Esta página no existe.</p>} />
        </Routes>
      </main>
    </>
  )
}
