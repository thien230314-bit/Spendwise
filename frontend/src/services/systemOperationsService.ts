import { apiClient } from '../lib/apiClient';

// ---------------------------------------------------------------------------
// DTO & DATA CONTRACTS (matching backend OperationsController & OperationsFeature)
// ---------------------------------------------------------------------------

export interface MaintenanceStateDto {
  isEnabled: boolean;
  changedAt: string | null;
}

export interface ChangeMaintenanceRequest {
  isEnabled: boolean;
  securityPin: string;
  reason: string;
}

export interface EmergencyAnnouncementDto {
  id: string;
  title: string;
  description: string;
  status: string; // 'Active' | 'Inactive'
  createdAt: string;
  deactivatedAt?: string | null;
}

export interface PublishAnnouncementRequest {
  title: string;
  description: string;
  securityPin: string;
  reason: string;
}

export interface DeactivateAnnouncementRequest {
  securityPin: string;
  reason: string;
}

// UI Compatible Shapes preserved for existing components
export interface SystemMaintenanceState {
  enabled: boolean;
  changedAt: string;
  isInitialized: boolean;
}

export interface EmergencyAnnouncement {
  id: string;
  title: string;
  description: string;
  createdAt: string;
  status: 'active' | 'inactive';
}

const DISMISSED_BANNER_KEY_PREFIX = 'spendwise_dismissed_announcements_';

// In-memory backend-authoritative state cache
let currentMaintenanceState: SystemMaintenanceState = {
  enabled: false,
  changedAt: '',
  isInitialized: false,
};
let currentActiveAnnouncement: EmergencyAnnouncement | null = null;
let lastConfirmedMutationTimestamp = 0;
let lastSuccessfulFetchTimestamp = 0;
let activeRefreshPromise: Promise<{
  maintenance: SystemMaintenanceState;
  activeAnnouncement: EmergencyAnnouncement | null;
}> | null = null;

let pollingIntervalId: any = null;
let activeSubscriberCount = 0;
let lastError: Error | null = null;

// Listeners
const maintenanceListeners = new Set<(state: SystemMaintenanceState) => void>();
const announcementListeners = new Set<() => void>();

function notifyMaintenanceListeners() {
  maintenanceListeners.forEach((listener) => {
    try {
      listener(currentMaintenanceState);
    } catch (e) {
      console.error('Error in maintenance listener', e);
    }
  });
}

function notifyAnnouncementListeners() {
  announcementListeners.forEach((listener) => {
    try {
      listener();
    } catch (e) {
      console.error('Error in announcement listener', e);
    }
  });
}

function startPollingIfNeeded() {
  if (typeof window === 'undefined') return;
  if (!pollingIntervalId) {
    // Poll every 30 seconds while UI subscribers exist
    pollingIntervalId = setInterval(() => {
      systemOperationsService.refreshState().catch(() => {});
    }, 30000);
  }
}

function stopPollingIfIdle() {
  if (activeSubscriberCount <= 0 && pollingIntervalId) {
    clearInterval(pollingIntervalId);
    pollingIntervalId = null;
  }
}

// Handle window focus refresh
if (typeof window !== 'undefined') {
  window.addEventListener('focus', () => {
    if (activeSubscriberCount > 0) {
      systemOperationsService.refreshState().catch(() => {});
    }
  });
}

