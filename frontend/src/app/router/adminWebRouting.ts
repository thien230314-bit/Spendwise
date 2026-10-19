const adminPaths = new Set([
  '/admin', '/admin/overview', '/admin/users', '/admin/transactions',
  '/admin/ledger', '/admin/risk', '/admin/disputes', '/admin/support',
  '/admin/audit', '/admin/sandbox', '/admin/banking', '/admin/reconciliation',
  '/admin/health', '/admin/configuration', '/admin/feature-flags',
]);

export function resolveAdminReturnPath(role: unknown, from?: unknown): string {
  if (role !== 'SystemAdmin') return '/forbidden';
  if (!from || typeof from !== 'object') return '/admin';
  const { pathname, search, hash } = from as Record<string, unknown>;
  if (typeof pathname !== 'string' || !adminPaths.has(pathname)) return '/admin';
  const safeSearch = typeof search === 'string' && search.startsWith('?') ? search : '';
  const safeHash = typeof hash === 'string' && hash.startsWith('#') ? hash : '';
  return pathname + safeSearch + safeHash;
}
