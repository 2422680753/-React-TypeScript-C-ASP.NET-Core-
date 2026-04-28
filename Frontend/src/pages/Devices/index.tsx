import {
  Card,
  Table,
  Button,
  Tag,
  Input,
  Select,
  Space,
  Modal,
  Form,
  message,
  Popconfirm,
  Typography,
  Empty,
  Spin,
} from 'antd'
import {
  PlusOutlined,
  SearchOutlined,
  EditOutlined,
  DeleteOutlined,
  EyeOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import { deviceApi } from '@/services/api'
import type { Device, DeviceStatus } from '@/types'

const { Title } = Typography
const { Search } = Input
const { Option } = Select

const Devices = () => {
  const [loading, setLoading] = useState(false)
  const [devices, setDevices] = useState<Device[]>([])
  const [filteredDevices, setFilteredDevices] = useState<Device[]>([])
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingDevice, setEditingDevice] = useState<Device | null>(null)
  const [form] = Form.useForm()
  const navigate = useNavigate()

  useEffect(() => {
    loadDevices()
  }, [])

  const loadDevices = async () => {
    setLoading(true)
    try {
      const data = await deviceApi.getAll()
      setDevices(data)
      setFilteredDevices(data)
    } catch (error) {
      console.error('Failed to load devices:', error)
      message.error('加载设备列表失败')
    } finally {
      setLoading(false)
    }
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

  const handleSearch = (value: string) => {
    if (!value.trim()) {
      setFilteredDevices(devices)
      return
    }

    const filtered = devices.filter(
      (d) =>
        d.name.toLowerCase().includes(value.toLowerCase()) ||
        d.serialNumber.toLowerCase().includes(value.toLowerCase()) ||
        d.deviceType.toLowerCase().includes(value.toLowerCase())
    )
    setFilteredDevices(filtered)
  }

  const handleStatusFilter = (status: string | undefined) => {
    if (!status) {
      setFilteredDevices(devices)
      return
    }

    const filtered = devices.filter((d) => d.status === status)
    setFilteredDevices(filtered)
  }

  const handleTypeFilter = (type: string | undefined) => {
    if (!type) {
      setFilteredDevices(devices)
      return
    }

    const filtered = devices.filter((d) => d.deviceType === type)
    setFilteredDevices(filtered)
  }

  const handleAdd = () => {
    setEditingDevice(null)
    form.resetFields()
    setIsModalOpen(true)
  }

  const handleEdit = (device: Device) => {
    setEditingDevice(device)
    form.setFieldsValue({
      name: device.name,
      description: device.description,
      location: device.location,
      latitude: device.latitude,
      longitude: device.longitude,
      firmwareVersion: device.firmwareVersion,
      isActive: device.isActive,
    })
    setIsModalOpen(true)
  }

  const handleDelete = async (id: string) => {
    try {
      await deviceApi.delete(id)
      message.success('设备删除成功')
      loadDevices()
    } catch (error) {
      console.error('Failed to delete device:', error)
      message.error('删除设备失败')
    }
  }

  const handleSubmit = async (values: {
    name: string
    deviceType?: string
    serialNumber?: string
    description?: string
    location?: string
    latitude?: number
    longitude?: number
    manufacturer?: string
    firmwareVersion?: string
    isActive?: boolean
  }) => {
    try {
      if (editingDevice) {
        await deviceApi.update(editingDevice.id, {
          name: values.name,
          description: values.description,
          location: values.location,
          latitude: values.latitude,
          longitude: values.longitude,
          firmwareVersion: values.firmwareVersion,
          isActive: values.isActive,
        })
        message.success('设备更新成功')
      } else {
        await deviceApi.create({
          name: values.name,
          deviceType: values.deviceType || 'Sensor',
          serialNumber: values.serialNumber || `DEV-${Date.now()}`,
          description: values.description,
          location: values.location,
          latitude: values.latitude,
          longitude: values.longitude,
          manufacturer: values.manufacturer,
          firmwareVersion: values.firmwareVersion,
        })
        message.success('设备添加成功')
      }

      setIsModalOpen(false)
      loadDevices()
    } catch (error) {
      console.error('Failed to save device:', error)
      message.error('保存设备失败')
    }
  }

  const handleStatusChange = async (deviceId: string, status: DeviceStatus) => {
    try {
      await deviceApi.updateStatus(deviceId, status)
      message.success('状态更新成功')
      loadDevices()
    } catch (error) {
      console.error('Failed to update status:', error)
      message.error('更新状态失败')
    }
  }

  const deviceTypes = [...new Set(devices.map((d) => d.deviceType))]

  const columns: ColumnsType<Device> = [
    {
      title: '设备名称',
      dataIndex: 'name',
      key: 'name',
      ellipsis: true,
      width: 180,
      render: (text: string, record) => (
        <Button
          type="link"
          onClick={() => navigate(`/devices/${record.id}`)}
          style={{ padding: 0 }}
        >
          {text}
        </Button>
      ),
    },
    {
      title: '设备类型',
      dataIndex: 'deviceType',
      key: 'deviceType',
      width: 120,
    },
    {
      title: '序列号',
      dataIndex: 'serialNumber',
      key: 'serialNumber',
      width: 150,
      ellipsis: true,
    },
    {
      title: '位置',
      dataIndex: 'location',
      key: 'location',
      ellipsis: true,
      render: (text: string) => text || '-',
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string, record) => (
        <Space>
          <Tag color={getStatusColor(status)}>{getStatusText(status)}</Tag>
          <Select
            size="small"
            defaultValue={status}
            style={{ width: 100 }}
            onChange={(newStatus) => handleStatusChange(record.id, newStatus as DeviceStatus)}
          >
            <Option value="Online">在线</Option>
            <Option value="Offline">离线</Option>
            <Option value="Warning">告警</Option>
            <Option value="Error">错误</Option>
            <Option value="Maintenance">维护中</Option>
          </Select>
        </Space>
      ),
    },
    {
      title: '最后在线',
      dataIndex: 'lastOnlineTime',
      key: 'lastOnlineTime',
      width: 170,
      render: (time?: string) =>
        time ? dayjs(time).format('YYYY-MM-DD HH:mm:ss') : '-',
    },
    {
      title: '制造商',
      dataIndex: 'manufacturer',
      key: 'manufacturer',
      width: 100,
      render: (text: string) => text || '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 180,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            type="text"
            icon={<EyeOutlined />}
            onClick={() => navigate(`/devices/${record.id}`)}
          />
          <Button
            type="text"
            icon={<EditOutlined />}
            onClick={() => handleEdit(record)}
          />
          <Popconfirm
            title="确定要删除此设备吗？"
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

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        设备管理
      </Title>

      <Card style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <Search
            placeholder="搜索设备名称、序列号..."
            allowClear
            enterButton={<SearchOutlined />}
            style={{ width: 300 }}
            onSearch={handleSearch}
          />

          <Select
            placeholder="状态筛选"
            allowClear
            style={{ width: 150 }}
            onChange={handleStatusFilter}
          >
            <Option value="Online">在线</Option>
            <Option value="Offline">离线</Option>
            <Option value="Warning">告警</Option>
            <Option value="Error">错误</Option>
            <Option value="Maintenance">维护中</Option>
          </Select>

          <Select
            placeholder="类型筛选"
            allowClear
            style={{ width: 150 }}
            onChange={handleTypeFilter}
          >
            {deviceTypes.map((type) => (
              <Option key={type} value={type}>
                {type}
              </Option>
            ))}
          </Select>

          <Space style={{ marginLeft: 'auto' }}>
            <Button icon={<ReloadOutlined />} onClick={loadDevices}>
              刷新
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>
              添加设备
            </Button>
          </Space>
        </Space>
      </Card>

      <Card>
        {loading ? (
          <div style={{ textAlign: 'center', padding: 50 }}>
            <Spin size="large" />
          </div>
        ) : filteredDevices.length > 0 ? (
          <Table
            columns={columns}
            dataSource={filteredDevices}
            rowKey="id"
            scroll={{ x: 1200 }}
            pagination={{
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
            }}
          />
        ) : (
          <Empty description="暂无设备数据" />
        )}
      </Card>

      <Modal
        title={editingDevice ? '编辑设备' : '添加设备'}
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        footer={null}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          initialValues={{
            deviceType: 'Sensor',
            isActive: true,
          }}
        >
          <Form.Item
            name="name"
            label="设备名称"
            rules={[{ required: true, message: '请输入设备名称' }]}
          >
            <Input placeholder="请输入设备名称" />
          </Form.Item>

          {!editingDevice && (
            <>
              <Form.Item
                name="deviceType"
                label="设备类型"
                rules={[{ required: true, message: '请选择设备类型' }]}
              >
                <Select>
                  <Option value="Sensor">传感器</Option>
                  <Option value="Actuator">执行器</Option>
                  <Option value="Gateway">网关</Option>
                  <Option value="Controller">控制器</Option>
                  <Option value="Camera">摄像头</Option>
                  <Option value="Other">其他</Option>
                </Select>
              </Form.Item>

              <Form.Item
                name="serialNumber"
                label="序列号"
                rules={[{ required: true, message: '请输入序列号' }]}
              >
                <Input placeholder="请输入设备序列号" />
              </Form.Item>

              <Form.Item name="manufacturer" label="制造商">
                <Input placeholder="请输入制造商" />
              </Form.Item>
            </>
          )}

          <Form.Item name="description" label="描述">
            <Input.TextArea rows={3} placeholder="请输入设备描述" />
          </Form.Item>

          <Form.Item name="location" label="位置">
            <Input placeholder="请输入设备位置" />
          </Form.Item>

          <Space>
            <Form.Item name="latitude" label="纬度">
              <Input type="number" placeholder="纬度" style={{ width: 180 }} />
            </Form.Item>

            <Form.Item name="longitude" label="经度">
              <Input type="number" placeholder="经度" style={{ width: 180 }} />
            </Form.Item>
          </Space>

          <Form.Item name="firmwareVersion" label="固件版本">
            <Input placeholder="请输入固件版本" />
          </Form.Item>

          {editingDevice && (
            <Form.Item name="isActive" label="状态" valuePropName="checked">
              <Select>
                <Option value={true}>激活</Option>
                <Option value={false}>禁用</Option>
              </Select>
            </Form.Item>
          )}

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

export default Devices
