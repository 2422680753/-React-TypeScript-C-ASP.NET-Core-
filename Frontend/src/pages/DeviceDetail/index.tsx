import {
  Card,
  Descriptions,
  Tag,
  Row,
  Col,
  Statistic,
  Tabs,
  Table,
  Button,
  Space,
  Typography,
  Empty,
  Spin,
  Divider,
  Modal,
  Form,
  Select,
  InputNumber,
  message,
} from 'antd'
import {
  ArrowLeftOutlined,
  EditOutlined,
  ControlOutlined,
  ReloadOutlined,
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
} from 'recharts'
import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { deviceApi, dataApi, alarmApi, controlApi } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { useDeviceStore } from '@/store'
import type { Device, DeviceData, Alarm, RealTimeData, ControlCommand } from '@/types'

const { Title, Text } = Typography
const { TabPane } = Tabs
const { Option } = Select

const DeviceDetail = () => {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [device, setDevice] = useState<Device | null>(null)
  const [realTimeData, setRealTimeData] = useState<RealTimeData | null>(null)
  const [historicalData, setHistoricalData] = useState<DeviceData[]>([])
  const [alarms, setAlarms] = useState<Alarm[]>([])
  const [commands, setCommands] = useState<ControlCommand[]>([])
  const [chartData, setChartData] = useState<Array<{ time: string; [key: string]: number | string }>>([])
  const [controlModalOpen, setControlModalOpen] = useState(false)
  const [form] = Form.useForm()
  const { updateRealTimeData } = useDeviceStore()

  useEffect(() => {
    if (id) {
      loadDeviceData()
      subscribeToDevice(id)
    }

    return () => {
      if (id) {
        signalRService.unsubscribeFromDevice(id)
      }
    }
  }, [id])

  const subscribeToDevice = async (deviceId: string) => {
    try {
      await signalRService.subscribeToDevice(deviceId)
      
      signalRService.onDeviceData((data) => {
        if (data.deviceId === deviceId) {
          setRealTimeData(data)
          updateRealTimeData(data)
          updateChartData(data)
        }
      })
    } catch (error) {
      console.error('Failed to subscribe to device:', error)
    }
  }

  const loadDeviceData = async () => {
    if (!id) return

    setLoading(true)
    try {
      const [deviceData, realTime, historical, deviceAlarms, deviceCommands] = await Promise.all([
        deviceApi.getById(id),
        dataApi.getLatestData(id),
        dataApi.getHistoricalData({
          deviceId: id,
          startTime: dayjs().subtract(24, 'hour').toISOString(),
          endTime: dayjs().toISOString(),
          limit: 100,
        }),
        alarmApi.getAll(undefined, undefined, id, 20),
        controlApi.getByDevice(id, 20),
      ])

      setDevice(deviceData)
      setRealTimeData(realTime)
      setHistoricalData(historical)
      setAlarms(deviceAlarms)
      setCommands(deviceCommands)
      generateChartData(historical)
    } catch (error) {
      console.error('Failed to load device data:', error)
      message.error('加载设备数据失败')
    } finally {
      setLoading(false)
    }
  }

  const generateChartData = (data: DeviceData[]) => {
    const metrics = [...new Set(data.map((d) => d.metric))]
    const timeGroups: Record<string, Record<string, number>> = {}

    data.forEach((d) => {
      const time = dayjs(d.timestamp).format('HH:mm:ss')
      if (!timeGroups[time]) {
        timeGroups[time] = {}
      }
      timeGroups[time][d.metric] = d.value
    })

    const chartData = Object.entries(timeGroups)
      .map(([time, values]) => ({
        time,
        ...values,
      }))
      .sort((a, b) => a.time.localeCompare(b.time))

    setChartData(chartData)
  }

  const updateChartData = (data: RealTimeData) => {
    const time = dayjs().format('HH:mm:ss')
    setChartData((prev) => {
      const newData = [...prev.slice(-50), { time, ...data.metrics }]
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

  const getCommandStatusColor = (status: string) => {
    const colors: Record<string, string> = {
      Pending: 'default',
      Queued: 'processing',
      Sent: 'processing',
      Executing: 'processing',
      Executed: 'success',
      Failed: 'error',
      TimedOut: 'warning',
      Cancelled: 'default',
    }
    return colors[status] || 'default'
  }

  const getCommandStatusText = (status: string) => {
    const texts: Record<string, string> = {
      Pending: '等待中',
      Queued: '队列中',
      Sent: '已发送',
      Executing: '执行中',
      Executed: '已执行',
      Failed: '失败',
      TimedOut: '超时',
      Cancelled: '已取消',
    }
    return texts[status] || status
  }

  const handleSendCommand = async (values: {
    command: string
    parameters?: Record<string, number>
    priority: string
  }) => {
    if (!id) return

    try {
      await controlApi.sendCommand({
        deviceId: id,
        command: values.command,
        parameters: values.parameters,
        priority: values.priority,
      })

      message.success('命令已发送')
      setControlModalOpen(false)
      form.resetFields()

      loadDeviceData()
    } catch (error) {
      console.error('Failed to send command:', error)
      message.error('发送命令失败')
    }
  }

  const alarmColumns: ColumnsType<Alarm> = [
    {
      title: '告警标题',
      dataIndex: 'title',
      key: 'title',
      ellipsis: true,
    },
    {
      title: '级别',
      dataIndex: 'level',
      key: 'level',
      width: 100,
      render: (level: string) => (
        <Tag color={getAlarmLevelColor(level)}>{getAlarmLevelText(level)}</Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => (
        <Tag color={status === 'Active' ? 'error' : 'success'}>
          {status === 'Active' ? '活跃' : status === 'Acknowledged' ? '已确认' : '已处理'}
        </Tag>
      ),
    },
    {
      title: '触发时间',
      dataIndex: 'triggeredAt',
      key: 'triggeredAt',
      width: 170,
      render: (time: string) => dayjs(time).format('YYYY-MM-DD HH:mm:ss'),
    },
  ]

  const commandColumns: ColumnsType<ControlCommand> = [
    {
      title: '命令',
      dataIndex: 'command',
      key: 'command',
    },
    {
      title: '优先级',
      dataIndex: 'priority',
      key: 'priority',
      width: 100,
      render: (priority: string) => (
        <Tag
          color={
            priority === 'Critical'
              ? 'error'
              : priority === 'High'
              ? 'warning'
              : 'default'
          }
        >
          {priority}
        </Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => (
        <Tag color={getCommandStatusColor(status)}>{getCommandStatusText(status)}</Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      render: (time: string) => dayjs(time).format('YYYY-MM-DD HH:mm:ss'),
    },
    {
      title: '结果',
      dataIndex: 'result',
      key: 'result',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
  ]

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: 100 }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>加载设备数据中...</div>
      </div>
    )
  }

  if (!device) {
    return (
      <div style={{ textAlign: 'center', padding: 100 }}>
        <Empty description="设备不存在" />
      </div>
    )
  }

  const metrics = realTimeData ? Object.keys(realTimeData.metrics) : []

  return (
    <div>
      <div style={{ marginBottom: 24, display: 'flex', alignItems: 'center' }}>
        <Button
          icon={<ArrowLeftOutlined />}
          onClick={() => navigate('/devices')}
          style={{ marginRight: 16 }}
        >
          返回
        </Button>
        <Title level={2} style={{ margin: 0 }}>
          设备详情
        </Title>
        <Space style={{ marginLeft: 'auto' }}>
          <Button icon={<ReloadOutlined />} onClick={loadDeviceData}>
            刷新
          </Button>
          <Button
            type="primary"
            icon={<ControlOutlined />}
            onClick={() => setControlModalOpen(true)}
          >
            发送命令
          </Button>
        </Space>
      </div>

      <Card style={{ marginBottom: 16 }}>
        <Descriptions title="设备信息" bordered column={4}>
          <Descriptions.Item label="设备名称">{device.name}</Descriptions.Item>
          <Descriptions.Item label="设备类型">{device.deviceType}</Descriptions.Item>
          <Descriptions.Item label="序列号">{device.serialNumber}</Descriptions.Item>
          <Descriptions.Item label="状态">
            <Tag color={getStatusColor(device.status)}>{getStatusText(device.status)}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="位置">{device.location || '-'}</Descriptions.Item>
          <Descriptions.Item label="经纬度">
            {device.latitude && device.longitude
              ? `${device.latitude}, ${device.longitude}`
              : '-'}
          </Descriptions.Item>
          <Descriptions.Item label="制造商">{device.manufacturer || '-'}</Descriptions.Item>
          <Descriptions.Item label="固件版本">{device.firmwareVersion || '-'}</Descriptions.Item>
          <Descriptions.Item label="描述" span={2}>
            {device.description || '-'}
          </Descriptions.Item>
          <Descriptions.Item label="创建时间" span={2}>
            {dayjs(device.createdAt).format('YYYY-MM-DD HH:mm:ss')}
          </Descriptions.Item>
          <Descriptions.Item label="最后在线" span={2}>
            {device.lastOnlineTime
              ? dayjs(device.lastOnlineTime).format('YYYY-MM-DD HH:mm:ss')
              : '-'}
          </Descriptions.Item>
          <Descriptions.Item label="最后更新" span={2}>
            {dayjs(device.updatedAt).format('YYYY-MM-DD HH:mm:ss')}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      {realTimeData && metrics.length > 0 && (
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          {metrics.map((metric) => (
            <Col xs={12} sm={6} md={4} key={metric}>
              <Card size="small">
                <Statistic
                  title={metric}
                  value={realTimeData.metrics[metric]}
                  suffix={realTimeData.units[metric] || ''}
                />
              </Card>
            </Col>
          ))}
        </Row>
      )}

      <Tabs defaultActiveKey="realtime">
        <TabPane tab="实时数据" key="realtime">
          <Card>
            {chartData.length > 0 ? (
              <div style={{ height: 400 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="time" />
                    <YAxis />
                    <Tooltip />
                    {metrics.map((metric, index) => {
                      const colors = ['#1890ff', '#52c41a', '#faad14', '#ff4d4f', '#722ed1']
                      return (
                        <Line
                          key={metric}
                          type="monotone"
                          dataKey={metric}
                          stroke={colors[index % colors.length]}
                          dot={false}
                        />
                      )
                    })}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <Empty description="暂无数据" />
            )}
          </Card>
        </TabPane>

        <TabPane tab="历史数据" key="history">
          <Card>
            {historicalData.length > 0 ? (
              <div style={{ height: 400, marginBottom: 24 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="time" />
                    <YAxis />
                    <Tooltip />
                    {metrics.map((metric, index) => {
                      const colors = ['#1890ff', '#52c41a', '#faad14', '#ff4d4f', '#722ed1']
                      return (
                        <Area
                          key={metric}
                          type="monotone"
                          dataKey={metric}
                          stroke={colors[index % colors.length]}
                          fill={colors[index % colors.length]}
                          fillOpacity={0.2}
                        />
                      )
                    })}
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <Empty description="暂无历史数据" />
            )}
          </Card>
        </TabPane>

        <TabPane tab="告警记录" key="alarms">
          <Card>
            {alarms.length > 0 ? (
              <Table
                columns={alarmColumns}
                dataSource={alarms}
                rowKey="id"
                pagination={{ pageSize: 10 }}
              />
            ) : (
              <Empty description="暂无告警记录" />
            )}
          </Card>
        </TabPane>

        <TabPane tab="命令历史" key="commands">
          <Card>
            {commands.length > 0 ? (
              <Table
                columns={commandColumns}
                dataSource={commands}
                rowKey="id"
                pagination={{ pageSize: 10 }}
              />
            ) : (
              <Empty description="暂无命令记录" />
            )}
          </Card>
        </TabPane>
      </Tabs>

      <Modal
        title="发送控制命令"
        open={controlModalOpen}
        onCancel={() => setControlModalOpen(false)}
        footer={null}
      >
        <Form form={form} layout="vertical" onFinish={handleSendCommand}>
          <Form.Item
            name="command"
            label="命令类型"
            rules={[{ required: true, message: '请选择命令类型' }]}
          >
            <Select placeholder="请选择命令类型">
              <Option value="RESTART">重启设备</Option>
              <Option value="START">启动</Option>
              <Option value="STOP">停止</Option>
              <Option value="SET_VALUE">设置参数</Option>
              <Option value="READ_CONFIG">读取配置</Option>
              <Option value="WRITE_CONFIG">写入配置</Option>
              <Option value="RESET">恢复出厂</Option>
              <Option value="UPGRADE">固件升级</Option>
            </Select>
          </Form.Item>

          <Form.Item name="parameters" label="参数">
            <Input.Number
              placeholder="请输入参数值"
              style={{ width: '100%' }}
            />
          </Form.Item>

          <Form.Item
            name="priority"
            label="优先级"
            initialValue="Normal"
          >
            <Select>
              <Option value="Low">低</Option>
              <Option value="Normal">正常</Option>
              <Option value="High">高</Option>
              <Option value="Critical">紧急</Option>
            </Select>
          </Form.Item>

          <Form.Item style={{ textAlign: 'right', marginBottom: 0 }}>
            <Space>
              <Button onClick={() => setControlModalOpen(false)}>取消</Button>
              <Button type="primary" htmlType="submit">
                发送
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}

export default DeviceDetail
