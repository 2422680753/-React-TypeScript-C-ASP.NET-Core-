import { create } from 'zustand';
import type {
  User,
  Device,
  DeviceStatistics,
  RealTimeData,
  Alarm,
  AlarmStatistics,
  AlarmRule,
  ControlCommand,
  DeviceStatus,
} from '@/types';
import { signalRService } from '@/services/signalr';

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  
  setUser: (user: User | null) => void;
  setToken: (token: string | null) => void;
  login: (user: User, token: string) => void;
  logout: () => void;
  setLoading: (loading: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  token: localStorage.getItem('token'),
  isAuthenticated: !!localStorage.getItem('token'),
  isLoading: false,

  setUser: (user) => set({ user }),
  setToken: (token) => {
    if (token) {
      localStorage.setItem('token', token);
    } else {
      localStorage.removeItem('token');
    }
    set({ token, isAuthenticated: !!token });
  },

  login: (user, token) => {
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify(user));
    set({
      user,
      token,
      isAuthenticated: true,
    });
  },

  logout: () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    signalRService.stop();
    set({
      user: null,
      token: null,
      isAuthenticated: false,
    });
  },

  setLoading: (isLoading) => set({ isLoading }),
}));

interface DeviceState {
  devices: Device[];
  selectedDevice: Device | null;
  deviceStatistics: DeviceStatistics | null;
  realTimeData: Record<string, RealTimeData>;
  isLoading: boolean;
  
  setDevices: (devices: Device[]) => void;
  setSelectedDevice: (device: Device | null) => void;
  setDeviceStatistics: (stats: DeviceStatistics | null) => void;
  updateDeviceStatus: (deviceId: string, status: DeviceStatus) => void;
  updateRealTimeData: (data: RealTimeData) => void;
  addDevice: (device: Device) => void;
  updateDevice: (device: Device) => void;
  removeDevice: (deviceId: string) => void;
  setLoading: (loading: boolean) => void;
}

export const useDeviceStore = create<DeviceState>((set, get) => ({
  devices: [],
  selectedDevice: null,
  deviceStatistics: null,
  realTimeData: {},
  isLoading: false,

  setDevices: (devices) => set({ devices }),
  setSelectedDevice: (selectedDevice) => set({ selectedDevice }),
  setDeviceStatistics: (deviceStatistics) => set({ deviceStatistics }),

  updateDeviceStatus: (deviceId, status) => {
    const { devices } = get();
    const updatedDevices = devices.map((d) =>
      d.id === deviceId ? { ...d, status } : d
    );
    set({ devices: updatedDevices });
  },

  updateRealTimeData: (data) => {
    set((state) => ({
      realTimeData: {
        ...state.realTimeData,
        [data.deviceId]: data,
      },
    }));
  },

  addDevice: (device) => {
    set((state) => ({
      devices: [device, ...state.devices],
    }));
  },

  updateDevice: (updatedDevice) => {
    set((state) => ({
      devices: state.devices.map((d) =>
        d.id === updatedDevice.id ? updatedDevice : d
      ),
      selectedDevice:
        state.selectedDevice?.id === updatedDevice.id
          ? updatedDevice
          : state.selectedDevice,
    }));
  },

  removeDevice: (deviceId) => {
    set((state) => ({
      devices: state.devices.filter((d) => d.id !== deviceId),
      selectedDevice:
        state.selectedDevice?.id === deviceId ? null : state.selectedDevice,
    }));
  },

  setLoading: (isLoading) => set({ isLoading }),
}));

interface AlarmState {
  alarms: Alarm[];
  alarmRules: AlarmRule[];
  alarmStatistics: AlarmStatistics | null;
  selectedAlarm: Alarm | null;
  unreadCount: number;
  isLoading: boolean;
  
  setAlarms: (alarms: Alarm[]) => void;
  setAlarmRules: (rules: AlarmRule[]) => void;
  setAlarmStatistics: (stats: AlarmStatistics | null) => void;
  setSelectedAlarm: (alarm: Alarm | null) => void;
  addAlarm: (alarm: Alarm) => void;
  updateAlarm: (alarm: Alarm) => void;
  addAlarmRule: (rule: AlarmRule) => void;
  updateAlarmRule: (rule: AlarmRule) => void;
  removeAlarmRule: (ruleId: string) => void;
  setLoading: (loading: boolean) => void;
  incrementUnreadCount: () => void;
  resetUnreadCount: () => void;
}

