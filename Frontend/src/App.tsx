import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useEffect } from 'react'
import { useAuthStore } from './store'
import { authApi } from './services/api'
import { signalRService } from './services/signalr'
import Layout from './components/Layout'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Devices from './pages/Devices'
import DeviceDetail from './pages/DeviceDetail'
import Alarms from './pages/Alarms'
import AlarmRules from './pages/AlarmRules'
import Control from './pages/Control'
import Reports from './pages/Reports'

function App() {
  const { isAuthenticated, setUser, setToken, logout } = useAuthStore()

  useEffect(() => {
    const token = localStorage.getItem('token')
    const storedUser = localStorage.getItem('user')

    if (token && storedUser) {
      try {
        const user = JSON.parse(storedUser)
        setUser(user)
        setToken(token)

        initSignalR()
        validateToken()
      } catch {
        logout()
      }
    }
  }, [])

  const initSignalR = async () => {
    try {
      await signalRService.start()
      await signalRService.subscribeToAllDevices()
      await signalRService.subscribeToAlarms()
    } catch (error) {
      console.error('Failed to connect to SignalR:', error)
    }
  }

  const validateToken = async () => {
    try {
      const user = await authApi.getCurrentUser()
      setUser(user)
      localStorage.setItem('user', JSON.stringify(user))
    } catch {
      logout()
    }
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={!isAuthenticated ? <Login /> : <Navigate to="/" />} />
        
        <Route path="/" element={isAuthenticated ? <Layout /> : <Navigate to="/login" />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="devices" element={<Devices />} />
          <Route path="devices/:id" element={<DeviceDetail />} />
          <Route path="alarms" element={<Alarms />} />
          <Route path="alarm-rules" element={<AlarmRules />} />
          <Route path="control" element={<Control />} />
          <Route path="reports" element={<Reports />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
