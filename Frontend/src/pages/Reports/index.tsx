import {
  Card,
  Tabs,
  DatePicker,
  Button,
  Table,
  Tag,
  Typography,
  Empty,
  Spin,
  Row,
  Col,
  Statistic,
  Progress,
  Select,
  Space,
  Descriptions,
} from 'antd'
import {
  BarChartOutlined,
  DownloadOutlined,
  ReloadOutlined,
  DeviceHubOutlined,
  AlertOutlined,
  CheckCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  LineChart,
  Line,
  PieChart,
  Pie,
  Cell,
  Legend,
  AreaChart,
  Area,
} from 'recharts'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'
import type { Dayjs } from 'dayjs'
import { reportApi, deviceApi, alarmApi } from '@/services/api'
import type {
  DeviceStatisticsReport,
  DeviceAvailabilityReport,
  AlarmReport,
  CommandExecutionReport,
  Device,
  AlarmStatistics,
} from '@/types'

const { Title } = Typography
const { RangePicker } = DatePicker
const { TabPane } = Tabs
const { Option } = Select

const Reports = () => {
  const [loading, setLoading] = useState(false)
  const [deviceStats, setDeviceStats] = useState<DeviceStatisticsReport | null>(null)
  const [alarmStats, setAlarmStats] = useState<AlarmStatistics | null>(null)
  const [availabilityReports, setAvailabilityReports] = useState<DeviceAvailabilityReport[]>([])
  const [alarmReport, setAlarmReport] = useState<AlarmReport | null>(null)
  const [commandReport, setCommandReport] = useState<CommandExecutionReport | null>(null)
  const [devices, setDevices] = useState<Device[]>([])
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
    dayjs().subtract(7, 'day'),
    dayjs(),
  ])
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<string[]>([])

  useEffect(() => {
    loadInitialData()
  }, [])

  const loadInitialData = async () => {
    setLoading(true)
    try {
      const [stats, alarmStatsData, devicesData] = await Promise.all([
        reportApi.getDeviceStatistics(),
        alarmApi.getStatistics(),
        deviceApi.getAll(),
      ])

      setDeviceStats(stats)
      setAlarmStats(alarmStatsData)
      setDevices(devicesData)
    } catch (error) {
      console.error('Failed to load initial data:', error)
    } finally {
      setLoading(false)
    }
  }

  const loadReports = async () => {
    setLoading(true)
    try {
      const [availability, alarm, command] = await Promise.all([
        reportApi.getDeviceAvailability({
          startTime: dateRange[0].toISOString(),
          endTime: dateRange[1].toISOString(),
          deviceIds: selectedDeviceIds.length > 0 ? selectedDeviceIds : undefined,
        }),
        reportApi.getAlarmReport({
          startTime: dateRange[0].toISOString(),
          endTime: dateRange[1].toISOString(),
          deviceIds: selectedDeviceIds.length > 0 ? selectedDeviceIds : undefined,
        }),
        reportApi.getCommandExecutionReport({
          startTime: dateRange[0].toISOString(),
          endTime: dateRange[1].toISOString(),
          deviceIds: selectedDeviceIds.length > 0 ? selectedDeviceIds : undefined,
        }),
      ])

      setAvailabilityReports(availability)
      setAlarmReport(alarm)
      setCommandReport(command)
    } catch (error) {
      console.error('Failed to load reports:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleDateRangeChange = (dates: [Dayjs | null, Dayjs | null) => {
    if (dates && dates[0] && dates[1]) {
      setDateRange([dates[0], dates[1]])
    }
  }

  const handleDeviceFilterChange = (values: string[]) => {
    setSelectedDeviceIds(values)
  }

  const handleExport = async (reportType: string) => {
    try {
      const blob = await reportApi.exportCsv(reportType, {
        startTime: dateRange[0].toISOString(),
        endTime: dateRange[1].toISOString(),
      })

      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${reportType}_report_${dayjs().format('YYYYMMDD_HHmmss')}.csv`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      window.URL.revokeObjectURL(url)
    } catch (error) {
      console.error('Failed to export report:', error)
    }
  }

  const deviceStatusChartData = deviceStats
    ? [
        { name: '在线', value: deviceStats.onlineDevices, color: '#52c41a' },
        { name: '离线', value: deviceStats.offlineDevices, color: '#8c8c8c' },
        { name: '告警', value: deviceStats.warningDevices, color: '#faad14' },
        { name: '错误', value: deviceStats.errorDevices, color: '#ff4d4f' },
        { name: '维护', value: deviceStats.maintenanceDevices, color: '#1890ff' },
      ]
    : []

  const alarmLevelChartData = alarmStats
    ? [
        { name: '严重', value: alarmStats.criticalAlarms, color: '#ff4d4f' },
        { name: '警告', value: alarmStats.warningAlarms, color: '#faad14' },
        { name: '信息', value: alarmStats.informationAlarms, color: '#1890ff' },
      ]
    : []

  const deviceTypeChartData = deviceStats?.byDeviceType.map((item) => ({
    name: item.deviceType,
    总数: item.count,
    在线: item.onlineCount,
    离线: item.offlineCount,
  })) || []

  const availabilityColumns: ColumnsType<DeviceAvailabilityReport> = [
    {
      title: '设备名称',
      dataIndex: 'deviceName',
      key: 'deviceName',
    },
    {
      title: '设备类型',
      dataIndex: 'deviceType',
      key: 'deviceType',
      width: 120,
    },
    {
      title: '可用率',
      dataIndex: 'availabilityPercentage',
      key: 'availabilityPercentage',
      width: 150,
      render: (percent: number) => (
        <Progress
          percent={Math.round(percent)}
          status={percent >= 90 ? 'normal' : percent >= 70 ? 'exception' : 'exception'}
          size="small"
        />
      ),
    },
    {
      title: '在线时长',
      dataIndex: 'onlineDuration',
      key: 'onlineDuration',
      width: 150,
      render: (duration: string) => {
        const dur = dayjs.duration(duration)
        return `${dur.asHours().toFixed(1)} 小时`
      },
    },
    {
      title: '在线次数',
      dataIndex: 'onlineCount',
      key: 'onlineCount',
      width: 100,
    },
  ]

  if (loading && !deviceStats) {
    return (
      <div style={{ textAlign: 'center', padding: 100 }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>加载报表数据中...</div>
      </div>
    )
  }

  return (
    <div>
      <Title level={2} style={{ marginBottom: 24 }}>
        运维报表
      </Title>

      <Card style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <span>时间范围：</span>
          <RangePicker
            value={dateRange}
            onChange={handleDateRangeChange}
            style={{ width: 300 }}
          />

          <span>设备筛选：</span>
          <Select
            mode="multiple"
            placeholder="选择设备（不选则全部）"
            style={{ width: 300 }}
            allowClear
            value={selectedDeviceIds.length > 0 ? selectedDeviceIds : undefined}
            onChange={handleDeviceFilterChange}
            maxTagCount={3}
          >
            {devices.map((device) => (
              <Option key={device.id} value={device.id}>
                {device.name}
              </Option>
            ))}
          </Select>

          <Button type="primary" icon={<ReloadOutlined />} onClick={loadReports}>
            加载报表
          </Button>

          <Button icon={<DownloadOutlined />} onClick={() => handleExport('alarm')}>
            导出告警
          </Button>
          <Button icon={<DownloadOutlined />} onClick={() => handleExport('device')}>
            导出设备
          </Button>
        </Space>
      </Card>

      <Tabs defaultActiveKey="overview">
        <TabPane tab="统计概览" key="overview">
          <Row gutter={[16, 16]}>
            <Col xs={24} lg={12}>
              <Card title="设备统计">
                {deviceStats && (
                  <div>
                    <Row gutter={[16, 16]}>
                      <Col xs={12}>
                        <Statistic
                          title="设备总数"
                          value={deviceStats.totalDevices}
                          prefix={<DeviceHubOutlined />}
                        />
                      </Col>
                      <Col xs={12}>
                        <Statistic
                          title="在线率"
                          value={deviceStats.onlinePercentage}
                          suffix="%"
                          prefix={<CheckCircleOutlined />}
                          valueStyle={{ color: '#52c41a' }}
                        />
                      </Col>
                    </Row>

                    <div style={{ height: 250, marginTop: 24 }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                          <Pie
                            data={deviceStatusChartData}
                            dataKey="value"
                            nameKey="name"
                            cx="50%"
                            cy="50%"
                            outerRadius={80}
                            label={({ name, percent }) =>
                              `${name} ${(percent * 100).toFixed(0)}%`
                            }
                          >
                            {deviceStatusChartData.map((entry, index) => (
                              <Cell key={`cell-${index}`} fill={entry.color} />
                            ))}
                          </Pie>
                          <Tooltip />
                          <Legend />
                        </PieChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                )}
              </Card>
            </Col>

            <Col xs={24} lg={12}>
              <Card title="告警统计">
                {alarmStats && (
                  <div>
                    <Row gutter={[16, 16]}>
                      <Col xs={6}>
                        <Statistic
                          title="活跃"
                          value={alarmStats.activeAlarms}
                          prefix={<AlertOutlined />}
                          valueStyle={{ color: '#ff4d4f' }}
                        />
                      </Col>
                      <Col xs={6}>
                        <Statistic
                          title="已确认"
                          value={alarmStats.acknowledgedAlarms}
                          prefix={<WarningOutlined />}
                          valueStyle={{ color: '#faad14' }}
                        />
                      </Col>
                      <Col xs={6}>
                        <Statistic
                          title="已处理"
                          value={alarmStats.resolvedAlarms}
                          prefix={<CheckCircleOutlined />}
                          valueStyle={{ color: '#52c41a' }}
                        />
                      </Col>
                      <Col xs={6}>
                        <Statistic
                          title="总告警"
                          value={alarmStats.totalAlarms}
                          prefix={<BarChartOutlined />}
                        />
                      </Col>
                    </Row>

                    <div style={{ height: 250, marginTop: 24 }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                          <Pie
                            data={alarmLevelChartData}
                            dataKey="value"
                            nameKey="name"
                            cx="50%"
                            cy="50%"
                            outerRadius={80}
                            label={({ name, percent }) =>
                              `${name} ${(percent * 100).toFixed(0)}%`
                            }
                          >
                            {alarmLevelChartData.map((entry, index) => (
                              <Cell key={`cell-${index}`} fill={entry.color} />
                            ))}
                          </Pie>
                          <Tooltip />
                          <Legend />
                        </PieChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                )}
              </Card>
            </Col>
          </Row>

          {deviceTypeChartData.length > 0 && (
            <Card title="按设备类型统计" style={{ marginTop: 16 }}>
              <div style={{ height: 300 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={deviceTypeChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis />
                    <Tooltip />
                    <Legend />
                    <Bar dataKey="总数" fill="#1890ff" />
                    <Bar dataKey="在线" fill="#52c41a" />
                    <Bar dataKey="离线" fill="#8c8c8c" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Card>
          )}
        </TabPane>

        <TabPane tab="设备可用性" key="availability">
          <Card>
            {availabilityReports.length > 0 ? (
            <div>
              <div style={{ height: 300, marginBottom: 24 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={availabilityReports}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="deviceName" />
                    <YAxis domain={[0, 100]} />
                    <Tooltip formatter={(value) => [`${value}%`, '可用率']} />
                    <Bar
                      dataKey="availabilityPercentage"
                      name="可用率"
                      fill="#1890ff"
                      label={{ position: 'top', formatter: (v) => `${v}%` }}
                    >
                      {availabilityReports.map((entry, index) => (
                        <Cell
                          key={`cell-${index}`}
                          fill={
                            entry.availabilityPercentage >= 90
                              ? '#52c41a'
                              : entry.availabilityPercentage >= 70
                              ? '#faad14'
                              : '#ff4d4f'
                          }
                        />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <Table
                columns={availabilityColumns}
                dataSource={availabilityReports}
                rowKey="deviceId"
                pagination={{ pageSize: 10 }}
              />
            </div>
          ) : (
            <Empty description="请选择时间范围并点击加载报表" />
          )}
          </Card>
        </TabPane>

        <TabPane tab="告警趋势" key="alarmTrend">
          <Card>
            {alarmReport ? (
              <div>
                <Descriptions bordered column={4} style={{ marginBottom: 24 }}>
                  <Descriptions.Item label="总告警数">
                    {alarmReport.totalCount}
                  </Descriptions.Item>
                  <Descriptions.Item label="活跃告警">
                    <Tag color="error">{alarmReport.activeCount}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="已处理">
                    <Tag color="success">{alarmReport.resolvedCount}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="平均处理时间">
                    {alarmReport.averageResolutionTime
                      ? dayjs.duration(alarmReport.averageResolutionTime).asMinutes().toFixed(1) + ' 分钟'
                      : '-'}
                  </Descriptions.Item>
                </Descriptions>

                {alarmReport.trend.length > 0 && (
                  <div style={{ height: 300, marginBottom: 24 }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart data={alarmReport.trend}>
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis
                          dataKey="date"
                          tickFormatter={(date) => dayjs(date).format('MM-DD')}
                        />
                        <YAxis />
                        <Tooltip
                          labelFormatter={(date) => dayjs(date).format('YYYY-MM-DD')}
                        />
                        <Legend />
                        <Area
                          type="monotone"
                          dataKey="totalAlarms"
                          name="总告警"
                          stroke="#1890ff"
                          fill="#1890ff"
                          fillOpacity={0.2}
                        />
                        <Area
                          type="monotone"
                          dataKey="criticalAlarms"
                          name="严重告警"
                          stroke="#ff4d4f"
                          fill="#ff4d4f"
                          fillOpacity={0.2}
                        />
                        <Area
                          type="monotone"
                          dataKey="warningAlarms"
                          name="警告告警"
                          stroke="#faad14"
                          fill="#faad14"
                          fillOpacity={0.2}
                        />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                )}

                {alarmReport.topAlarmDevices.length > 0 && (
                  <Card title="告警最多的设备" size="small" type="inner">
                    <div style={{ height: 250 }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart data={alarmReport.topAlarmDevices} layout="vertical">
                          <CartesianGrid strokeDasharray="3 3" />
                          <XAxis type="number" />
                          <YAxis
                            dataKey="deviceName"
                            type="category"
                            width={100}
                          />
                          <Tooltip />
                          <Legend />
                          <Bar dataKey="alarmCount" name="总告警" fill="#1890ff" />
                          <Bar dataKey="criticalCount" name="严重" fill="#ff4d4f" />
                          <Bar dataKey="warningCount" name="警告" fill="#faad14" />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </Card>
                )}
              </div>
            ) : (
              <Empty description="请选择时间范围并点击加载报表" />
            )}
          </Card>
        </TabPane>

        <TabPane tab="命令执行" key="command">
          <Card>
            {commandReport ? (
              <div>
                <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                  <Col xs={6}>
                    <Statistic title="总命令数" value={commandReport.totalCommands} />
                  </Col>
                  <Col xs={6}>
                    <Statistic
                      title="成功"
                      value={commandReport.successfulCommands}
                      valueStyle={{ color: '#52c41a' }}
                    />
                  </Col>
                  <Col xs={6}>
                    <Statistic
                      title="失败"
                      value={commandReport.failedCommands}
                      valueStyle={{ color: '#ff4d4f' }}
                    />
                  </Col>
                  <Col xs={6}>
                    <Statistic
                      title="成功率"
                      value={commandReport.successRate}
                      suffix="%"
                      valueStyle={{
                        color: commandReport.successRate >= 90 ? '#52c41a' : '#faad14',
                      }}
                    />
                  </Col>
                </Row>

                {commandReport.trend.length > 0 && (
                  <div style={{ height: 300, marginBottom: 24 }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={commandReport.trend}>
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis
                          dataKey="date"
                          tickFormatter={(date) => dayjs(date).format('MM-DD')}
                        />
                        <YAxis />
                        <Tooltip
                          labelFormatter={(date) => dayjs(date).format('YYYY-MM-DD')}
                        />
                        <Legend />
                        <Line
                          type="monotone"
                          dataKey="totalCommands"
                          name="总命令"
                          stroke="#1890ff"
                        />
                        <Line
                          type="monotone"
                          dataKey="successfulCommands"
                          name="成功"
                          stroke="#52c41a"
                        />
                        <Line
                          type="monotone"
                          dataKey="failedCommands"
                          name="失败"
                          stroke="#ff4d4f"
                        />
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                )}
              </div>
            ) : (
              <Empty description="请选择时间范围并点击加载报表" />
            )}
          </Card>
        </TabPane>
      </Tabs>
    </div>
  )
}

export default Reports
