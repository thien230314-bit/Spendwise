/**
 * SpendWise One-Time Selective Legacy Data Cleanup
 *
 * Removes ONLY known obsolete prototype keys and legacy mock/seed items by exact IDs/signatures.
 * NEVER deletes entire namespaces (spendwise_*, sepay_*) and preserves all genuine user data.
 * Safe and idempotent: if parsing fails, skips safely without destroying state.
 */

export const CLEAN_STATE_MIGRATION_KEY = 'spendwise_clean_state_v2';

/**
 * Safely filters an array stored in localStorage by key.
 * If key does not exist or JSON parsing fails, skips safely without wiping or throwing.
 */
function cleanArrayStorageKey(key: string, keepPredicate: (item: any) => boolean): void {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return;

    let parsed: unknown;
    try {
      parsed = JSON.parse(raw);
    } catch {
      // If parsing fails, skip safely - do not wipe storage
      return;
    }

    if (!Array.isArray(parsed)) {
      return;
    }

    const filtered = parsed.filter(keepPredicate);
    if (filtered.length !== parsed.length) {
      localStorage.setItem(key, JSON.stringify(filtered));
    }
  } catch (err) {
    console.warn(`[Safe Migration] Skipped cleanup for ${key}:`, err);
  }
}

export function runOneTimeCleanReset(): void {
  if (typeof window === 'undefined' || !window.localStorage) return;

  try {
    const alreadyMigrated = localStorage.getItem(CLEAN_STATE_MIGRATION_KEY);
    if (alreadyMigrated === 'true') {
      return;
    }

    // 1. Clean fake users: usr_demo_user_002, usr_demo_user_003, usr_demo_user_004 from spendwise_registered_users
    // (Do not remove Demo User usr_demo_user_001 or Demo SystemAdmin usr_sysadmin_001)
    const FAKE_USER_IDS = new Set(['usr_demo_user_002', 'usr_demo_user_003', 'usr_demo_user_004']);
    cleanArrayStorageKey('spendwise_registered_users', (u: any) => {
      if (!u || typeof u !== 'object') return true;
      return !FAKE_USER_IDS.has(u.id);
    });

    // 2. Clean fake groups: grp-room-101, grp-trip-dalat from spendwise_groups
    const FAKE_GROUP_IDS = new Set(['grp-room-101', 'grp-trip-dalat']);
    cleanArrayStorageKey('spendwise_groups', (g: any) => {
      if (!g || typeof g !== 'object') return true;
      return !FAKE_GROUP_IDS.has(g.id);
    });

    // 3. Clean legacy fake notifications from spendwise_notifications_*
    // Exact IDs: notif-1, notif-2, notif-3, notif-4, notif-5 or prefix notif-seed-, notif_demo_
    const FAKE_NOTIF_IDS = new Set(['notif-1', 'notif-2', 'notif-3', 'notif-4', 'notif-5']);
    const notifKeys: string[] = [];
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key && key.startsWith('spendwise_notifications_')) {
        notifKeys.push(key);
      }
    }
    for (const key of notifKeys) {
      cleanArrayStorageKey(key, (n: any) => {
        if (!n || typeof n !== 'object' || typeof n.id !== 'string') return true;
        if (FAKE_NOTIF_IDS.has(n.id)) return false;
        if (n.id.startsWith('notif-seed-') || n.id.startsWith('notif_demo_')) return false;
        return true;
      });
    }

    // 4. Clean legacy account recovery seed ONLY by exact known seed id: arr_init_001
    cleanArrayStorageKey('spendwise_account_recovery_requests', (r: any) => {
      if (!r || typeof r !== 'object') return true;
      if (r.id === 'arr_init_001') return false;
      return true;
    });

    // 5. Clean legacy friend request bootstrap data: ID begins with fr_bootstrap_
    cleanArrayStorageKey('spendwise_friend_requests', (fr: any) => {
      if (!fr || typeof fr !== 'object' || typeof fr.id !== 'string') return true;
      if (fr.id.startsWith('fr_bootstrap_')) return false;
      return true;
    });

    // 6. Remove obsolete prototype key only
    try {
      localStorage.removeItem('spendwise_admin_users_mock');
    } catch {
      // safe skip
    }

    // 7. Remove obsolete migration marker
    try {
      localStorage.removeItem('spendwise_clean_state_v1');
    } catch {
      // safe skip
    }

    // 8. Mark current selective migration complete
    try {
      localStorage.setItem(CLEAN_STATE_MIGRATION_KEY, 'true');
    } catch {
      // safe skip
    }
  } catch (err) {
    console.warn('SpendWise selective clean state migration skipped or failed:', err);
  }
}
