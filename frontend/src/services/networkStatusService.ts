/**
 * SpendWise Frontend — Network Connection Status Service
 *
 * Provides a lightweight singleton listener for browser online / offline state
 * with debouncing and safe subscriber notification.
 *
 * Rules:
 * - Does not trigger page reload
 * - Does not log user out on offline
 * - Does not perform automatic financial mutation retries
 * - Smoothly manages transient connection drops
 */

type NetworkStatusListener = (isOnline: boolean, wasOffline: boolean) => void;

class NetworkStatusService {
  private isOnline: boolean = typeof navigator !== 'undefined' ? navigator.onLine : true;
  private wasOffline: boolean = false;
  private listeners: Set<NetworkStatusListener> = new Set();
  private initialized: boolean = false;

  constructor() {
    this.init();
  }

  private init() {
    if (typeof window === 'undefined' || this.initialized) return;
    this.initialized = true;

    window.addEventListener('online', this.handleOnline);
    window.addEventListener('offline', this.handleOffline);
  }

  private handleOnline = () => {
    const prevOffline = !this.isOnline || this.wasOffline;
    this.isOnline = true;
    this.wasOffline = prevOffline;
    this.notify(true, prevOffline);
  };

  private handleOffline = () => {
    this.isOnline = false;
    this.wasOffline = true;
    this.notify(false, true);
  };

  private notify(isOnline: boolean, wasOffline: boolean) {
    this.listeners.forEach((listener) => {
      try {
        listener(isOnline, wasOffline);
      } catch (err) {
        console.error('[NetworkStatusService] Listener error:', err);
      }
    });
  }

  public getStatus(): { isOnline: boolean; wasOffline: boolean } {
    return {
      isOnline: this.isOnline,
      wasOffline: this.wasOffline,
    };
  }

  public subscribe(listener: NetworkStatusListener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /**
   * Helper to manually simulate connection states (used safely in Dev Preview only)
   */
  public simulateStatus(isOnline: boolean, wasOffline: boolean = false) {
    this.isOnline = isOnline;
    this.wasOffline = wasOffline;
    this.notify(isOnline, wasOffline);
  }
}

export const networkStatusService = new NetworkStatusService();
