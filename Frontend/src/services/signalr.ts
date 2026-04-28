import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import type {
  RealTimeData,
  DeviceStatusUpdate,
  Alarm,
  CommandResult,
  DeviceStatistics,
  StateSyncRequestDto,
  StateSyncResponseDto,
  DeviceStateDto,
  DeviceOnlineEventDto,
  DeviceOfflineEventDto,
  ConnectionStatusDto,
  AlarmAggregationDto,
  AlarmSuppressionDto,
} from '@/types';
import { api } from './api';

interface StateCache {
  [deviceId: string]: {
    state: DeviceStateDto;
    lastSyncTime: Date;
  };
}

class SignalRService {
  private connection: HubConnection | null = null;
  private isConnecting = false;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 10;
  private reconnectDelay = 3000;
  private lastConnectedAt: Date | null = null;
  private lastDisconnectedAt: Date | null = null;
  private stateCache: StateCache = {};
  private lastKnownVersion = 0;
  private isSyncing = false;

  private onDeviceDataCallbacks: ((data: RealTimeData) => void)[] = [];
  private onDeviceStatusCallbacks: ((status: DeviceStatusUpdate) => void)[] = [];
  private onAlarmCallbacks: ((alarm: Alarm) => void)[] = [];
  private onAlarmUpdateCallbacks: ((alarm: Alarm) => void)[] = [];
  private onCommandResultCallbacks: ((result: CommandResult) => void)[] = [];
  private onStatisticsCallbacks: ((statistics: DeviceStatistics) => void)[] = [];
  private onConnectedCallbacks: (() => void)[] = [];
  private onDisconnectedCallbacks: ((error?: Error) => void)[] = [];
  private onDeviceOnlineCallbacks: ((event: DeviceOnlineEventDto) => void)[] = [];
  private onDeviceOfflineCallbacks: ((event: DeviceOfflineEventDto) => void)[] = [];
  private onStateSyncCallbacks: ((response: StateSyncResponseDto) => void)[] = [];
  private onAlarmAggregationCallbacks: ((aggregation: AlarmAggregationDto) => void)[] = [];
  private onConnectionStatusCallbacks: ((status: ConnectionStatusDto) => void)[] = [];

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
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
              return null;
            }
            const delays = [0, 2000, 5000, 10000, 20000, 30000];
            return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
          },
        })
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

      this.connection.on('ReceiveAlarmAggregation', (aggregation: AlarmAggregationDto) => {
        this.onAlarmAggregationCallbacks.forEach((cb) => cb(aggregation));
      });

      this.connection.on('ReceiveCommandResult', (result: CommandResult) => {
        this.onCommandResultCallbacks.forEach((cb) => cb(result));
      });

      this.connection.on('ReceiveStatisticsUpdate', (statistics: DeviceStatistics) => {
        this.onStatisticsCallbacks.forEach((cb) => cb(statistics));
      });

      this.connection.on('ReceiveDeviceOnline', (event: DeviceOnlineEventDto) => {
        this.onDeviceOnlineCallbacks.forEach((cb) => cb(event));
      });

      this.connection.on('ReceiveDeviceOffline', (event: DeviceOfflineEventDto) => {
        this.onDeviceOfflineCallbacks.forEach((cb) => cb(event));
      });

      this.connection.on('ReceiveStateSync', (response: StateSyncResponseDto) => {
        this.handleStateSyncResponse(response);
        this.onStateSyncCallbacks.forEach((cb) => cb(response));
      });

      this.connection.onreconnecting((error) => {
        console.warn('SignalR reconnecting:', error);
        this.notifyConnectionStatus();
      });

      this.connection.onreconnected(async (connectionId) => {
        console.log('SignalR reconnected, connectionId:', connectionId);
        this.reconnectAttempts = 0;
        this.lastConnectedAt = new Date();
        this.isConnecting = false;
        
        await this.performStateSync();
        
        this.onConnectedCallbacks.forEach((cb) => cb());
        this.notifyConnectionStatus();
      });

      this.connection.onclose((error) => {
        console.warn('SignalR connection closed:', error);
        this.lastDisconnectedAt = new Date();
        this.isConnecting = false;
        
        this.onDisconnectedCallbacks.forEach((cb) => cb(error));
        this.notifyConnectionStatus();
        
        this.attemptReconnect();
      });

      await this.connection.start();
      console.log('SignalR connected successfully');
      this.reconnectAttempts = 0;
      this.lastConnectedAt = new Date();
      this.isConnecting = false;

      await this.subscribeToAllDevices();
      await this.subscribeToAlarms();
      
      await this.performStateSync();

      this.onConnectedCallbacks.forEach((cb) => cb());
      this.notifyConnectionStatus();
    } catch (error) {
      console.error('SignalR connection failed:', error);
      this.isConnecting = false;
      this.lastDisconnectedAt = new Date();
      this.notifyConnectionStatus();
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

  private async performStateSync(): Promise<void> {
    if (this.isSyncing) {
      return;
    }

    this.isSyncing = true;

    try {
      const request: StateSyncRequestDto = {
        lastKnownVersion: this.lastKnownVersion,
        timestamp: new Date().toISOString(),
        requestFullSnapshot: this.reconnectAttempts > 0,
      };

      console.log('Requesting state sync:', request);

      const response = await this.invoke<StateSyncResponseDto>('GetStateSync', request);
      
      if (response) {
        this.handleStateSyncResponse(response);
      }
    } catch (error) {
      console.error('State sync failed:', error);
    } finally {
      this.isSyncing = false;
    }
  }

  private handleStateSyncResponse(response: StateSyncResponseDto): void {
    console.log('State sync received:', {
      isFullSnapshot: response.isFullSnapshot,
      deviceCount: response.states.length,
      syncId: response.syncId,
    });

    for (const state of response.states) {
      this.stateCache[state.deviceId] = {
        state,
        lastSyncTime: new Date(),
      };

      if (state.version > this.lastKnownVersion) {
        this.lastKnownVersion = state.version;
      }

      const statusUpdate: DeviceStatusUpdate = {
        deviceId: state.deviceId,
        status: state.currentStatus,
        timestamp: state.statusChangedAt,
        metrics: state.metrics,
      };
      
      this.onDeviceStatusCallbacks.forEach((cb) => cb(statusUpdate));
    }
  }

  private notifyConnectionStatus(): void {
    const status: ConnectionStatusDto = {
      isConnected: this.isConnected(),
      reconnectAttempts: this.reconnectAttempts,
      lastConnectedAt: this.lastConnectedAt?.toISOString(),
      lastDisconnectedAt: this.lastDisconnectedAt?.toISOString(),
    };

    this.onConnectionStatusCallbacks.forEach((cb) => cb(status));
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log('SignalR disconnected');
    }
  }

  async invoke<T>(methodName: string, ...args: unknown[]): Promise<T | null> {
    if (this.connection?.state !== HubConnectionState.Connected) {
      console.warn(`Cannot invoke ${methodName}: not connected`);
      return null;
    }

    try {
      return await this.connection.invoke<T>(methodName, ...args);
    } catch (error) {
      console.error(`Invocation failed for ${methodName}:`, error);
      return null;
    }
  }

  async subscribeToDevice(deviceId: string): Promise<void> {
    await this.invoke('SubscribeToDevice', deviceId);
  }

  async unsubscribeFromDevice(deviceId: string): Promise<void> {
    await this.invoke('UnsubscribeFromDevice', deviceId);
  }

  async subscribeToGroup(groupId: string): Promise<void> {
    await this.invoke('SubscribeToGroup', groupId);
  }

  async unsubscribeFromGroup(groupId: string): Promise<void> {
    await this.invoke('UnsubscribeFromGroup', groupId);
  }

  async subscribeToAlarms(): Promise<void> {
    await this.invoke('SubscribeToAlarms');
  }

  async unsubscribeFromAlarms(): Promise<void> {
    await this.invoke('UnsubscribeFromAlarms');
  }

  async subscribeToAllDevices(): Promise<void> {
    await this.invoke('SubscribeToAllDevices');
  }

  async unsubscribeFromAllDevices(): Promise<void> {
    await this.invoke('UnsubscribeFromAllDevices');
  }

  async requestStateSync(deviceIds?: string[]): Promise<void> {
    if (!this.isConnected()) {
      return;
    }

    const request: StateSyncRequestDto = {
      lastKnownVersion: this.lastKnownVersion,
      deviceIds,
      timestamp: new Date().toISOString(),
      requestFullSnapshot: !deviceIds,
    };

    await this.invoke('RequestStateSync', request);
  }

  async sendHeartbeat(deviceId: string, metrics?: Record<string, number>): Promise<void> {
    const heartbeat = {
      deviceId,
      timestamp: new Date().toISOString(),
      status: 'Online' as const,
      metrics,
    };

    await this.invoke('SendHeartbeat', heartbeat);
  }

  getCachedState(deviceId: string): DeviceStateDto | undefined {
    return this.stateCache[deviceId]?.state;
  }

  getAllCachedStates(): DeviceStateDto[] {
    return Object.values(this.stateCache).map((cache) => cache.state);
  }

  clearStateCache(): void {
    this.stateCache = {};
    this.lastKnownVersion = 0;
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

  onAlarmAggregation(callback: (aggregation: AlarmAggregationDto) => void): () => void {
    this.onAlarmAggregationCallbacks.push(callback);
    return () => {
      this.onAlarmAggregationCallbacks = this.onAlarmAggregationCallbacks.filter((cb) => cb !== callback);
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

  onDeviceOnline(callback: (event: DeviceOnlineEventDto) => void): () => void {
    this.onDeviceOnlineCallbacks.push(callback);
    return () => {
      this.onDeviceOnlineCallbacks = this.onDeviceOnlineCallbacks.filter((cb) => cb !== callback);
    };
  }

  onDeviceOffline(callback: (event: DeviceOfflineEventDto) => void): () => void {
    this.onDeviceOfflineCallbacks.push(callback);
    return () => {
      this.onDeviceOfflineCallbacks = this.onDeviceOfflineCallbacks.filter((cb) => cb !== callback);
    };
  }

  onStateSync(callback: (response: StateSyncResponseDto) => void): () => void {
    this.onStateSyncCallbacks.push(callback);
    return () => {
      this.onStateSyncCallbacks = this.onStateSyncCallbacks.filter((cb) => cb !== callback);
    };
  }

  onConnectionStatusChange(callback: (status: ConnectionStatusDto) => void): () => void {
    this.onConnectionStatusCallbacks.push(callback);
    return () => {
      this.onConnectionStatusCallbacks = this.onConnectionStatusCallbacks.filter((cb) => cb !== callback);
    };
  }

  getConnectionState(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  getReconnectAttempts(): number {
    return this.reconnectAttempts;
  }

  getLastConnectedTime(): Date | null {
    return this.lastConnectedAt;
  }

  getLastDisconnectedTime(): Date | null {
    return this.lastDisconnectedAt;
  }
}

export const signalRService = new SignalRService();
export default signalRService;
