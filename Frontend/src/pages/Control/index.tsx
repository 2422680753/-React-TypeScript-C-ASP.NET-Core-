import {
  Card,
  Table,
  Button,
  Tag,
  Space,
  Typography,
  Empty,
  Spin,
  Modal,
  Form,
  Select,
  InputNumber,
  Input,
  message,
  Popconfirm,
  Row,
  Col,
  Statistic,
  Descriptions,
  Checkbox,
} from 'antd'
import {
  ControlOutlined,
  ReloadOutlined,
  SendOutlined,
  StopOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ClockCircleOutlined,
} from '@ant-design/icons'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { controlApi, deviceApi } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { useControlStore } from '@/store'
import type { ControlCommand, Device, CommandStatus } from '@/types'

const { Title } = Typography
const { Option } = Select
const { TextArea } = Input

const Control = () => {
  const [loading, setLoading] = useState(false)
  const [commands, setCommands] = useState<ControlCommand[]>([])
  const [devices, setDevices] = useState<Device[]>([])
  const [isCommandModalOpen, setIsCommandModalOpen] = useState(false)
  const [isBatchCommandModalOpen, setIsBatchCommandModalOpen] = useState(false)
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null)
  const [selectedDevices, setSelectedDevices] = useState<string[]>([])
  const [form] = Form.useForm()
  const [batchForm] = Form.useForm()
  const { addCommand, updateCommand } = useControlStore()

  useEffect(() => {
    loadData()
    setupRealTimeListeners()
  }, [])

  const loadData = async () => {
    setLoading(true)
    try {
      const [commandsData, devicesData] = await Promise.all([
        controlApi.getAll(undefined, undefined, 100),
        deviceApi.getAll(),
      ])

      setCommands(commandsData)
      setDevices(devicesData)
    } catch (error) {
      console.error('Failed to load control data:', error)
      message.error('加载控制数据失败')
    } finally {
      setLoading(false)
    }
  }

  const setupRealTimeListeners = () => {
    const unsubCommandResult = signalRService.onCommandResult((result) => {
      console.log('Command result:', result)
    })

    return () => {
      unsubCommandResult()
    }
  }

  const getStatusColor = (status: string) => {
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

  const getStatusText = (status: string) => {
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

  const getPriorityColor = (priority: string) => {
    const colors: Record<string, string> = {
      Low: 'default',
      Normal: 'info',
      High: 'warning',
      Critical: 'error',
    }
    return colors[priority] || 'default'
  }

  const getPriorityText = (priority: string) => {
    const texts: Record<string, string> = {
      Low: '低',
      Normal: '正常',
      High: '高',
      Critical: '紧急',
    }
    return texts[priority] || priority
  }

  const handleSendCommand = async (values: {
    command: string
    parameters?: Record<string, number>
    priority: string
    maxRetries?: number
    timeoutSeconds?: number
  }) => {
    if (!selectedDevice) return

    try {
      const command = await controlApi.sendCommand({
        deviceId: selectedDevice.id,
        command: values.command,
        parameters: values.parameters,
        priority: values.priority,
        maxRetries: values.maxRetries,
        timeoutSeconds: values.timeoutSeconds,
      })

      message.success('命令已发送')
      addCommand(command)
      setCommands((prev) => [command, ...prev])
      setIsCommandModalOpen(false)
      form.resetFields()
      setSelectedDevice(null)
    } catch (error) {
      console.error('Failed to send command:', error)
      message.error('发送命令失败')
    }
  }

  const handleSendBatchCommand = async (values: {
    command: string
    parameters?: Record<string, number>
    priority: string
    maxRetries?: number
    timeoutSeconds?: number
  }) => {
    if (selectedDevices.length === 0) {
      message.warning('请选择至少一个设备')
      return
    }

    try {
      const results = await controlApi.sendBatchCommand({
        deviceIds: selectedDevices,
        command: values.command,
        parameters: values.parameters,
        priority: values.priority,
        maxRetries: values.maxRetries,
        timeoutSeconds: values.timeoutSeconds,
      })

      message.success(`已向 ${selectedDevices.length} 个设备发送命令`)
      setCommands((prev) => [...results, ...prev])
      setIsBatchCommandModalOpen(false)
      batchForm.resetFields()
      setSelectedDevices([])
    } catch (error) {
      console.error('Failed to send batch commands:', error)
      message.error('发送批量命令失败')
    }
  }

  const handleCancelCommand = async (command: ControlCommand) => {
    try {
      await controlApi.cancelCommand(command.id)
      message.success('命令已取消')
      loadData()
    } catch (error) {
      console.error('Failed to cancel command:', error)
      message.error('取消命令失败')
    }
  }

  const onlineDevices = devices.filter((d) => d.status === 'Online')

  const columns: ColumnsType<ControlCommand> = [
    {
      title: '设备',
      dataIndex: 'deviceName',
      key: 'deviceName',
      width: 150,
    },
    {
      title: '命令',
      dataIndex: 'command',
      key: 'command',
      ellipsis: true,
    },
    {
      title: '优先级',
      dataIndex: 'priority',
      key: 'priority',
      width: 100,
      render: (priority: string) => (
        <Tag color={getPriorityColor(priority)}>{getPriorityText(priority)}</Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => (
        <Tag color={getStatusColor(status)}>
          {status === 'Pending' ||
          status === 'Queued' ||
          status === 'Sent' ||
          status === 'Executing' ? (
            <ClockCircleOutlined spin />
          ) : status === 'Executed' ? (
            <CheckCircleOutlined />
          ) : (
            <CloseCircleOutlined />
          )}
          {' '}
          {getStatusText(status)}
        </Tag>
      ),
    },
    {
      title: '重试次数',
      dataIndex: 'retryCount',
      key: 'retryCount',
      width: 100,
      render: (count: number) => (
        <span>
          {count !== undefined ? `${count} / ${
            commands.find((c) => c.retryCount === count)?.maxRetries || 3
          }` : '-'}
        </span>
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
      title: '执行时间',
      key: 'executionTime',
      width: 100,
      render: (_, record) => {
        if (record.executedAt && record.sentAt) {
          const duration = dayjs(record.executedAt).diff(dayjs(record.sentAt), 'second')
          return `${duration}秒`
        }
        return '-'
      },
    },
    {
      title: '结果',
      dataIndex: 'result',
      key: 'result',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '错误',
      dataIndex: 'errorMessage',
      key: 'errorMessage',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 120,
      render: (_, record) => (
        <Space size="small">
          {(record.status === 'Pending' || record.status === 'Queued') && (
            <Popconfirm
              title="确定要取消此命令吗？"
              onConfirm={() => handleCancelCommand(record)}
              okText="确定"
              cancelText="取消"
            >
              <Button type="text" icon={<StopOutlined />} danger>
                取消
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  const commandTypes = [
    { value: 'RESTART', label: '重启设备' },
    { value: 'START', label: '启动' },
    { value: 'STOP', label: '停止' },
    { value: 'SET_VALUE', label: '设置参数' },
    { value: 'READ_CONFIG', label: '读取配置' },
    { value: 'WRITE_CONFIG', label: '写入配置' },
    { value: 'RESET', label: '恢复出厂' },
    { value: 'UPGRADE', label: '固件升级' },
    { value: 'TEST', label: '测试命令' },
  ]

  const pendingCount = commands.filter((c) =>
    ['Pending', 'Queued', 'Sent', 'Executing'].includes(c.status)
  ).length
  const successCount = commands.filter((c) => c.status === 'Executed').length
  const failedCount = commands.filter((c) =>
    ['Failed', 'TimedOut', 'Cancelled'].includes(c.status)
  ).length

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        远程控制
      </Title>

      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="在线设备"
              value={onlineDevices.length}
              prefix={<ControlOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="等待执行"
              value={pendingCount}
              prefix={<ClockCircleOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="执行成功"
              value={successCount}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="执行失败"
              value={failedCount}
              prefix={<CloseCircleOutlined />}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
      </Row>

      <Card style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={() => {
              if (onlineDevices.length === 0) {
                message.warning('没有在线设备可控制')
                return
              }
              setIsCommandModalOpen(true)
            }}
            disabled={onlineDevices.length === 0}
          >
            发送单设备命令
          </Button>
          <Button
            icon={<SendOutlined />}
            onClick={() => {
              if (onlineDevices.length === 0) {
                message.warning('没有在线设备可控制')
                return
              }
              setIsBatchCommandModalOpen(true)
            }}
            disabled={onlineDevices.length === 0}
          >
            发送批量命令
          </Button>
          <Button icon={<ReloadOutlined />} onClick={loadData} style={{ marginLeft: 'auto' }}>
            刷新
          </Button>
        </Space>
      </Card>

      <Card title="命令执行记录">
        {loading ? (
          <div style={{ textAlign: 'center', padding: 50 }}>
            <Spin size="large" />
          </div>
        ) : commands.length > 0 ? (
          <Table
            columns={columns}
            dataSource={commands}
            rowKey="id"
            scroll={{ x: 1400 }}
            pagination={{
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
            }}
          />
        ) : (
          <Empty description="暂无命令记录" />
        )}
      </Card>

      <Modal
        title="发送控制命令"
        open={isCommandModalOpen}
        onCancel={() => {
          setIsCommandModalOpen(false)
          form.resetFields()
          setSelectedDevice(null)
        }}
        footer={null}
        width={600}
      >
        <Form form={form} layout="vertical" onFinish={handleSendCommand}>
          <Form.Item
            name="deviceId"
            label="目标设备"
            rules={[{ required: true, message: '请选择目标设备' }]}
          >
            <Select
              placeholder="请选择目标设备"
              onChange={(value) => {
                const device = onlineDevices.find((d) => d.id === value)
                setSelectedDevice(device || null)
              }}
            >
              {onlineDevices.map((device) => (
                <Option key={device.id} value={device.id}>
                  {device.name} ({device.serialNumber})
                </Option>
              ))}
            </Select>
          </Form.Item>

          {selectedDevice && (
            <Descriptions bordered size="small" column={2} style={{ marginBottom: 16 }}>
              <Descriptions.Item label="设备类型">{selectedDevice.deviceType}</Descriptions.Item>
              <Descriptions.Item label="位置">{selectedDevice.location || '-'}</Descriptions.Item>
              <Descriptions.Item label="固件版本">
                {selectedDevice.firmwareVersion || '-'}
              </Descriptions.Item>
              <Descriptions.Item label="最后在线">
                {selectedDevice.lastOnlineTime
                  ? dayjs(selectedDevice.lastOnlineTime).format('HH:mm:ss')
                  : '-'}
              </Descriptions.Item>
            </Descriptions>
          )}

          <Form.Item
            name="command"
            label="命令类型"
            rules={[{ required: true, message: '请选择命令类型' }]}
          >
            <Select placeholder="请选择命令类型">
              {commandTypes.map((cmd) => (
                <Option key={cmd.value} value={cmd.value}>
                  {cmd.label}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item name="parameters" label="参数值">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="请输入参数值（可选）"
            />
          </Form.Item>

          <Form.Item name="priority" label="优先级" initialValue="Normal">
            <Select>
              <Option value="Low">低</Option>
              <Option value="Normal">正常</Option>
              <Option value="High">高</Option>
              <Option value="Critical">紧急</Option>
            </Select>
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="maxRetries" label="最大重试次数" initialValue={3}>
                <InputNumber
                style={{ width: '100%' }}
                placeholder="最大重试次数"
                min={0}
                max={10}
              />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="timeoutSeconds" label="超时时间（秒）" initialValue={30}>
                <InputNumber
                style={{ width: '100%' }}
                placeholder="超时时间（秒）"
                min={1}
                max={300}
              />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item style={{ textAlign: 'right', marginBottom: 0 }}>
            <Space>
              <Button
                onClick={() => {
                  setIsCommandModalOpen(false)
                  form.resetFields()
                  setSelectedDevice(null)
                }}
              >
                取消
              </Button>
              <Button type="primary" htmlType="submit">
                发送
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="发送批量控制命令"
        open={isBatchCommandModalOpen}
        onCancel={() => {
          setIsBatchCommandModalOpen(false)
          batchForm.resetFields()
          setSelectedDevices([])
        }}
        footer={null}
        width={700}
      >
        <Form form={batchForm} layout="vertical" onFinish={handleSendBatchCommand}>
          <Form.Item label="选择目标设备">
            <div
              style={{
                maxHeight: 200,
                overflow: 'auto',
                border: '1px solid #d9d9d9',
                borderRadius: 6,
                padding: 16,
              }}
            >
              {onlineDevices.map((device) => (
                <div key={device.id} style={{ marginBottom: 8 }}>
                  <Checkbox
                    checked={selectedDevices.includes(device.id)}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedDevices((prev) => [...prev, device.id])
                      } else {
                        setSelectedDevices((prev) =>
                          prev.filter((id) => id !== device.id)
                      }
                    }}
                  >
                    {device.name} ({device.serialNumber}) - {device.deviceType}
                  </Checkbox>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 8, color: '#666' }}>
              已选择 {selectedDevices.length} 个设备
            </div>
          </Form.Item>

          <Form.Item
            name="command"
            label="命令类型"
            rules={[{ required: true, message: '请选择命令类型' }]}
          >
            <Select placeholder="请选择命令类型">
              {commandTypes.map((cmd) => (
                <Option key={cmd.value} value={cmd.value}>
                  {cmd.label}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item name="parameters" label="参数值">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="请输入参数值（可选）"
            />
          </Form.Item>

          <Form.Item name="priority" label="优先级" initialValue="Normal">
            <Select>
              <Option value="Low">低</Option>
              <Option value="Normal">正常</Option>
              <Option value="High">高</Option>
              <Option value="Critical">紧急</Option>
            </Select>
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="maxRetries" label="最大重试次数" initialValue={3}>
                <InputNumber
                style={{ width: '100%' }}
                placeholder="最大重试次数"
                min={0}
                max={10}
              />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="timeoutSeconds" label="超时时间（秒）" initialValue={30}>
                <InputNumber
                style={{ width: '100%' }}
                placeholder="超时时间（秒）"
                min={1}
                max={300}
              />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item style={{ textAlign: 'right', marginBottom: 0 }}>
            <Space>
              <Button
                onClick={() => {
                  setIsBatchCommandModalOpen(false)
                  batchForm.resetFields()
                  setSelectedDevices([])
                }}
              >
                取消
              </Button>
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

export default Control
