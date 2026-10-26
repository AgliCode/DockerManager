import Home from './pages/Home.jsx';
import History from './pages/History';
import Monitoring from './pages/Monitoring';
import './App.css'
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom'

function App() {
  return (
    <BrowserRouter>
      <nav className="main-nav">
        <Link to="/">Kontenery</Link>
        <Link to="/history">Historia</Link>
        <Link to="/monitoring">Monitoring</Link>
      </nav>

      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/history" element={<History />} />
        <Route path="/monitoring" element={<Monitoring />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App