import {
  Card,
  Table,
  Tag,
  Button,
  Space,
  Select,
  Typography,
  Empty,
  Spin,
  Modal,
  Input,
  message,
  Popconfirm,
  Descriptions,
  Statistic,
  Row,
  Col,
} from 'antd'
import {
  WarningOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  BellOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { alarmApi } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { useAlarmStore } from '@/store'
import type { Alarm, AlarmStatistics } from '@/types'

const { Title, Text } = Typography
const { TextArea } = Input
const { Option } = Select

const Alarms = () => {
  const [loading, setLoading] = useState(false)
  const [alarms, setAlarms] = useState<Alarm[]>([])
  const [statistics, setStatistics] = useState<AlarmStatistics | null>(null)
  const [selectedAlarm, setSelectedAlarm] = useState<Alarm | null>(null)
  const [detailModalOpen, setDetailModalOpen] = useState(false)
  const [statusFilter, setStatusFilter] = useState<string>()
  const [levelFilter, setLevelFilter] = useState<string>()
  const { addAlarm, updateAlarm, resetUnreadCount } = useAlarmStore()

  useEffect(() => {
    loadAlarms()
    setupRealTimeListeners()
    resetUnreadCount()
  }, [])

  const loadAlarms = async () => {
    setLoading(true)
    try {
      const [alarmData, statsData] = await Promise.all([
        alarmApi.getAll(statusFilter, levelFilter, undefined, 100),
        alarmApi.getStatistics(),
      ])

      setAlarms(alarmData)
      setStatistics(statsData)
    } catch (error) {
      console.error('Failed to load alarms:', error)
      message.error('加载告警数据失败')
    } finally {
      setLoading(false)
    }
  }

  const setupRealTimeListeners = () => {
    const unsubAlarm = signalRService.onAlarm((alarm) => {
      addAlarm(alarm)
      setAlarms((prev) => [alarm, ...prev])
    })

    const unsubAlarmUpdate = signalRService.onAlarmUpdate((alarm) => {
      updateAlarm(alarm)
      setAlarms((prev) =>
        prev.map((a) => (a.id === alarm.id ? alarm : a))
      )
    })

    return () => {
      unsubAlarm()
      unsubAlarmUpdate()
    }
  }

  const getLevelColor = (level: string) => {
    const colors: Record<string, string> = {
      Critical: 'error',
      Emergency: 'error',
      Warning: 'warning',
      Information: 'info',
    }
    return colors[level] || 'default'
  }

  const getLevelText = (level: string) => {
    const texts: Record<string, string> = {
      Critical: '严重',
      Emergency: '紧急',
      Warning: '警告',
      Information: '信息',
    }
    return texts[level] || level
  }

  const getStatusColor = (status: string) => {
    const colors: Record<string, string> = {
      Active: 'error',
      Acknowledged: 'warning',
      Resolved: 'success',
      Cleared: 'success',
      Suppressed: 'default',
    }
    return colors[status] || 'default'
  }

  const getStatusText = (status: string) => {
    const texts: Record<string, string> = {
      Active: '活跃',
      Acknowledged: '已确认',
      Resolved: '已处理',
      Cleared: '已清除',
      Suppressed: '已抑制',
    }
    return texts[status] || status
  }

  const handleAcknowledge = async (alarm: Alarm, notes?: string) => {
    try {
      const updatedAlarm = await alarmApi.acknowledge(alarm.id, notes)
      message.success('告警已确认')
      setAlarms((prev) =>
        prev.map((a) => (a.id === alarm.id ? updatedAlarm : a))
      )
      setSelectedAlarm(updatedAlarm)
    } catch (error) {
      console.error('Failed to acknowledge alarm:', error)
      message.error('确认告警失败')
    }
  }

  const handleResolve = async (alarm: Alarm, resolutionNotes: string) => {
    try {
      const updatedAlarm = await alarmApi.resolve(alarm.id, resolutionNotes)
      message.success('告警已处理')
      setAlarms((prev) =>
        prev.map((a) => (a.id === alarm.id ? updatedAlarm : a))
      )
      setSelectedAlarm(updatedAlarm)
    } catch (error) {
      console.error('Failed to resolve alarm:', error)
      message.error('处理告警失败')
    }
  }

  const handleStatusFilterChange = (value: string | undefined) => {
    setStatusFilter(value)
  }

  const handleLevelFilterChange = (value: string | undefined) => {
    setLevelFilter(value)
  }

  useEffect(() => {
    loadAlarms()
  }, [statusFilter, levelFilter])

  const columns: ColumnsType<Alarm> = [
    {
      title: '级别',
      dataIndex: 'level',
      key: 'level',
      width: 100,
      render: (level: string) => (
        <Tag color={getLevelColor(level)} icon={<ExclamationCircleOutlined />}>
          {getLevelText(level)}
        </Tag>
      ),
    },
    {
      title: '标题',
      dataIndex: 'title',
      key: 'title',
      ellipsis: true,
      render: (text: string, record) => (
        <Button
          type="link"
          onClick={() => {
            setSelectedAlarm(record)
            setDetailModalOpen(true)
          }}
          style={{ padding: 0 }}
        >
          {text}
        </Button>
      ),
    },
    {
      title: '设备',
      dataIndex: 'deviceName',
      key: 'deviceName',
      width: 150,
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
      title: '触发值',
      dataIndex: 'triggeredValue',
      key: 'triggeredValue',
      width: 120,
      render: (value: number) => (
        <span>
          {value !== undefined ? value : '-'}
        </span>
      ),
    },
    {
      title: '触发时间',
      dataIndex: 'triggeredAt',
      key: 'triggeredAt',
      width: 170,
      render: (time: string) => dayjs(time).format('YYYY-MM-DD HH:mm:ss'),
    },
    {
      title: '操作',
      key: 'action',
      width: 200,
      render: (_, record) => (
        <Space size="small">
          <Button
            size="small"
            onClick={() => {
              setSelectedAlarm(record)
              setDetailModalOpen(true)
            }}
          >
            详情
          </Button>
          {record.status === 'Active' && (
            <Button
              size="small"
              type="primary"
              onClick={() => handleAcknowledge(record)}
            >
              确认
            </Button>
          )}
          {(record.status === 'Active' || record.status === 'Acknowledged') && (
            <Popconfirm
              title="处理告警"
              description="请输入处理说明"
              onConfirm={(e) => {
                const input = document.createElement('input')
                const notes = prompt('请输入处理说明：')
                if (notes) {
                  handleResolve(record, notes)
                }
              }}
              okText="确定"
              cancelText="取消"
            >
              <Button size="small" type="primary" ghost>
                处理
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        告警中心
      </Title>

      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="活跃告警"
              value={statistics?.activeAlarms || 0}
              prefix={<BellOutlined />}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="已确认"
              value={statistics?.acknowledgedAlarms || 0}
              prefix={<WarningOutlined />}
              valueStyle={{ color: '#faad14' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="已处理"
              value={statistics?.resolvedAlarms || 0}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <Statistic
              title="严重告警"
              value={statistics?.criticalAlarms || 0}
              prefix={<ExclamationCircleOutlined />}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
      </Row>

      <Card style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <Select
            placeholder="状态筛选"
            allowClear
            style={{ width: 150 }}
            value={statusFilter}
            onChange={handleStatusFilterChange}
          >
            <Option value="Active">活跃</Option>
            <Option value="Acknowledged">已确认</Option>
            <Option value="Resolved">已处理</Option>
            <Option value="Cleared">已清除</Option>
          </Select>

          <Select
            placeholder="级别筛选"
            allowClear
            style={{ width: 150 }}
            value={levelFilter}
            onChange={handleLevelFilterChange}
          >
            <Option value="Critical">严重</Option>
            <Option value="Emergency">紧急</Option>
            <Option value="Warning">警告</Option>
            <Option value="Information">信息</Option>
          </Select>

          <Button
            icon={<ReloadOutlined />}
            onClick={loadAlarms}
            style={{ marginLeft: 'auto' }}
          >
            刷新
          </Button>
        </Space>
      </Card>

      <Card>
        {loading ? (
          <div style={{ textAlign: 'center', padding: 50 }}>
            <Spin size="large" />
          </div>
        ) : alarms.length > 0 ? (
          <Table
            columns={columns}
            dataSource={alarms}
            rowKey="id"
            scroll={{ x: 1000 }}
            pagination={{
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
            }}
          />
        ) : (
          <Empty description="暂无告警记录" />
        )}
      </Card>

      <Modal
        title="告警详情"
        open={detailModalOpen}
        onCancel={() => setDetailModalOpen(false)}
        footer={
          selectedAlarm &&
          (selectedAlarm.status === 'Active' || selectedAlarm.status === 'Acknowledged') ? (
            <Space style={{ float: 'right' }}>
              {selectedAlarm.status === 'Active' && (
                <Button
                  type="primary"
                  onClick={() => {
                    handleAcknowledge(selectedAlarm)
                  }}
                >
                  确认
                </Button>
              )}
              {(selectedAlarm.status === 'Active' ||
                selectedAlarm.status === 'Acknowledged') && (
                <Button
                  type="primary"
                  ghost
                  onClick={() => {
                    const notes = prompt('请输入处理说明：')
                    if (notes) {
                      handleResolve(selectedAlarm!, notes)
                    }
                  }}
                >
                  处理
                </Button>
              )}
            </Space>
          ) : null
        }
        width={700}
      >
        {selectedAlarm && (
          <Descriptions bordered column={2}>
            <Descriptions.Item label="告警标题" span={2}>
              <Text strong>{selectedAlarm.title}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="告警级别">
              <Tag color={getLevelColor(selectedAlarm.level)}>
                {getLevelText(selectedAlarm.level)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="当前状态">
              <Tag color={getStatusColor(selectedAlarm.status)}>
                {getStatusText(selectedAlarm.status)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="关联设备">
              {selectedAlarm.deviceName}
            </Descriptions.Item>
            <Descriptions.Item label="触发指标">
              {selectedAlarm.triggeredMetric || '-'}
            </Descriptions.Item>
            <Descriptions.Item label="触发值">
              {selectedAlarm.triggeredValue !== undefined
                ? selectedAlarm.triggeredValue
                : '-'}
            </Descriptions.Item>
            <Descriptions.Item label="触发时间">
              {dayjs(selectedAlarm.triggeredAt).format('YYYY-MM-DD HH:mm:ss')}
            </Descriptions.Item>
            {selectedAlarm.acknowledgedAt && (
              <>
                <Descriptions.Item label="确认时间">
                  {dayjs(selectedAlarm.acknowledgedAt).format('YYYY-MM-DD HH:mm:ss')}
                </Descriptions.Item>
                <Descriptions.Item label="确认人">
                  {selectedAlarm.acknowledgedBy || '-'}
                </Descriptions.Item>
              </>
            )}
            {selectedAlarm.resolvedAt && (
              <>
                <Descriptions.Item label="处理时间">
                  {dayjs(selectedAlarm.resolvedAt).format('YYYY-MM-DD HH:mm:ss')}
                </Descriptions.Item>
                <Descriptions.Item label="处理人">
                  {selectedAlarm.resolvedBy || '-'}
                </Descriptions.Item>
              </>
            )}
            <Descriptions.Item label="描述" span={2}>
              {selectedAlarm.description || '-'}
            </Descriptions.Item>
            {selectedAlarm.resolutionNotes && (
              <Descriptions.Item label="处理说明" span={2}>
                {selectedAlarm.resolutionNotes}
              </Descriptions.Item>
            )}
          </Descriptions>
        )}
      </Modal>
    </div>
  )
}

export default Alarms
