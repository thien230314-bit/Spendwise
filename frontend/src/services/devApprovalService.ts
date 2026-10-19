export type ApprovalStatus =
  | 'PENDING'
  | 'APPROVED'
  | 'NEEDS_REVISION'
  | 'KEEP_CURRENT'
  | 'APPLYING'
  | 'APPLIED'
  | 'APPLY_FAILED';

export type ParityStatus =
  | 'NOT_AUDITED'
  | 'AUDITING'
  | 'INCOMPLETE'
  | 'VERIFIED'
  | 'BLOCKED';

export interface PageApprovalRecord {
  route: string;
  previewTab: string;
  pageName: string;
  previewVersion: string;
  fingerprint: string;
  status: ApprovalStatus;
  parityStatus?: ParityStatus;
  parityMissingCount?: number;
  parityAuditNotes?: string;
  decidedAt: string | null;
  feedback: string | null;
  appliedAt: string | null;
  errorReason: string | null;
}

export interface UiApprovalStore {
  version: string;
  updatedAt: string;
  pages: Record<string, PageApprovalRecord>;
}

const DEV_API_ENDPOINT = '/api/dev/ui-approvals';

const LOCAL_STORAGE_KEY = 'spendwise_ui_approval_store';

function getLocalStore(): UiApprovalStore {
  try {
    const raw = localStorage.getItem(LOCAL_STORAGE_KEY);
    if (raw) return JSON.parse(raw);
  } catch (e) {
    // ignore
  }
  return {
    version: '2.0.0',
    updatedAt: new Date().toISOString(),
    pages: {},
  };
}

function saveLocalStore(store: UiApprovalStore) {
  try {
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(store));
  } catch (e) {
    // ignore
  }
}

export const devApprovalService = {
  async getStore(): Promise<UiApprovalStore | null> {
    try {
      const res = await fetch(DEV_API_ENDPOINT);
      if (res.ok) {
        const data = await res.json();
        if (data.success && data.data) {
          saveLocalStore(data.data);
          return data.data;
        }
      }
    } catch {
      // Server not reachable, fallback to localStorage
    }
    return getLocalStore();
  },

  async updateDecision(params: {
    pageKey: string;
    status: ApprovalStatus;
    previewVersion: string;
    fingerprint: string;
    parityStatus?: ParityStatus;
    feedback?: string | null;
  }): Promise<PageApprovalRecord | null> {
    // Update local storage first so user choices are never lost
    const store = getLocalStore();
    const existing = store.pages[params.pageKey] || {
      route: '',
      previewTab: params.pageKey,
      pageName: params.pageKey,
      previewVersion: params.previewVersion,
      fingerprint: params.fingerprint,
      status: 'PENDING',
      decidedAt: null,
      feedback: null,
      appliedAt: null,
      errorReason: null,
    };

    const updatedRecord: PageApprovalRecord = {
      ...existing,
      status: params.status,
      previewVersion: params.previewVersion,
      fingerprint: params.fingerprint,
      parityStatus: params.parityStatus || existing.parityStatus || 'VERIFIED',
      feedback: params.feedback !== undefined ? params.feedback : existing.feedback,
      decidedAt: new Date().toISOString(),
    };

    store.pages[params.pageKey] = updatedRecord;
    store.updatedAt = new Date().toISOString();
    saveLocalStore(store);

    try {
      const res = await fetch(DEV_API_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(params),
      });
      if (res.ok) {
        const data = await res.json();
        if (data.success && data.data) return data.data;
      }
    } catch {
      // Server not available, return the locally persisted record
    }

    return updatedRecord;
  },
};
