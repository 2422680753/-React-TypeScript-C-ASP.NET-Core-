export interface Device {
  id: string;
  name: string;
  deviceType: string;
  serialNumber: string;
  description?: string;
  location?: string;
  latitude?: number;
  longitude?: number;
  status: DeviceStatus;
  lastOnlineTime?: string;
  createdAt: string;
  updatedAt: string;
  isActive: boolean;
  manufacturer?: string;
  firmwareVersion?: string;
  hardwareVersion?: string;
  groupId?: string;
  groupName?: string;
}

export type DeviceStatus = 'Offline' | 'Online' | 'Warning' | 'Error' | 'Maintenance';

export interface DeviceStatistics {
  totalDevices: number;
  onlineDevices: number;
  offlineDevices: number;
  warningDevices: number;
  errorDevices: number;
  maintenanceDevices: number;
}

export interface DeviceData {
  id: string;
  deviceId: string;
  deviceName: string;
  metric: string;
  value: number;
  unit?: string;
  timestamp: string;
  quality: string;
}

export interface RealTimeData {
  deviceId: string;
  deviceName: string;
  metrics: Record<string, number>;
  units: Record<string, string | undefined>;
  timestamp: string;
  status: DeviceStatus;
}

export interface AggregatedData {
  metric: string;
  min: number;
  max: number;
  avg: number;
  sum: number;
  count: number;
  startTime: string;
  endTime: string;
}

export interface Alarm {
  id: string;
  deviceId: string;
  deviceName: string;
  ruleId?: string;
  title: string;
  description?: string;
  level: AlarmLevel;
  status: AlarmStatus;
  triggeredAt: string;
  acknowledgedAt?: string;
  resolvedAt?: string;
  acknowledgedBy?: string;
  resolvedBy?: string;
  resolutionNotes?: string;
  triggeredValue?: number;
  triggeredMetric?: string;
  aggregationId?: string;
}

export type AlarmLevel = 'Information' | 'Warning' | 'Critical' | 'Emergency';
export type AlarmStatus = 'Active' | 'Acknowledged' | 'Resolved' | 'Cleared' | 'Suppressed';

export interface AlarmRule {
  id: string;
  name: string;
  description?: string;
  deviceId?: string;
  deviceName?: string;
  groupId?: string;
  groupName?: string;
  metric: string;
  operator: ComparisonOperator;
  threshold: number;
  warningThreshold?: number;
  criticalThreshold?: number;
  durationSeconds: number;
  consecutiveOccurrences: number;
  alarmLevel: AlarmLevel;
  isEnabled: boolean;
  isNotificationEnabled: boolean;
  notificationChannels?: string;
  cooldownMinutes: number;
  lastTriggeredAt?: string;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
}

export type ComparisonOperator = 
  | 'GreaterThan' 
  | 'LessThan' 
  | 'GreaterThanOrEqual' 
  | 'LessThanOrEqual' 
  | 'Equal' 
  | 'NotEqual' 
  | 'Between' 
  | 'Outside';

export interface AlarmStatistics {
  totalAlarms: number;
  activeAlarms: number;
  acknowledgedAlarms: number;
  resolvedAlarms: number;
  criticalAlarms: number;
  warningAlarms: number;
  informationAlarms: number;
}

export interface ControlCommand {
  id: string;
  deviceId: string;
  deviceName: string;
  command: string;
  parameters?: string;
  status: CommandStatus;
  createdAt: string;
  sentAt?: string;
  executedAt?: string;
  failedAt?: string;
  createdBy?: string;
  result?: string;
  errorMessage?: string;
  retryCount?: number;
  maxRetries?: number;
  timeoutSeconds: number;
  priority: CommandPriority;
}

export type CommandStatus = 
  | 'Pending' 
  | 'Queued' 
  | 'Sent' 
  | 'Executing' 
  | 'Executed' 
  | 'Failed' 
  | 'TimedOut' 
  | 'Cancelled';

export type CommandPriority = 'Low' | 'Normal' | 'High' | 'Critical';

export interface CommandResult {
  commandId: string;
  success: boolean;
  result?: string;
  errorMessage?: string;
  timestamp: string;
}

export interface User {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
  phoneNumber?: string;
}

export type UserRole = 'User' | 'Operator' | 'Admin' | 'SuperAdmin';

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

export interface ReportRequest {
  startTime: string;
  endTime: string;
  deviceIds?: string[];
  metrics?: string[];
  granularity?: string;
  reportType?: string;
}

export interface DeviceAvailabilityReport {
  deviceId: string;
  deviceName: string;
  deviceType: string;
  availabilityPercentage: number;
  onlineDuration: string;
  offlineDuration: string;
  onlineCount: number;
  offlineCount: number;
  startTime: string;
  endTime: string;
}

