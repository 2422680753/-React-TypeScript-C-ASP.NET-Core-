import { Layout as AntLayout, Menu, theme, Avatar, Dropdown, Badge, Button } from 'antd'
import {
  DashboardOutlined,
  DeviceHubOutlined,
  AlertOutlined,
  SettingOutlined,
  BarChartOutlined,
  ControlOutlined,
  UserOutlined,
  LogoutOutlined,
  BellOutlined,
  WifiOutlined,
  WifiOffOutlined,
} from '@ant-design/icons'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { useAuthStore, useAlarmStore } from '@/store'
import { signalRService } from '@/services/signalr'

const { Header, Sider, Content } = AntLayout

const Layout = () => {
  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken()

  const navigate = useNavigate()
  const location = useLocation()
  const { user, logout } = useAuthStore()
  const { unreadCount } = useAlarmStore()

  const [isSignalRConnected, setIsSignalRConnected] = useState(false)
  const [collapsed, setCollapsed] = useState(false)

  useEffect(() => {
    const unsubscribeConnected = signalRService.onConnected(() => {
      setIsSignalRConnected(true)
    })

    const unsubscribeDisconnected = signalRService.onDisconnected(() => {
      setIsSignalRConnected(false)
    })

    setIsSignalRConnected(signalRService.isConnected())

    return () => {
      unsubscribeConnected()
      unsubscribeDisconnected()
    }
  }, [])

  const menuItems = [
    {
      key: '/dashboard',
      icon: <DashboardOutlined />,
      label: '实时看板',
    },
    {
      key: '/devices',
      icon: <DeviceHubOutlined />,
      label: '设备管理',
    },
    {
      key: '/alarms',
      icon: (
        <Badge count={unreadCount} size="small">
          <AlertOutlined />
        </Badge>
      ),
      label: '告警中心',
    },
    {
      key: '/alarm-rules',
      icon: <SettingOutlined />,
      label: '告警规则',
    },
    {
      key: '/control',
      icon: <ControlOutlined />,
      label: '远程控制',
    },
    {
      key: '/reports',
      icon: <BarChartOutlined />,
      label: '运维报表',
    },
  ]

  const userMenuItems = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: user?.username,
      disabled: true,
    },
    {
      key: 'role',
      label: `角色: ${getRoleName(user?.role)}`,
      disabled: true,
    },
    { type: 'divider' },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: '退出登录',
      danger: true,
    },
  ]

  const handleMenuClick = ({ key }: { key: string }) => {
    navigate(key)
  }

  const handleUserMenuClick = ({ key }: { key: string }) => {
    if (key === 'logout') {
      logout()
      navigate('/login')
    }
  }

  function getRoleName(role?: string): string {
    const roleMap: Record<string, string> = {
      User: '普通用户',
      Operator: '操作员',
      Admin: '管理员',
      SuperAdmin: '超级管理员',
    }
    return roleMap[role || 'User']
  }

  return (
    <AntLayout style={{ minHeight: '100vh' }}>
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={(value) => setCollapsed(value)}
        theme="dark"
        width={240}
      >
        <div
          style={{
            height: 64,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: 'rgba(255, 255, 255, 0.1)',
          }}
        >
          <DeviceHubOutlined style={{ fontSize: 24, color: '#1890ff' }} />
          {!collapsed && (
            <span
              style={{
                color: '#fff',
                fontSize: 18,
                fontWeight: 'bold',
                marginLeft: 8,
              }}
            >
              物联网监控
            </span>
          )}
        </div>

        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={handleMenuClick}
        />
      </Sider>

      <AntLayout>
        <Header
          style={{
            padding: '0 24px',
            background: colorBgContainer,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            boxShadow: '0 2px 8px rgba(0, 0, 0, 0.1)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Button
              icon={
                isSignalRConnected ? (
                  <WifiOutlined style={{ color: '#52c41a' }} />
                ) : (
                  <WifiOffOutlined style={{ color: '#ff4d4f' }} />
                )
              }
              type="text"
            >
              <span
                style={{
                  color: isSignalRConnected ? '#52c41a' : '#ff4d4f',
                }}
              >
                {isSignalRConnected ? '实时连接中' : '连接断开'}
              </span>
            </Button>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Badge count={unreadCount}>
              <Button
                icon={<BellOutlined />}
                type="text"
                onClick={() => navigate('/alarms')}
              />
            </Badge>

            <Dropdown
              menu={{ items: userMenuItems, onClick: handleUserMenuClick }}
              placement="bottomRight"
            >
              <div
                style={{
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                }}
              >
                <Avatar icon={<UserOutlined />} />
                <span>{user?.username}</span>
              </div>
            </Dropdown>
          </div>
        </Header>

        <Content
          style={{
            margin: '24px',
            padding: 24,
            minHeight: 280,
            background: colorBgContainer,
            borderRadius: borderRadiusLG,
          }}
        >
          <Outlet />
        </Content>
      </AntLayout>
    </AntLayout>
  )
}

export default Layout