export const useAlarmStore = create<AlarmState>((set, get) => ({
  alarms: [],
  alarmRules: [],
  alarmStatistics: null,
  selectedAlarm: null,
  unreadCount: 0,
  isLoading: false,

  setAlarms: (alarms) => set({ alarms }),
  setAlarmRules: (alarmRules) => set({ alarmRules }),
  setAlarmStatistics: (alarmStatistics) => set({ alarmStatistics }),
  setSelectedAlarm: (selectedAlarm) => set({ selectedAlarm }),

  addAlarm: (alarm) => {
    set((state) => ({
      alarms: [alarm, ...state.alarms],
      unreadCount: state.unreadCount + 1,
    }));
  },

  updateAlarm: (updatedAlarm) => {
    set((state) => ({
      alarms: state.alarms.map((a) =>
        a.id === updatedAlarm.id ? updatedAlarm : a
      ),
      selectedAlarm:
        state.selectedAlarm?.id === updatedAlarm.id
          ? updatedAlarm
          : state.selectedAlarm,
    }));
  },

  addAlarmRule: (rule) => {
    set((state) => ({
      alarmRules: [rule, ...state.alarmRules],
    }));
  },

  updateAlarmRule: (updatedRule) => {
    set((state) => ({
      alarmRules: state.alarmRules.map((r) =>
        r.id === updatedRule.id ? updatedRule : r
      ),
    }));
  },

  removeAlarmRule: (ruleId) => {
    set((state) => ({
      alarmRules: state.alarmRules.filter((r) => r.id !== ruleId),
    }));
  },

  setLoading: (isLoading) => set({ isLoading }),

  incrementUnreadCount: () => {
    set((state) => ({ unreadCount: state.unreadCount + 1 }));
  },

  resetUnreadCount: () => set({ unreadCount: 0 }),
}));

interface ControlState {
  commands: ControlCommand[];
  selectedCommand: ControlCommand | null;
  isLoading: boolean;
  
  setCommands: (commands: ControlCommand[]) => void;
  setSelectedCommand: (command: ControlCommand | null) => void;
  addCommand: (command: ControlCommand) => void;
  updateCommand: (command: ControlCommand) => void;
  setLoading: (loading: boolean) => void;
}

export const useControlStore = create<ControlState>((set) => ({
  commands: [],
  selectedCommand: null,
  isLoading: false,

  setCommands: (commands) => set({ commands }),
  setSelectedCommand: (selectedCommand) => set({ selectedCommand }),

  addCommand: (command) => {
    set((state) => ({
      commands: [command, ...state.commands],
    }));
  },

  updateCommand: (updatedCommand) => {
    set((state) => ({
      commands: state.commands.map((c) =>
        c.id === updatedCommand.id ? updatedCommand : c
      ),
      selectedCommand:
        state.selectedCommand?.id === updatedCommand.id
          ? updatedCommand
          : state.selectedCommand,
    }));
  },

  setLoading: (isLoading) => set({ isLoading }),
}));

interface NotificationState {
  notifications: Array<{
    id: string;
    type: 'success' | 'error' | 'warning' | 'info';
    title: string;
    message?: string;
    duration?: number;
  }>;
  
  showNotification: (notification: Omit<{
    id: string;
    type: 'success' | 'error' | 'warning' | 'info';
    title: string;
    message?: string;
    duration?: number;
  }, 'id'>) => void;
  hideNotification: (id: string) => void;
}

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],

  showNotification: (notification) => {
    const id = Date.now().toString();
    set((state) => ({
      notifications: [...state.notifications, { ...notification, id }],
    }));

    if (notification.duration !== 0) {
      setTimeout(() => {
        set((state) => ({
          notifications: state.notifications.filter((n) => n.id !== id),
        }));
      }, notification.duration || 5000);
    }
  },

  hideNotification: (id) => {
    set((state) => ({
      notifications: state.notifications.filter((n) => n.id !== id),
    }));
  },
}));