export interface AlarmReport {
  alarmLevel: string;
  totalCount: number;
  activeCount: number;
  resolvedCount: number;
  averageResolutionTime: string;
  topAlarmDevices: TopAlarmDevice[];
  trend: AlarmTrend[];
  startTime: string;
  endTime: string;
}

export interface TopAlarmDevice {
  deviceId: string;
  deviceName: string;
  alarmCount: number;
  criticalCount: number;
  warningCount: number;
}

export interface AlarmTrend {
  date: string;
  totalAlarms: number;
  criticalAlarms: number;
  warningAlarms: number;
  informationAlarms: number;
}

export interface DeviceStatisticsReport {
  totalDevices: number;
  onlineDevices: number;
  offlineDevices: number;
  warningDevices: number;
  errorDevices: number;
  maintenanceDevices: number;
  onlinePercentage: number;
  offlinePercentage: number;
  warningPercentage: number;
  errorPercentage: number;
  byDeviceType: DeviceTypeStatistics[];
  byGroup: DeviceGroupStatistics[];
}

export interface DeviceTypeStatistics {
  deviceType: string;
  count: number;
  onlineCount: number;
  offlineCount: number;
}

export interface DeviceGroupStatistics {
  groupId: string;
  groupName: string;
  deviceCount: number;
  onlineCount: number;
  offlineCount: number;
}

export interface DeviceStatusUpdate {
  deviceId: string;
  status: DeviceStatus;
  timestamp: string;
  metrics?: Record<string, number>;
}

export interface ApiError {
  message: string;
  statusCode?: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface HeartbeatDto {
  deviceId: string;
  timestamp: string;
  status: DeviceStatus;
  metrics?: Record<string, number>;
  latencyMs?: number;
}

export interface HeartbeatResponseDto {
  deviceId: string;
  serverTime: string;
  nextHeartbeatIntervalMs: number;
  commands?: ControlCommand[];
}

export interface DeviceStateDto {
  deviceId: string;
  currentStatus: DeviceStatus;
  previousStatus?: DeviceStatus;
  statusChangedAt: string;
  version: number;
  metrics?: Record<string, number>;
  isConsistent: boolean;
}

export interface StateSyncRequestDto {
  lastKnownVersion?: number;
  deviceIds?: string[];
  timestamp: string;
  requestFullSnapshot: boolean;
}

export interface StateSyncResponseDto {
  timestamp: string;
  isFullSnapshot: boolean;
  states: DeviceStateDto[];
  checksum: string;
  syncId: string;
}

export interface StateConsistencyCheckDto {
  deviceId: string;
  cacheStatus: DeviceStatus;
  databaseStatus: DeviceStatus;
  isConsistent: boolean;
  lastUpdatedAt: string;
  autoFixApplied: boolean;
}

export interface DeviceOnlineEventDto {
  deviceId: string;
  deviceName: string;
  onlineTime: string;
  previousStatus?: DeviceStatus;
  connectionType: string;
  metrics?: Record<string, number>;
}

export interface DeviceOfflineEventDto {
  deviceId: string;
  deviceName: string;
  offlineTime: string;
  reason: string;
  lastHeartbeatTime?: string;
}

export interface ConnectionStatusDto {
  isConnected: boolean;
  connectionId?: string;
  reconnectAttempts: number;
  lastConnectedAt?: string;
  lastDisconnectedAt?: string;
}

export interface AlarmDeduplicationDto {
  deviceId: string;
  metric: string;
  occurrenceCount: number;
  firstTriggeredAt: string;
  lastTriggeredAt: string;
  lastValue: number;
}

export interface AlarmSuppressionDto {
  id: string;
  name: string;
  description?: string;
  type: SuppressionType;
  startTime: string;
  endTime: string;
  isActive: boolean;
  suppressedCount: number;
}

export type SuppressionType = 'Maintenance' | 'KnownIssue' | 'PlannedDowntime' | 'Custom';

export interface AlarmAggregationDto {
  id: string;
  correlationId: string;
  deviceId?: string;
  highestLevel: AlarmLevel;
  alarmCount: number;
  aggregatedTitle: string;
  firstOccurredAt: string;
  lastOccurredAt: string;
  isResolved: boolean;
  alarms: Alarm[];
}

export interface AlarmGovernanceStats {
  activeSuppressions: number;
  activeAggregations: number;
  queueSize: number;
  processedCount: number;
  droppedCount: number;
  deduplicatedCount: number;
  suppressedCount: number;
}

export type ConnectionType = 'WebSocket' | 'MQTT' | 'HTTP';
