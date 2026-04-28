import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import type {
  Device,
  DeviceStatistics,
  DeviceData,
  RealTimeData,
  AggregatedData,
  Alarm,
  AlarmRule,
  AlarmStatistics,
  ControlCommand,
  CommandResult,
  User,
  AuthResponse,
  DeviceStatisticsReport,
  AlarmReport,
  DeviceAvailabilityReport,
  CommandExecutionReport,
} from '@/types';

const API_BASE_URL = '/api';

const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('token');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export const authApi = {
  login: async (username: string, password: string): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/login', { username, password });
    return response.data;
  },

  register: async (data: {
    username: string;
    email: string;
    password: string;
    firstName?: string;
    lastName?: string;
    phoneNumber?: string;
  }): Promise<User> => {
    const response = await api.post<User>('/auth/register', data);
    return response.data;
  },

  getCurrentUser: async (): Promise<User> => {
    const response = await api.get<User>('/auth/me');
    return response.data;
  },

  refreshToken: async (token: string, refreshToken: string): Promise<AuthResponse> => {
    const response = await api.post<AuthResponse>('/auth/refresh', { token, refreshToken });
    return response.data;
  },
};

export const deviceApi = {
  getAll: async (status?: string, deviceType?: string, groupId?: string): Promise<Device[]> => {
    const params = new URLSearchParams();
    if (status) params.append('status', status);
    if (deviceType) params.append('deviceType', deviceType);
    if (groupId) params.append('groupId', groupId);
    
    const response = await api.get<Device[]>(`/devices?${params.toString()}`);
    return response.data;
  },

  getById: async (id: string): Promise<Device> => {
    const response = await api.get<Device>(`/devices/${id}`);
    return response.data;
  },

  create: async (data: {
    name: string;
    deviceType: string;
    serialNumber: string;
    description?: string;
    location?: string;
    latitude?: number;
    longitude?: number;
    manufacturer?: string;
    firmwareVersion?: string;
    hardwareVersion?: string;
    groupId?: string;
  }): Promise<Device> => {
    const response = await api.post<Device>('/devices', data);
    return response.data;
  },

  update: async (id: string, data: {
    name?: string;
    description?: string;
    location?: string;
    latitude?: number;
    longitude?: number;
    firmwareVersion?: string;
    groupId?: string;
    isActive?: boolean;
  }): Promise<Device> => {
    const response = await api.put<Device>(`/devices/${id}`, data);
    return response.data;
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/devices/${id}`);
  },

  getStatistics: async (): Promise<DeviceStatistics> => {
    const response = await api.get<DeviceStatistics>('/devices/statistics');
    return response.data;
  },

  updateStatus: async (id: string, status: string): Promise<void> => {
    await api.put(`/devices/${id}/status`, { status });
  },

  getByGroup: async (groupId: string): Promise<Device[]> => {
    const response = await api.get<Device[]>(`/devices/group/${groupId}`);
    return response.data;
  },

  getBySerialNumber: async (serialNumber: string): Promise<Device> => {
    const response = await api.get<Device>(`/devices/serial/${serialNumber}`);
    return response.data;
  },
};

export const dataApi = {
  addData: async (data: {
    deviceId: string;
    metric: string;
    value: number;
    unit?: string;
    timestamp?: string;
    metadata?: string;
  }): Promise<void> => {
    await api.post('/data', data);
  },

  addBatchData: async (data: {
    deviceId: string;
    metrics: { metric: string; value: number; unit?: string }[];
    timestamp?: string;
  }): Promise<void> => {
    await api.post('/data/batch', data);
  },

  getHistoricalData: async (params: {
    deviceId: string;
    startTime: string;
    endTime: string;
    metric?: string;
    limit?: number;
  }): Promise<DeviceData[]> => {
    const queryParams = new URLSearchParams({
      deviceId: params.deviceId,
      startTime: params.startTime,
      endTime: params.endTime,
    });
    if (params.metric) queryParams.append('metric', params.metric);
    if (params.limit) queryParams.append('limit', params.limit.toString());

    const response = await api.get<DeviceData[]>(`/data/historical?${queryParams.toString()}`);
    return response.data;
  },

  getAggregatedData: async (params: {
    deviceId: string;
    startTime: string;
    endTime: string;
    metric?: string;
    intervalSeconds?: number;
  }): Promise<AggregatedData[]> => {
    const queryParams = new URLSearchParams({
      deviceId: params.deviceId,
      startTime: params.startTime,
      endTime: params.endTime,
    });
    if (params.metric) queryParams.append('metric', params.metric);
    if (params.intervalSeconds) queryParams.append('intervalSeconds', params.intervalSeconds.toString());

    const response = await api.get<AggregatedData[]>(`/data/aggregated?${queryParams.toString()}`);
    return response.data;
  },

  getLatestData: async (deviceId: string): Promise<RealTimeData> => {
    const response = await api.get<RealTimeData>(`/data/latest/${deviceId}`);
    return response.data;
  },

  getLatestDataForDevices: async (deviceIds: string[]): Promise<RealTimeData[]> => {
    const response = await api.post<RealTimeData[]>('/data/latest-batch', deviceIds);
    return response.data;
  },
};

export const alarmApi = {
  getAll: async (status?: string, level?: string, deviceId?: string, limit?: number): Promise<Alarm[]> => {
    const params = new URLSearchParams();
    if (status) params.append('status', status);
    if (level) params.append('level', level);
    if (deviceId) params.append('deviceId', deviceId);
    if (limit) params.append('limit', limit.toString());

    const response = await api.get<Alarm[]>(`/alarms?${params.toString()}`);
    return response.data;
  },

  getById: async (id: string): Promise<Alarm> => {
    const response = await api.get<Alarm>(`/alarms/${id}`);
    return response.data;
  },

  acknowledge: async (id: string, notes?: string): Promise<Alarm> => {
    const response = await api.post<Alarm>(`/alarms/${id}/acknowledge`, { notes });
    return response.data;
  },

  resolve: async (id: string, resolutionNotes: string): Promise<Alarm> => {
    const response = await api.post<Alarm>(`/alarms/${id}/resolve`, { resolutionNotes });
    return response.data;
  },

  getStatistics: async (): Promise<AlarmStatistics> => {
    const response = await api.get<AlarmStatistics>('/alarms/statistics');
    return response.data;
  },

  getAllRules: async (isEnabled?: boolean, deviceId?: string): Promise<AlarmRule[]> => {
    const params = new URLSearchParams();
    if (isEnabled !== undefined) params.append('isEnabled', isEnabled.toString());
    if (deviceId) params.append('deviceId', deviceId);

    const response = await api.get<AlarmRule[]>(`/alarms/rules?${params.toString()}`);
    return response.data;
  },

  getRuleById: async (id: string): Promise<AlarmRule> => {
    const response = await api.get<AlarmRule>(`/alarms/rules/${id}`);
    return response.data;
  },

  createRule: async (data: {
    name: string;
    description?: string;
    deviceId?: string;
    groupId?: string;
    metric: string;
    operator: string;
    threshold: number;
    warningThreshold?: number;
    criticalThreshold?: number;
    durationSeconds?: number;
    consecutiveOccurrences?: number;
    alarmLevel?: string;
    isEnabled?: boolean;
    isNotificationEnabled?: boolean;
    notificationChannels?: string;
    cooldownMinutes?: number;
  }): Promise<AlarmRule> => {
    const response = await api.post<AlarmRule>('/alarms/rules', data);
    return response.data;
  },

  updateRule: async (id: string, data: {
    name?: string;
    description?: string;
    metric?: string;
    operator?: string;
    threshold?: number;
    warningThreshold?: number;
    criticalThreshold?: number;
    durationSeconds?: number;
    consecutiveOccurrences?: number;
    alarmLevel?: string;
    isEnabled?: boolean;
    isNotificationEnabled?: boolean;
    notificationChannels?: string;
    cooldownMinutes?: number;
  }): Promise<AlarmRule> => {
    const response = await api.put<AlarmRule>(`/alarms/rules/${id}`, data);
    return response.data;
  },

  deleteRule: async (id: string): Promise<void> => {
    await api.delete(`/alarms/rules/${id}`);
  },
};

export const controlApi = {
  sendCommand: async (data: {
    deviceId: string;
    command: string;
    parameters?: Record<string, unknown>;
    maxRetries?: number;
    timeoutSeconds?: number;
    priority?: string;
  }): Promise<ControlCommand> => {
    const response = await api.post<ControlCommand>('/control/send', data);
    return response.data;
  },

  sendBatchCommand: async (data: {
    deviceIds: string[];
    command: string;
    parameters?: Record<string, unknown>;
    maxRetries?: number;
    timeoutSeconds?: number;
    priority?: string;
  }): Promise<ControlCommand[]> => {
    const response = await api.post<ControlCommand[]>('/control/send-batch', data);
    return response.data;
  },

  getById: async (id: string): Promise<ControlCommand> => {
    const response = await api.get<ControlCommand>(`/control/${id}`);
    return response.data;
  },

  getAll: async (status?: string, deviceId?: string, limit?: number): Promise<ControlCommand[]> => {
    const params = new URLSearchParams();
    if (status) params.append('status', status);
    if (deviceId) params.append('deviceId', deviceId);
    if (limit) params.append('limit', limit.toString());

    const response = await api.get<ControlCommand[]>(`/control?${params.toString()}`);
    return response.data;
  },

  getByDevice: async (deviceId: string, limit?: number): Promise<ControlCommand[]> => {
    const params = new URLSearchParams();
    if (limit) params.append('limit', limit.toString());

    const response = await api.get<ControlCommand[]>(`/control/device/${deviceId}?${params.toString()}`);
    return response.data;
  },

  getCommandResult: async (id: string): Promise<CommandResult> => {
    const response = await api.get<CommandResult>(`/control/result/${id}`);
    return response.data;
  },

  cancelCommand: async (id: string): Promise<ControlCommand> => {
    const response = await api.post<ControlCommand>(`/control/cancel/${id}`);
    return response.data;
  },
};

export const reportApi = {
  getDeviceStatistics: async (): Promise<DeviceStatisticsReport> => {
    const response = await api.get<DeviceStatisticsReport>('/reports/device-statistics');
    return response.data;
  },

  getDeviceAvailability: async (params: {
    startTime: string;
    endTime: string;
    deviceIds?: string[];
  }): Promise<DeviceAvailabilityReport[]> => {
    const queryParams = new URLSearchParams({
      startTime: params.startTime,
      endTime: params.endTime,
    });

    const response = await api.get<DeviceAvailabilityReport[]>(`/reports/device-availability?${queryParams.toString()}`);
    return response.data;
  },

  getAlarmReport: async (params: {
    startTime: string;
    endTime: string;
    deviceIds?: string[];
  }): Promise<AlarmReport> => {
    const queryParams = new URLSearchParams({
      startTime: params.startTime,
      endTime: params.endTime,
    });

    const response = await api.get<AlarmReport>(`/reports/alarm?${queryParams.toString()}`);
    return response.data;
  },

  getCommandExecutionReport: async (params: {
    startTime: string;
    endTime: string;
    deviceIds?: string[];
  }): Promise<CommandExecutionReport> => {
    const queryParams = new URLSearchParams({
      startTime: params.startTime,
      endTime: params.endTime,
    });

    const response = await api.get<CommandExecutionReport>(`/reports/command-execution?${queryParams.toString()}`);
    return response.data;
  },

  exportCsv: async (reportType: string, params: {
    startTime: string;
    endTime: string;
  }): Promise<Blob> => {
    const queryParams = new URLSearchParams({
      reportType,
      startTime: params.startTime,
      endTime: params.endTime,
    });

    const response = await api.get(`/reports/export/csv?${queryParams.toString()}`, {
      responseType: 'blob',
    });
    return response.data;
  },
};

export default api;
