import { NavLink, Route, Routes } from 'react-router'
import Inicio from './paginas/Inicio'

export default function App() {
  return (
    <>
      <header className="barra">
        <strong>Inventario</strong>
        <nav>
          <NavLink to="/">Inicio</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Inicio />} />
          <Route path="*" element={<p>Esta página no existe.</p>} />
        </Routes>
      </main>
    </>
  )
}
