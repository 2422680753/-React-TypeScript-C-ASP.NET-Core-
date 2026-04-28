import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import type {
  RealTimeData,
  DeviceStatusUpdate,
  Alarm,
  CommandResult,
  DeviceStatistics,
} from '@/types';

class SignalRService {
  private connection: HubConnection | null = null;
  private isConnecting = false;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 3000;

  private onDeviceDataCallbacks: ((data: RealTimeData) => void)[] = [];
  private onDeviceStatusCallbacks: ((status: DeviceStatusUpdate) => void)[] = [];
  private onAlarmCallbacks: ((alarm: Alarm) => void)[] = [];
  private onAlarmUpdateCallbacks: ((alarm: Alarm) => void)[] = [];
  private onCommandResultCallbacks: ((result: CommandResult) => void)[] = [];
  private onStatisticsCallbacks: ((statistics: DeviceStatistics) => void)[] = [];
  private onConnectedCallbacks: (() => void)[] = [];
  private onDisconnectedCallbacks: ((error?: Error) => void)[] = [];

  async start(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (this.isConnecting) {
      return;
    }

    this.isConnecting = true;

    try {
      const token = localStorage.getItem('token');
      
      this.connection = new HubConnectionBuilder()
        .withUrl('/monitoringHub', {
          accessTokenFactory: () => token || '',
          withCredentials: true,
        })
        .configureLogging(LogLevel.Information)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build();

      this.connection.on('ReceiveDeviceData', (data: RealTimeData) => {
        this.onDeviceDataCallbacks.forEach((cb) => cb(data));
      });

      this.connection.on('ReceiveDeviceStatusChange', (status: DeviceStatusUpdate) => {
        this.onDeviceStatusCallbacks.forEach((cb) => cb(status));
      });

      this.connection.on('ReceiveAlarm', (alarm: Alarm) => {
        this.onAlarmCallbacks.forEach((cb) => cb(alarm));
      });

      this.connection.on('ReceiveAlarmUpdate', (alarm: Alarm) => {
        this.onAlarmUpdateCallbacks.forEach((cb) => cb(alarm));
      });

      this.connection.on('ReceiveCommandResult', (result: CommandResult) => {
        this.onCommandResultCallbacks.forEach((cb) => cb(result));
      });

      this.connection.on('ReceiveStatisticsUpdate', (statistics: DeviceStatistics) => {
        this.onStatisticsCallbacks.forEach((cb) => cb(statistics));
      });

      this.connection.onreconnecting((error) => {
        console.warn('SignalR reconnecting:', error);
      });

      this.connection.onreconnected((connectionId) => {
        console.log('SignalR reconnected, connectionId:', connectionId);
        this.reconnectAttempts = 0;
        this.onConnectedCallbacks.forEach((cb) => cb());
      });

      this.connection.onclose((error) => {
        console.warn('SignalR connection closed:', error);
        this.onDisconnectedCallbacks.forEach((cb) => cb(error));
        this.attemptReconnect();
      });

      await this.connection.start();
      console.log('SignalR connected successfully');
      this.reconnectAttempts = 0;
      this.isConnecting = false;
      this.onConnectedCallbacks.forEach((cb) => cb());
    } catch (error) {
      console.error('SignalR connection failed:', error);
      this.isConnecting = false;
      this.attemptReconnect();
      throw error;
    }
  }

  private async attemptReconnect(): Promise<void> {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('Max reconnect attempts reached');
      return;
    }

    this.reconnectAttempts++;
    console.log(`Attempting to reconnect (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`);

    await new Promise((resolve) => setTimeout(resolve, this.reconnectDelay));

    try {
      await this.start();
    } catch {
      // Reconnect failed, will try again
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log('SignalR disconnected');
    }
  }

  async subscribeToDevice(deviceId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubscribeToDevice', deviceId);
    }
  }

  async unsubscribeFromDevice(deviceId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnsubscribeFromDevice', deviceId);
    }
  }

  async subscribeToGroup(groupId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubscribeToGroup', groupId);
    }
  }

  async unsubscribeFromGroup(groupId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnsubscribeFromGroup', groupId);
    }
  }

  async subscribeToAlarms(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubscribeToAlarms');
    }
  }

  async unsubscribeFromAlarms(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnsubscribeFromAlarms');
    }
  }

  async subscribeToAllDevices(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubscribeToAllDevices');
    }
  }

  async unsubscribeFromAllDevices(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnsubscribeFromAllDevices');
    }
  }

  onDeviceData(callback: (data: RealTimeData) => void): () => void {
    this.onDeviceDataCallbacks.push(callback);
    return () => {
      this.onDeviceDataCallbacks = this.onDeviceDataCallbacks.filter((cb) => cb !== callback);
    };
  }

  onDeviceStatusChange(callback: (status: DeviceStatusUpdate) => void): () => void {
    this.onDeviceStatusCallbacks.push(callback);
    return () => {
      this.onDeviceStatusCallbacks = this.onDeviceStatusCallbacks.filter((cb) => cb !== callback);
    };
  }

  onAlarm(callback: (alarm: Alarm) => void): () => void {
    this.onAlarmCallbacks.push(callback);
    return () => {
      this.onAlarmCallbacks = this.onAlarmCallbacks.filter((cb) => cb !== callback);
    };
  }

  onAlarmUpdate(callback: (alarm: Alarm) => void): () => void {
    this.onAlarmUpdateCallbacks.push(callback);
    return () => {
      this.onAlarmUpdateCallbacks = this.onAlarmUpdateCallbacks.filter((cb) => cb !== callback);
    };
  }

  onCommandResult(callback: (result: CommandResult) => void): () => void {
    this.onCommandResultCallbacks.push(callback);
    return () => {
      this.onCommandResultCallbacks = this.onCommandResultCallbacks.filter((cb) => cb !== callback);
    };
  }

  onStatisticsUpdate(callback: (statistics: DeviceStatistics) => void): () => void {
    this.onStatisticsCallbacks.push(callback);
    return () => {
      this.onStatisticsCallbacks = this.onStatisticsCallbacks.filter((cb) => cb !== callback);
    };
  }

  onConnected(callback: () => void): () => void {
    this.onConnectedCallbacks.push(callback);
    return () => {
      this.onConnectedCallbacks = this.onConnectedCallbacks.filter((cb) => cb !== callback);
    };
  }

  onDisconnected(callback: (error?: Error) => void): () => void {
    this.onDisconnectedCallbacks.push(callback);
    return () => {
      this.onDisconnectedCallbacks = this.onDisconnectedCallbacks.filter((cb) => cb !== callback);
    };
  }

  getConnectionState(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }
}

export const signalRService = new SignalRService();
export default signalRService;
