import { NavLink, Route, Routes } from 'react-router'
import Activar from './paginas/Activar'
import Inicio from './paginas/Inicio'
import ReenviarActivacion from './paginas/ReenviarActivacion'
import Registro from './paginas/Registro'

export default function App() {
  return (
    <>
      <header className="barra">
        <strong>Inventario</strong>
        <nav>
          <NavLink to="/" end>
            Inicio
          </NavLink>
          <NavLink to="/registro">Crear cuenta</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Inicio />} />
          <Route path="/registro" element={<Registro />} />
          <Route path="/activar" element={<Activar />} />
          <Route path="/reenviar-activacion" element={<ReenviarActivacion />} />
          <Route path="*" element={<p>Esta página no existe.</p>} />
        </Routes>
      </main>
    </>
  )
}
