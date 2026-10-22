import Home from './pages/Home.jsx';
import History from './pages/History'
import './App.css'
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom'

function App() {
  return (
    <BrowserRouter>
      <nav className="main-nav">
        <Link to="/">Kontenery</Link>
        <Link to="/history">Historia logów</Link>
      </nav>

      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/history" element={<History />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App