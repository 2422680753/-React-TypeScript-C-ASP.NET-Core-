import { Card, Row, Col, Statistic, Table, Tag, Empty, Spin, Typography, List } from 'antd'
import {
  DeviceHubOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  WarningOutlined,
  AlertOutlined,
} from '@ant-design/icons'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  AreaChart,
  Area,
  PieChart,
  Pie,
  Cell,
} from 'recharts'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { deviceApi, alarmApi, dataApi } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { useDeviceStore, useAlarmStore } from '@/store'
import type { Device, Alarm, RealTimeData, DeviceStatistics, AlarmStatistics } from '@/types'

const { Title } = Typography

const Dashboard = () => {
  const [loading, setLoading] = useState(true)
  const [devices, setDevices] = useState<Device[]>([])
  const [recentAlarms, setRecentAlarms] = useState<Alarm[]>([])
  const [deviceStats, setDeviceStats] = useState<DeviceStatistics | null>(null)
  const [alarmStats, setAlarmStats] = useState<AlarmStatistics | null>(null)
  const [chartData, setChartData] = useState<
    Array<{ time: string; temperature: number; humidity: number; power: number }>
  >([])
  const { updateRealTimeData, realTimeData } = useDeviceStore()
  const { addAlarm } = useAlarmStore()

  useEffect(() => {
    loadData()
    setupRealTimeListeners()
    const interval = setInterval(loadData, 30000)
    return () => clearInterval(interval)
  }, [])

  const loadData = async () => {
    setLoading(true)
    try {
      const [devicesData, stats, alarmStatsData, alarms] = await Promise.all([
        deviceApi.getAll(),
        deviceApi.getStatistics(),
        alarmApi.getStatistics(),
        alarmApi.getAll('Active', undefined, undefined, 10),
      ])

      setDevices(devicesData)
      setDeviceStats(stats)
      setAlarmStats(alarmStatsData)
      setRecentAlarms(alarms)

      generateChartData()

      if (devicesData.length > 0) {
        const sampleData = await dataApi.getLatestData(devicesData[0].id)
        updateRealTimeData(sampleData)
      }
    } catch (error) {
      console.error('Failed to load dashboard data:', error)
    } finally {
      setLoading(false)
    }
  }

  const setupRealTimeListeners = () => {
    const unsubDeviceData = signalRService.onDeviceData((data) => {
      updateRealTimeData(data)
      updateChartWithNewData(data)
    })

    const unsubAlarm = signalRService.onAlarm((alarm) => {
      addAlarm(alarm)
      setRecentAlarms((prev) => [alarm, ...prev].slice(0, 10))
    })

    const unsubDeviceStatus = signalRService.onDeviceStatusChange((status) => {
      setDevices((prev) =>
        prev.map((d) =>
          d.id === status.deviceId ? { ...d, status: status.status } : d
        )
      )
    })

    return () => {
      unsubDeviceData()
      unsubAlarm()
      unsubDeviceStatus()
    }
  }

  const generateChartData = () => {
    const now = dayjs()
    const data = []
    for (let i = 23; i >= 0; i--) {
      const time = now.subtract(i, 'hour').format('HH:00')
      data.push({
        time,
        temperature: Math.round(20 + Math.random() * 15),
        humidity: Math.round(40 + Math.random() * 40),
        power: Math.round(100 + Math.random() * 200),
      })
    }
    setChartData(data)
  }

  const updateChartWithNewData = (data: RealTimeData) => {
    const now = dayjs().format('HH:mm')
    const temperature = data.metrics['temperature'] || Math.round(20 + Math.random() * 15)
    const humidity = data.metrics['humidity'] || Math.round(40 + Math.random() * 40)
    const power = data.metrics['power'] || Math.round(100 + Math.random() * 200)

    setChartData((prev) => {
      const newData = [...prev.slice(1), { time: now, temperature, humidity, power }]
      return newData
    })
  }

  const getStatusColor = (status: string) => {
    const colors: Record<string, string> = {
      Online: 'success',
      Offline: 'default',
      Warning: 'warning',
      Error: 'error',
      Maintenance: 'processing',
    }
    return colors[status] || 'default'
  }

  const getStatusText = (status: string) => {
    const texts: Record<string, string> = {
      Online: '在线',
      Offline: '离线',
      Warning: '告警',
      Error: '错误',
      Maintenance: '维护中',
    }
    return texts[status] || status
  }

  const getAlarmLevelColor = (level: string) => {
    const colors: Record<string, string> = {
      Critical: 'error',
      Emergency: 'error',
      Warning: 'warning',
      Information: 'info',
    }
    return colors[level] || 'default'
  }

  const getAlarmLevelText = (level: string) => {
    const texts: Record<string, string> = {
      Critical: '严重',
      Emergency: '紧急',
      Warning: '警告',
      Information: '信息',
    }
    return texts[level] || level
  }

  const deviceColumns: ColumnsType<Device> = [
    {
      title: '设备名称',
      dataIndex: 'name',
      key: 'name',
      ellipsis: true,
    },
    {
      title: '设备类型',
      dataIndex: 'deviceType',
      key: 'deviceType',
      width: 120,
    },
    {
      title: '位置',
      dataIndex: 'location',
      key: 'location',
      ellipsis: true,
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => (
        <Tag color={getStatusColor(status)}>{getStatusText(status)}</Tag>
      ),
    },
    {
      title: '最后在线',
      dataIndex: 'lastOnlineTime',
      key: 'lastOnlineTime',
      width: 180,
      render: (time?: string) =>
        time ? dayjs(time).format('YYYY-MM-DD HH:mm:ss') : '-',
    },
  ]

  const deviceTypeChartData = [
    { name: '在线', value: deviceStats?.onlineDevices || 0, color: '#52c41a' },
    { name: '离线', value: deviceStats?.offlineDevices || 0, color: '#8c8c8c' },
    { name: '告警', value: deviceStats?.warningDevices || 0, color: '#faad14' },
    { name: '错误', value: deviceStats?.errorDevices || 0, color: '#ff4d4f' },
  ]

  const alarmChartData = [
    { name: '严重', value: alarmStats?.criticalAlarms || 0, color: '#ff4d4f' },
    { name: '警告', value: alarmStats?.warningAlarms || 0, color: '#faad14' },
    { name: '信息', value: alarmStats?.informationAlarms || 0, color: '#1890ff' },
  ]

  if (loading && !deviceStats) {
    return (
      <div style={{ textAlign: 'center', padding: 100 }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>加载数据中...</div>
      </div>
    )
  }

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        实时看板
      </Title>

      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={12} sm={12} md={6}>
          <Card>
            <Statistic
              title="设备总数"
              value={deviceStats?.totalDevices || 0}
              prefix={<DeviceHubOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={12} md={6}>
          <Card>
            <Statistic
              title="在线设备"
              value={deviceStats?.onlineDevices || 0}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={12} md={6}>
          <Card>
            <Statistic
              title="离线设备"
              value={deviceStats?.offlineDevices || 0}
              prefix={<CloseCircleOutlined />}
              valueStyle={{ color: '#8c8c8c' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={12} md={6}>
          <Card>
            <Statistic
              title="活跃告警"
              value={alarmStats?.activeAlarms || 0}
              prefix={<WarningOutlined />}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={16}>
          <Card title="实时数据趋势" size="small">
            <div style={{ height: 350 }}>
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="time" />
                  <YAxis />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="temperature"
                    stroke="#ff7300"
                    fill="#ff7300"
                    fillOpacity={0.3}
                    name="温度 (°C)"
                  />
                  <Area
                    type="monotone"
                    dataKey="humidity"
                    stroke="#387908"
                    fill="#387908"
                    fillOpacity={0.3}
                    name="湿度 (%)"
                  />
                  <Area
                    type="monotone"
                    dataKey="power"
                    stroke="#1890ff"
                    fill="#1890ff"
                    fillOpacity={0.3}
                    name="功率 (W)"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </Col>

        <Col xs={24} lg={8}>
          <Row gutter={[16, 16]}>
            <Col span={24}>
              <Card title="设备状态分布" size="small">
                <div style={{ height: 150 }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={deviceTypeChartData}
                        dataKey="value"
                        nameKey="name"
                        cx="50%"
                        cy="50%"
                        outerRadius={50}
                        label={({ name, percent }) =>
                          `${name} ${(percent * 100).toFixed(0)}%`
                        }
                      >
                        {deviceTypeChartData.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </div>
              </Card>
            </Col>

            <Col span={24}>
              <Card title="告警级别分布" size="small">
                <div style={{ height: 150 }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={alarmChartData}
                        dataKey="value"
                        nameKey="name"
                        cx="50%"
                        cy="50%"
                        outerRadius={50}
                        label={({ name, percent }) =>
                          `${name} ${(percent * 100).toFixed(0)}%`
                        }
                      >
                        {alarmChartData.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </div>
              </Card>
            </Col>
          </Row>
        </Col>
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} lg={16}>
          <Card title="设备列表" size="small">
            {devices.length > 0 ? (
              <Table
                columns={deviceColumns}
                dataSource={devices}
                rowKey="id"
                size="small"
                pagination={{ pageSize: 5 }}
              />
            ) : (
              <Empty description="暂无设备数据" />
            )}
          </Card>
        </Col>

        <Col xs={24} lg={8}>
          <Card
            title={
              <span>
                <AlertOutlined style={{ marginRight: 8 }} />
                最近告警
              </span>
            }
            size="small"
          >
            {recentAlarms.length > 0 ? (
              <List
                size="small"
                dataSource={recentAlarms}
                renderItem={(alarm) => (
                  <List.Item>
                    <List.Item.Meta
                      avatar={
                        <Tag color={getAlarmLevelColor(alarm.level)}>
                          {getAlarmLevelText(alarm.level)}
                        </Tag>
                      }
                      title={
                        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                          <span style={{ fontWeight: 500 }}>{alarm.title}</span>
                          <Tag
                            color={
                              alarm.status === 'Active'
                                ? 'error'
                                : alarm.status === 'Acknowledged'
                                ? 'warning'
                                : 'success'
                            }
                          >
                            {alarm.status === 'Active'
                              ? '活跃'
                              : alarm.status === 'Acknowledged'
                              ? '已确认'
                              : '已处理'}
                          </Tag>
                        </div>
                      }
                      description={
                        <div>
                          <div style={{ color: '#666' }}>{alarm.deviceName}</div>
                          <div style={{ color: '#999', fontSize: 12 }}>
                            {dayjs(alarm.triggeredAt).format('MM-DD HH:mm:ss')}
                          </div>
                        </div>
                      }
                    />
                  </List.Item>
                )}
              />
            ) : (
              <Empty description="暂无告警" />
            )}
          </Card>
        </Col>
      </Row>

      {Object.keys(realTimeData).length > 0 && (
        <Card title="实时数据更新" style={{ marginTop: 16 }}>
          <Row gutter={[16, 16]}>
            {Object.values(realTimeData).slice(0, 4).map((data) => (
              <Col xs={12} sm={8} md={6} key={data.deviceId}>
                <Card size="small" type="inner">
                  <div style={{ fontWeight: 500, marginBottom: 8 }}>{data.deviceName}</div>
                  {Object.entries(data.metrics).map(([key, value]) => (
                    <div key={key} style={{ marginBottom: 4 }}>
                      <span style={{ color: '#666' }}>{key}: </span>
                      <span style={{ fontWeight: 500 }}>
                        {value}
                        {data.units[key] && ` ${data.units[key]}`}
                      </span>
                    </div>
                  ))}
                  <div style={{ color: '#999', fontSize: 12, marginTop: 8 }}>
                    {dayjs(data.timestamp).format('HH:mm:ss')}
                  </div>
                </Card>
              </Col>
            ))}
          </Row>
        </Card>
      )}
    </div>
  )
}

export default Dashboard