export const systemOperationsService = {
  // ---------------------------------------------------------------------------
  // READ STATE (Synchronous access from cached confirmed backend state)
  // ---------------------------------------------------------------------------

  getMaintenanceState(): SystemMaintenanceState {
    return currentMaintenanceState;
  },

  isMaintenanceEnabled(): boolean {
    return currentMaintenanceState.enabled;
  },

  isMaintenanceInitialized(): boolean {
    return currentMaintenanceState.isInitialized;
  },

  getActiveAnnouncement(): EmergencyAnnouncement | null {
    return currentActiveAnnouncement;
  },

  getActiveAnnouncements(): EmergencyAnnouncement[] {
    return currentActiveAnnouncement ? [currentActiveAnnouncement] : [];
  },

  getLastError(): Error | null {
    return lastError;
  },

  // ---------------------------------------------------------------------------
  // REFRESH FROM BACKEND
  // ---------------------------------------------------------------------------

  async refreshState(): Promise<{
    maintenance: SystemMaintenanceState;
    activeAnnouncement: EmergencyAnnouncement | null;
  }> {
    // If a refresh is already in-flight, return the existing active promise so callers await the true result
    if (activeRefreshPromise) {
      return activeRefreshPromise;
    }

    const requestTime = Date.now();

    activeRefreshPromise = (async () => {
      try {
        const [maintRes, annRes] = await Promise.all([
          apiClient.get<MaintenanceStateDto>('/system/maintenance'),
          apiClient.get<EmergencyAnnouncementDto | null>('/system/announcements/active'),
        ]);

        // Prevent stale responses or responses initiated before a confirmed mutation from overwriting state
        if (requestTime >= lastSuccessfulFetchTimestamp && requestTime >= lastConfirmedMutationTimestamp) {
          lastSuccessfulFetchTimestamp = requestTime;

          let maintenanceChanged = false;
          const newMaint: SystemMaintenanceState = {
            enabled: Boolean(maintRes.isEnabled),
            changedAt: maintRes.changedAt || '',
            isInitialized: true,
          };
          if (
            currentMaintenanceState.enabled !== newMaint.enabled ||
            currentMaintenanceState.changedAt !== newMaint.changedAt ||
            !currentMaintenanceState.isInitialized
          ) {
            currentMaintenanceState = newMaint;
            maintenanceChanged = true;
          }

          let announcementChanged = false;
          let newAnn: EmergencyAnnouncement | null = null;
          if (annRes && (annRes.status?.toLowerCase() === 'active' || !annRes.status)) {
            newAnn = {
              id: annRes.id,
              title: annRes.title,
              description: annRes.description,
              createdAt: annRes.createdAt,
              status: 'active',
            };
          }

          const prev = currentActiveAnnouncement;
          if (
            (!prev && newAnn) ||
            (prev && !newAnn) ||
            (prev && newAnn && (
              prev.id !== newAnn.id ||
              prev.title !== newAnn.title ||
              prev.description !== newAnn.description ||
              prev.status !== newAnn.status
            ))
          ) {
            currentActiveAnnouncement = newAnn;
            announcementChanged = true;
          }

          lastError = null;
          if (maintenanceChanged) notifyMaintenanceListeners();
          if (announcementChanged) notifyAnnouncementListeners();
        }

        return {
          maintenance: currentMaintenanceState,
          activeAnnouncement: currentActiveAnnouncement,
        };
      } catch (err: any) {
        lastError = err instanceof Error ? err : new Error(String(err));
        // Throw error so callers can distinguish failure from verified state
        throw lastError;
      } finally {
        activeRefreshPromise = null;
      }
    })();

    return activeRefreshPromise;
  },

  // ---------------------------------------------------------------------------
  // ADMIN MUTATIONS (Call backend directly)
  // ---------------------------------------------------------------------------

  async changeMaintenance(request: ChangeMaintenanceRequest): Promise<SystemMaintenanceState> {
    const res = await apiClient.put<MaintenanceStateDto>('/admin/operations/maintenance', request);
    lastConfirmedMutationTimestamp = Date.now();
    const updated: SystemMaintenanceState = {
      enabled: Boolean(res.isEnabled),
      changedAt: res.changedAt || new Date().toISOString(),
      isInitialized: true,
    };
    currentMaintenanceState = updated;
    notifyMaintenanceListeners();
    return updated;
  },

  async publishAnnouncement(request: PublishAnnouncementRequest): Promise<EmergencyAnnouncement> {
    const res = await apiClient.post<EmergencyAnnouncementDto>('/admin/operations/announcements', request);
    lastConfirmedMutationTimestamp = Date.now();
    const published: EmergencyAnnouncement = {
      id: res.id,
      title: res.title,
      description: res.description,
      createdAt: res.createdAt,
      status: 'active',
    };
    currentActiveAnnouncement = published;
    notifyAnnouncementListeners();
    return published;
  },

  async deactivateAnnouncement(id: string, request: DeactivateAnnouncementRequest): Promise<void> {
    await apiClient.deleteWithBody<void>(`/admin/operations/announcements/${id}`, request);
    lastConfirmedMutationTimestamp = Date.now();
    if (currentActiveAnnouncement?.id === id) {
      currentActiveAnnouncement = null;
    }
    notifyAnnouncementListeners();
    // Re-verify from backend
    await this.refreshState().catch(() => {});
  },

  // ---------------------------------------------------------------------------
  // UI DISMISSAL STATE (Allowed in localStorage per requirement D.7)
  // ---------------------------------------------------------------------------

  isBannerDismissedForUser(announcementId: string, userId: string): boolean {
    if (!userId || !announcementId) return false;
    try {
      const raw = localStorage.getItem(`${DISMISSED_BANNER_KEY_PREFIX}${userId}`);
      if (!raw) return false;
      const dismissedList: string[] = JSON.parse(raw);
      return Array.isArray(dismissedList) && dismissedList.includes(announcementId);
    } catch {
      return false;
    }
  },

  dismissBannerForUser(announcementId: string, userId: string): void {
    if (!userId || !announcementId) return;
    try {
      const key = `${DISMISSED_BANNER_KEY_PREFIX}${userId}`;
      const raw = localStorage.getItem(key);
      let dismissedList: string[] = [];
      if (raw) dismissedList = JSON.parse(raw);
      if (!dismissedList.includes(announcementId)) {
        dismissedList.push(announcementId);
        localStorage.setItem(key, JSON.stringify(dismissedList));
        notifyAnnouncementListeners();
      }
    } catch (e) {
      console.warn('Failed to persist dismissed banner', e);
    }
  },

  // ---------------------------------------------------------------------------
  // SUBSCRIPTIONS
  // ---------------------------------------------------------------------------

  subscribeMaintenance(callback: (state: SystemMaintenanceState) => void): () => void {
    maintenanceListeners.add(callback);
    activeSubscriberCount++;
    startPollingIfNeeded();

    // Trigger initial callback with cached state
    callback(currentMaintenanceState);
    // Trigger eager fetch in background
    this.refreshState().catch(() => {});

    return () => {
      maintenanceListeners.delete(callback);
      activeSubscriberCount--;
      stopPollingIfIdle();
    };
  },

  subscribeAnnouncements(callback: () => void): () => void {
    announcementListeners.add(callback);
    activeSubscriberCount++;
    startPollingIfNeeded();

    // Trigger eager fetch in background
    this.refreshState().catch(() => {});

    return () => {
      announcementListeners.delete(callback);
      activeSubscriberCount--;
      stopPollingIfIdle();
    };
  },
};
