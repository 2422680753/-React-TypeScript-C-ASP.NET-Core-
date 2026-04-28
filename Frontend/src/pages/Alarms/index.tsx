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
  Badge,
  Timeline,
} from 'antd'
import {
  WarningOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  BellOutlined,
  ExclamationCircleOutlined,
  StopOutlined,
  DashboardOutlined,
} from '@ant-design/icons'
import { useEffect, useState, useCallback, useRef, useMemo } from 'react'
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
  const [statistics, setStatistics] = useState<AlarmStatistics | null>(null)
  const [selectedAlarm, setSelectedAlarm] = useState<Alarm | null>(null)
  const [detailModalOpen, setDetailModalOpen] = useState(false)
  const [statusFilter, setStatusFilter] = useState<string>()
  const [levelFilter, setLevelFilter] = useState<string>()
  const [resolveModalOpen, setResolveModalOpen] = useState(false)
  const [resolveNotes, setResolveNotes] = useState('')
  
  const {
    alarms,
    addAlarm,
    updateAlarm,
    resetUnreadCount,
    setAlarms,
    unreadCount,
    alarmIdSet,
    addAlarmsBatch,
  } = useAlarmStore()

  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true
    loadAlarms()
    setupRealTimeListeners()
    resetUnreadCount()

    return () => {
      mountedRef.current = false
    }
  }, [])

  const loadAlarms = useCallback(async () => {
    if (!mountedRef.current) return
    
    setLoading(true)
    try {
      const [alarmData, statsData] = await Promise.all([
        alarmApi.getAll(statusFilter, levelFilter, undefined, 200),
        alarmApi.getStatistics(),
      ])

      if (mountedRef.current) {
        setAlarms(alarmData)
        setStatistics(statsData)
      }
    } catch (error) {
      console.error('Failed to load alarms:', error)
      if (mountedRef.current) {
        message.error('加载告警数据失败')
      }
    } finally {
      if (mountedRef.current) {
        setLoading(false)
      }
    }
  }, [statusFilter, levelFilter])

  const setupRealTimeListeners = useCallback(() => {
    const unsubAlarm = signalRService.onAlarm((alarm) => {
      if (mountedRef.current) {
        if (!alarmIdSet.has(alarm.id)) {
          addAlarm(alarm)
        }
      }
    })

    const unsubAlarmUpdate = signalRService.onAlarmUpdate((alarm) => {
      if (mountedRef.current) {
        updateAlarm(alarm)
      }
    })

    return () => {
      unsubAlarm()
      unsubAlarmUpdate()
    }
  }, [alarmIdSet])

  const filteredAlarms = useMemo(() => {
    let result = alarms

    if (statusFilter) {
      result = result.filter((a) => a.status === statusFilter)
    }

    if (levelFilter) {
      result = result.filter((a) => a.level === levelFilter)
    }

    return result
  }, [alarms, statusFilter, levelFilter])

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

  const getLevelPriority = (level: string) => {
    const priorities: Record<string, number> = {
      Emergency: 4,
      Critical: 3,
      Warning: 2,
      Information: 1,
    }
    return priorities[level] || 0
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

  const handleAcknowledge = async (alarm: Alarm) => {
    try {
      const updatedAlarm = await alarmApi.acknowledge(alarm.id)
      message.success('告警已确认')
      updateAlarm(updatedAlarm)
      setSelectedAlarm(updatedAlarm)
    } catch (error) {
      console.error('Failed to acknowledge alarm:', error)
      message.error('确认告警失败')
    }
  }

  const handleResolve = async () => {
    if (!selectedAlarm) return

    try {
      const updatedAlarm = await alarmApi.resolve(selectedAlarm.id, resolveNotes)
      message.success('告警已处理')
      updateAlarm(updatedAlarm)
      setSelectedAlarm(updatedAlarm)
      setResolveModalOpen(false)
      setResolveNotes('')
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

  const openResolveModal = (alarm: Alarm) => {
    setSelectedAlarm(alarm)
    setResolveNotes('')
    setResolveModalOpen(true)
  }

  const columns: ColumnsType<Alarm> = [
    {
      title: '级别',
      dataIndex: 'level',
      key: 'level',
      width: 100,
      sorter: (a, b) => getLevelPriority(b.level) - getLevelPriority(a.level),
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
          style={{ padding: 0, textAlign: 'left' }}
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
      filters: [
        { text: '活跃', value: 'Active' },
        { text: '已确认', value: 'Acknowledged' },
        { text: '已处理', value: 'Resolved' },
      ],
      onFilter: (value, record) => record.status === value,
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
        <span style={{ fontFamily: 'monospace' }}>
          {value !== undefined ? value.toFixed(2) : '-'}
        </span>
      ),
    },
    {
      title: '触发时间',
      dataIndex: 'triggeredAt',
      key: 'triggeredAt',
      width: 170,
      sorter: (a, b) => new Date(a.triggeredAt).getTime() - new Date(b.triggeredAt).getTime(),
      render: (time: string) => dayjs(time).format('YYYY-MM-DD HH:mm:ss'),
    },
    {
      title: '操作',
      key: 'action',
      width: 200,
      fixed: 'right',
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
            <Button
              size="small"
              type="primary"
              ghost
              onClick={() => openResolveModal(record)}
            >
              处理
            </Button>
          )}
        </Space>
      ),
    },
  ]

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        告警中心
        {unreadCount > 0 && (
          <Badge
            count={unreadCount}
            style={{ marginLeft: 16 }}
            showZero={false}
            overflowCount={99}
          />
        )}
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
              prefix={<StopOutlined />}
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
            <Option value="Emergency">紧急</Option>
            <Option value="Critical">严重</Option>
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
        ) : filteredAlarms.length > 0 ? (
          <Table
            columns={columns}
            dataSource={filteredAlarms}
            rowKey="id"
            scroll={{ x: 1100 }}
            pagination={{
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条告警`,
              pageSize: 10,
              pageSizeOptions: ['10', '20', '50', '100'],
            }}
          />
        ) : (
          <Empty
            description={
              <Space direction="vertical" align="center">
                <DashboardOutlined style={{ fontSize: 48, color: '#ccc' }} />
                <Text type="secondary">暂无告警记录</Text>
              </Space>
            }
          />
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
                  onClick={() => openResolveModal(selectedAlarm)}
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
                ? selectedAlarm.triggeredValue.toFixed(2)
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

      <Modal
        title="处理告警"
        open={resolveModalOpen}
        onOk={handleResolve}
        onCancel={() => setResolveModalOpen(false)}
        okText="确定处理"
        cancelText="取消"
        width={500}
      >
        <div style={{ marginBottom: 16 }}>
          <Text strong>告警标题：</Text>
          <Text>{selectedAlarm?.title}</Text>
        </div>
        <div style={{ marginBottom: 16 }}>
          <Text strong>当前状态：</Text>
          <Tag color={selectedAlarm ? getStatusColor(selectedAlarm.status) : 'default'}>
            {selectedAlarm ? getStatusText(selectedAlarm.status) : ''}
          </Tag>
        </div>
        <div>
          <Text strong>处理说明：</Text>
          <TextArea
            rows={4}
            value={resolveNotes}
            onChange={(e) => setResolveNotes(e.target.value)}
            placeholder="请输入处理说明..."
            style={{ marginTop: 8 }}
          />
        </div>
      </Modal>
    </div>
  )
}

export default Alarms
