import { create } from 'zustand';
import type { Alarm, AlarmStatistics, AlarmRule, AlarmAggregationDto } from '@/types';

interface AlarmState {
  alarms: Alarm[];
  alarmRules: AlarmRule[];
  alarmStatistics: AlarmStatistics | null;
  selectedAlarm: Alarm | null;
  unreadCount: number;
  isLoading: boolean;
  pendingAlarms: Alarm[];
  alarmIdSet: Set<string>;
  aggregations: AlarmAggregationDto[];
  
  setAlarms: (alarms: Alarm[]) => void;
  setAlarmRules: (rules: AlarmRule[]) => void;
  setAlarmStatistics: (stats: AlarmStatistics | null) => void;
  setSelectedAlarm: (alarm: Alarm | null) => void;
  addAlarm: (alarm: Alarm) => void;
  addAlarmsBatch: (alarms: Alarm[]) => void;
  updateAlarm: (alarm: Alarm) => void;
  addAlarmRule: (rule: AlarmRule) => void;
  updateAlarmRule: (rule: AlarmRule) => void;
  removeAlarmRule: (ruleId: string) => void;
  setLoading: (loading: boolean) => void;
  incrementUnreadCount: () => void;
  resetUnreadCount: () => void;
  processPendingAlarms: () => void;
  clearAlarms: () => void;
  addAggregation: (aggregation: AlarmAggregationDto) => void;
  updateAggregation: (aggregation: AlarmAggregationDto) => void;
}

const BATCH_FLUSH_INTERVAL = 500;
const MAX_BATCH_SIZE = 50;

export const useAlarmStore = create<AlarmState>((set, get) => ({
  alarms: [],
  alarmRules: [],
  alarmStatistics: null,
  selectedAlarm: null,
  unreadCount: 0,
  isLoading: false,
  pendingAlarms: [],
  alarmIdSet: new Set(),
  aggregations: [],

  setAlarms: (alarms) => {
    const alarmIdSet = new Set(alarms.map(a => a.id));
    set({ alarms, alarmIdSet });
  },
  setAlarmRules: (alarmRules) => set({ alarmRules }),
  setAlarmStatistics: (alarmStatistics) => set({ alarmStatistics }),
  setSelectedAlarm: (selectedAlarm) => set({ selectedAlarm }),

  addAlarm: (alarm) => {
    const { alarmIdSet, pendingAlarms } = get();
    
    if (alarmIdSet.has(alarm.id)) {
      return;
    }

    const newPending = [...pendingAlarms, alarm];
    
    if (newPending.length >= MAX_BATCH_SIZE) {
      const { alarms } = get();
      const uniqueNewAlarms = newPending.filter(a => !alarmIdSet.has(a.id));
      const uniqueAlarmIds = new Set(uniqueNewAlarms.map(a => a.id));
      
      set({
        alarms: [...uniqueNewAlarms, ...alarms],
        pendingAlarms: [],
        alarmIdSet: new Set([...alarmIdSet, ...uniqueAlarmIds]),
        unreadCount: get().unreadCount + uniqueNewAlarms.length,
      });
    } else {
      set({ pendingAlarms: newPending });
      
      if (newPending.length === 1) {
        setTimeout(() => {
          get().processPendingAlarms();
        }, BATCH_FLUSH_INTERVAL);
      }
    }
  },

  addAlarmsBatch: (alarms) => {
    const { alarmIdSet, alarms: existingAlarms, unreadCount } = get();
    
    const uniqueAlarms = alarms.filter(a => !alarmIdSet.has(a.id));
    const uniqueIds = new Set(uniqueAlarms.map(a => a.id));
    
    if (uniqueAlarms.length > 0) {
      set({
        alarms: [...uniqueAlarms, ...existingAlarms],
        alarmIdSet: new Set([...alarmIdSet, ...uniqueIds]),
        unreadCount: unreadCount + uniqueAlarms.length,
      });
    }
  },

  processPendingAlarms: () => {
    const { pendingAlarms, alarmIdSet, alarms, unreadCount } = get();
    
    if (pendingAlarms.length === 0) return;
    
    const uniqueAlarms = pendingAlarms.filter(a => !alarmIdSet.has(a.id));
    const uniqueIds = new Set(uniqueAlarms.map(a => a.id));
    
    if (uniqueAlarms.length > 0) {
      set({
        alarms: [...uniqueAlarms, ...alarms],
        pendingAlarms: [],
        alarmIdSet: new Set([...alarmIdSet, ...uniqueIds]),
        unreadCount: unreadCount + uniqueAlarms.length,
      });
    } else {
      set({ pendingAlarms: [] });
    }
  },

  updateAlarm: (updatedAlarm) => {
    const { alarms, alarmIdSet } = get();
    
    const updatedAlarms = alarms.map((a) =>
      a.id === updatedAlarm.id ? updatedAlarm : a
    );
    
    const newSelectedAlarm = get().selectedAlarm?.id === updatedAlarm.id
      ? updatedAlarm
      : get().selectedAlarm;

    if (!alarmIdSet.has(updatedAlarm.id)) {
      set({
        alarms: [updatedAlarm, ...updatedAlarms],
        selectedAlarm: newSelectedAlarm,
        alarmIdSet: new Set([...alarmIdSet, updatedAlarm.id]),
      });
    } else {
      set({
        alarms: updatedAlarms,
        selectedAlarm: newSelectedAlarm,
      });
    }
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

  clearAlarms: () => {
    set({
      alarms: [],
      alarmIdSet: new Set(),
      pendingAlarms: [],
    });
  },

  addAggregation: (aggregation) => {
    set((state) => ({
      aggregations: [aggregation, ...state.aggregations],
    }));
  },

  updateAggregation: (updatedAggregation) => {
    set((state) => ({
      aggregations: state.aggregations.map((a) =>
        a.id === updatedAggregation.id ? updatedAggregation : a
      ),
    }));
  },
}));

export default useAlarmStore;
