import {
  Card,
  Table,
  Button,
  Tag,
  Switch,
  Modal,
  Form,
  Input,
  Select,
  InputNumber,
  message,
  Popconfirm,
  Space,
  Typography,
  Empty,
  Spin,
} from 'antd'
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  SettingOutlined,
} from '@ant-design/icons'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { alarmApi, deviceApi } from '@/services/api'
import type { AlarmRule, Device, ComparisonOperator, AlarmLevel } from '@/types'

const { Title } = Typography
const { TextArea } = Input
const { Option } = Select

const AlarmRules = () => {
  const [loading, setLoading] = useState(false)
  const [rules, setRules] = useState<AlarmRule[]>([])
  const [devices, setDevices] = useState<Device[]>([])
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingRule, setEditingRule] = useState<AlarmRule | null>(null)
  const [form] = Form.useForm()

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    setLoading(true)
    try {
      const [rulesData, devicesData] = await Promise.all([
        alarmApi.getAllRules(),
        deviceApi.getAll(),
      ])

      setRules(rulesData)
      setDevices(devicesData)
    } catch (error) {
      console.error('Failed to load alarm rules:', error)
      message.error('加载告警规则失败')
    } finally {
      setLoading(false)
    }
  }

  const getOperatorText = (operator: string) => {
    const texts: Record<string, string> = {
      GreaterThan: '大于',
      LessThan: '小于',
      GreaterThanOrEqual: '大于等于',
      LessThanOrEqual: '小于等于',
      Equal: '等于',
      NotEqual: '不等于',
      Between: '介于',
      Outside: '超出',
    }
    return texts[operator] || operator
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

  const handleAdd = () => {
    setEditingRule(null)
    form.resetFields()
    setIsModalOpen(true)
  }

  const handleEdit = (rule: AlarmRule) => {
    setEditingRule(rule)
    form.setFieldsValue({
      name: rule.name,
      description: rule.description,
      deviceId: rule.deviceId,
      metric: rule.metric,
      operator: rule.operator,
      threshold: rule.threshold,
      warningThreshold: rule.warningThreshold,
      criticalThreshold: rule.criticalThreshold,
      durationSeconds: rule.durationSeconds,
      consecutiveOccurrences: rule.consecutiveOccurrences,
      alarmLevel: rule.alarmLevel,
      isEnabled: rule.isEnabled,
      isNotificationEnabled: rule.isNotificationEnabled,
      cooldownMinutes: rule.cooldownMinutes,
    })
    setIsModalOpen(true)
  }

  const handleDelete = async (id: string) => {
    try {
      await alarmApi.deleteRule(id)
      message.success('规则删除成功')
      loadData()
    } catch (error) {
      console.error('Failed to delete rule:', error)
      message.error('删除规则失败')
    }
  }

  const handleToggleStatus = async (rule: AlarmRule, enabled: boolean) => {
    try {
      await alarmApi.updateRule(rule.id, { isEnabled: enabled })
      message.success(`规则已${enabled ? '启用' : '禁用'}`)
      loadData()
    } catch (error) {
      console.error('Failed to toggle rule status:', error)
      message.error('更新规则状态失败')
    }
  }

  const handleSubmit = async (values: {
    name: string
    description?: string
    deviceId?: string
    metric: string
    operator: string
    threshold: number
    warningThreshold?: number
    criticalThreshold?: number
    durationSeconds?: number
    consecutiveOccurrences?: number
    alarmLevel?: string
    isEnabled?: boolean
    isNotificationEnabled?: boolean
    cooldownMinutes?: number
  }) => {
    try {
      if (editingRule) {
        await alarmApi.updateRule(editingRule.id, values)
        message.success('规则更新成功')
      } else {
        await alarmApi.createRule({
          ...values,
          durationSeconds: values.durationSeconds || 0,
          consecutiveOccurrences: values.consecutiveOccurrences || 1,
          alarmLevel: values.alarmLevel || 'Warning',
          isEnabled: values.isEnabled ?? true,
          isNotificationEnabled: values.isNotificationEnabled ?? true,
          cooldownMinutes: values.cooldownMinutes || 5,
        })
        message.success('规则创建成功')
      }

      setIsModalOpen(false)
      loadData()
    } catch (error) {
      console.error('Failed to save rule:', error)
      message.error('保存规则失败')
    }
  }

  const columns: ColumnsType<AlarmRule> = [
    {
      title: '规则名称',
      dataIndex: 'name',
      key: 'name',
      ellipsis: true,
    },
    {
      title: '关联设备',
      dataIndex: 'deviceName',
      key: 'deviceName',
      width: 150,
      render: (text: string, record) => text || record.deviceId || '所有设备',
    },
    {
      title: '监控指标',
      dataIndex: 'metric',
      key: 'metric',
      width: 120,
    },
    {
      title: '触发条件',
      key: 'condition',
      width: 200,
      render: (_, record) => (
        <span>
          {getOperatorText(record.operator)} {record.threshold}
          {record.criticalThreshold && ` (临界: ${record.criticalThreshold})`}
        </span>
      ),
    },
    {
      title: '告警级别',
      dataIndex: 'alarmLevel',
      key: 'alarmLevel',
      width: 100,
      render: (level: string) => (
        <Tag color={getLevelColor(level)}>{getLevelText(level)}</Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'isEnabled',
      key: 'isEnabled',
      width: 100,
      render: (enabled: boolean, record) => (
        <Switch
          checked={enabled}
          onChange={(checked) => handleToggleStatus(record, checked)}
          checkedChildren="启用"
          unCheckedChildren="禁用"
        />
      ),
    },
    {
      title: '最后触发',
      dataIndex: 'lastTriggeredAt',
      key: 'lastTriggeredAt',
      width: 170,
      render: (time?: string) =>
        time ? dayjs(time).format('YYYY-MM-DD HH:mm:ss') : '从未触发',
    },
    {
      title: '操作',
      key: 'action',
      width: 150,
      render: (_, record) => (
        <Space size="small">
          <Button
            type="text"
            icon={<EditOutlined />}
            onClick={() => handleEdit(record)}
          />
          <Popconfirm
            title="确定要删除此规则吗？"
            onConfirm={() => handleDelete(record.id)}
            okText="确定"
            cancelText="取消"
          >
            <Button type="text" icon={<DeleteOutlined />} danger />
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const commonMetrics = [
    'temperature',
    'humidity',
    'power',
    'voltage',
    'current',
    'pressure',
    'flow',
    'speed',
    'cpu_usage',
    'memory_usage',
    'disk_usage',
    'network_traffic',
    'battery_level',
    'signal_strength',
  ]

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        告警规则配置
      </Title>

      <Card style={{ marginBottom: 16 }}>
        <Space style={{ float: 'right' }}>
          <Button icon={<ReloadOutlined />} onClick={loadData}>
            刷新
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>
            添加规则
          </Button>
        </Space>
      </Card>

      <Card>
        {loading ? (
          <div style={{ textAlign: 'center', padding: 50 }}>
            <Spin size="large" />
          </div>
        ) : rules.length > 0 ? (
          <Table
            columns={columns}
            dataSource={rules}
            rowKey="id"
            scroll={{ x: 1200 }}
            pagination={{
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
            }}
          />
        ) : (
          <Empty
            description={
              <span>
                暂无告警规则，点击右上角"添加规则"创建
              </span>
            }
          >
            <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>
              添加规则
            </Button>
          </Empty>
        )}
      </Card>

      <Modal
        title={editingRule ? '编辑告警规则' : '添加告警规则'}
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        footer={null}
        width={700}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          initialValues={{
            operator: 'GreaterThan',
            threshold: 0,
            durationSeconds: 0,
            consecutiveOccurrences: 1,
            alarmLevel: 'Warning',
            isEnabled: true,
            isNotificationEnabled: true,
            cooldownMinutes: 5,
          }}
        >
          <Form.Item
            name="name"
            label="规则名称"
            rules={[{ required: true, message: '请输入规则名称' }]}
          >
            <Input placeholder="请输入规则名称" />
          </Form.Item>

          <Form.Item name="description" label="描述">
            <TextArea rows={2} placeholder="请输入规则描述" />
          </Form.Item>

          <Form.Item name="deviceId" label="关联设备">
            <Select placeholder="选择设备（不选则应用于所有设备）" allowClear>
              {devices.map((device) => (
                <Option key={device.id} value={device.id}>
                  {device.name} ({device.serialNumber})
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="metric"
            label="监控指标"
            rules={[{ required: true, message: '请输入或选择监控指标' }]}
          >
            <Select
              mode="tags"
              placeholder="请输入或选择监控指标"
              options={commonMetrics.map((m) => ({ label: m, value: m }))}
              maxTagCount={1}
            />
          </Form.Item>

          <Form.Item
            name="operator"
            label="比较运算符"
            rules={[{ required: true, message: '请选择比较运算符' }]}
          >
            <Select>
              <Option value="GreaterThan">大于 (>)</Option>
              <Option value="LessThan">小于 (<)</Option>
              <Option value="GreaterThanOrEqual">大于等于 (>=)</Option>
              <Option value="LessThanOrEqual">小于等于 (<=)</Option>
              <Option value="Equal">等于 (==)</Option>
              <Option value="NotEqual">不等于 (!=)</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="threshold"
            label="阈值"
            rules={[{ required: true, message: '请输入阈值' }]}
          >
            <InputNumber
              style={{ width: '100%' }}
              placeholder="请输入阈值"
              step={0.01}
            />
          </Form.Item>

          <Form.Item name="warningThreshold" label="警告阈值">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="请输入警告阈值（可选）"
              step={0.01}
            />
          </Form.Item>

          <Form.Item name="criticalThreshold" label="临界阈值">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="请输入临界阈值（可选）"
              step={0.01}
            />
          </Form.Item>

          <Form.Item name="durationSeconds" label="持续时间（秒）">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="条件持续多少秒后触发"
              min={0}
            />
          </Form.Item>

          <Form.Item name="consecutiveOccurrences" label="连续触发次数">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="连续满足条件多少次后触发"
              min={1}
            />
          </Form.Item>

          <Form.Item name="alarmLevel" label="告警级别">
            <Select>
              <Option value="Information">信息</Option>
              <Option value="Warning">警告</Option>
              <Option value="Critical">严重</Option>
              <Option value="Emergency">紧急</Option>
            </Select>
          </Form.Item>

          <Form.Item name="cooldownMinutes" label="冷却时间（分钟）">
            <InputNumber
              style={{ width: '100%' }}
              placeholder="告警触发后冷却多久才能再次触发"
              min={0}
            />
          </Form.Item>

          <Form.Item name="isEnabled" label="启用规则" valuePropName="checked">
            <Switch checkedChildren="是" unCheckedChildren="否" />
          </Form.Item>

          <Form.Item name="isNotificationEnabled" label="启用通知" valuePropName="checked">
            <Switch checkedChildren="是" unCheckedChildren="否" />
          </Form.Item>

          <Form.Item style={{ textAlign: 'right', marginBottom: 0 }}>
            <Space>
              <Button onClick={() => setIsModalOpen(false)}>取消</Button>
              <Button type="primary" htmlType="submit">
                确定
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}

export default AlarmRules
