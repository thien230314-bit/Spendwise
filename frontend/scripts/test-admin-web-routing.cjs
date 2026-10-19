// Isolated component/route regressions; fixture identities are NOT authentication E2E.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const Module = require('node:module');
const ts = require('typescript');
const React = require('react');
const { renderToStaticMarkup } = require('react-dom/server');
const router = require('react-router-dom');
const root = path.resolve(__dirname, '..');
let identity = null;
let entry = '/';
let development = false;
const adminPaths = ['/admin', '/admin/overview', '/admin/users', '/admin/transactions', '/admin/ledger', '/admin/risk', '/admin/disputes', '/admin/support', '/admin/audit', '/admin/sandbox', '/admin/banking', '/admin/reconciliation', '/admin/health', '/admin/configuration', '/admin/feature-flags'];
const legacyPaths = ['/dashboard', '/wallet', '/transactions', '/transfer', '/budget', '/goals', '/groups', '/groups/group-id', '/friends', '/notifications', '/settings', '/support', '/disputes', '/contacts', '/wallets', '/budgets', '/transfers', '/recurring', '/rules', '/categories'];
const transportForbidden = () => { throw new Error('No real auth/provider transport permitted in isolated component tests'); };
const core = new Set(['src/app/router/AppRouter.tsx', 'src/app/router/adminWebRouting.ts', 'src/components/auth/RoleRoute.tsx', 'src/components/auth/ProtectedRoute.tsx', 'src/features/auth/AuthPage.tsx', 'src/components/layout/NotificationsDropdown.tsx']);
for (const extension of ['.ts', '.tsx']) {
  require.extensions[extension] = (module, filename) => {
    const source = fs.readFileSync(filename, 'utf8').replaceAll('import.meta.env.DEV', String(development));
    const result = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true, target: ts.ScriptTarget.ES2022 } });
    module._compile(result.outputText, filename);
  };
}
const load = Module._load;
Module._load = function (request, parent, isMain) {
  if (request === 'react-router-dom') return {
    ...router,
    BrowserRouter: ({ children }) => React.createElement(router.MemoryRouter, { initialEntries: [entry] }, children),
    Navigate: ({ to }) => React.createElement('output', { 'data-redirect': to }),
  };
  if (request.startsWith('.') && parent?.filename.startsWith(root)) {
    const resolved = Module._resolveFilename(request, parent);
    const relative = path.relative(root, resolved).replaceAll('\\', '/');
    if (relative.startsWith('node_modules/')) return load.apply(this, arguments);
    if (relative === 'src/providers/AuthProvider.tsx') return { useAuth: () => ({ currentUser: identity, isAuthenticated: !!identity, isLoading: false, initiateLogin: transportForbidden, completePinVerify: transportForbidden, completePinSetup: transportForbidden, logout: transportForbidden }) };
    if (relative === 'src/i18n/I18nContext.tsx') return { useI18n: () => ({ t: key => key, locale: 'vi-VN', setLocale() {} }) };
    if (relative === 'src/providers/ThemeProvider.tsx') return { useTheme: () => ({ theme: 'light', resolvedTheme: 'light', setTheme() {} }) };
    if (relative === 'src/providers/NotificationProvider.tsx') return { useNotifications: () => ({ notifications: [], unreadCount: 0, unreadByAccount: {}, accountList: [], markAsRead: transportForbidden, markAllAsRead: transportForbidden }) };
    if (relative === 'src/services/securityPinService.ts') return { isValidSecurityPin: pin => /^\d{6}$/.test(pin), securityPinService: { createPinResetRequest: transportForbidden } };
    if (!core.has(relative) && !relative.startsWith('src/utils/')) return new Proxy({ __esModule: true }, { get(target, key) {
      if (key === '__esModule') return true;
      return ({ children }) => React.createElement('section', { 'data-component': String(key) }, children || String(key));
    } });
  }
  return load.apply(this, arguments);
};
function renderRoute(url, role, dev = false) {
  entry = url;
  identity = role ? { id: 'isolated-fixture', role } : null;
  development = dev;
  const filename = path.join(root, 'src/app/router/AppRouter.tsx');
  delete require.cache[require.resolve(filename)];
  return renderToStaticMarkup(React.createElement(require(filename).AppRouter));
}
for (const route of adminPaths) {
  test(`Admin fixture retains ${route}`, () => assert.match(renderRoute(route, 'SystemAdmin'), /data-component="Admin|data-redirect="\/admin\/banking"/));
  test(`User fixture denied ${route}`, () => assert.doesNotMatch(renderRoute(route, 'User'), /data-component="Admin(?:AppShell|OverviewPage|UsersPage|BankingPage|ReconciliationPage)"/));
}
for (const route of legacyPaths) test(`Retired User route ${route}`, () => assert.match(renderRoute(route, 'User'), /404/));
test('Anonymous root redirects to login', () => assert.match(renderRoute('/', null), /data-redirect="\/login"/));
test('Admin root redirects to admin', () => assert.match(renderRoute('/', 'SystemAdmin'), /data-redirect="\/admin"/));
test('User root never renders dashboard', () => assert.match(renderRoute('/', 'User'), /data-redirect="\/forbidden"/));
test('Register redirects to login', () => assert.match(renderRoute('/register', null), /data-redirect="\/login"/));
for (const role of [null, 'User', 'SystemAdmin']) test(`Unknown path is 404 for ${role}`, () => assert.match(renderRoute('/missing-path', role), /404/));
test('Production preview unavailable', () => assert.match(renderRoute('/preview/ui', 'SystemAdmin'), /404/));
test('Development preview retained for Admin', () => assert.match(renderRoute('/preview/ui', 'SystemAdmin', true), /data-component="UiPreviewCenterPage"/));
test('Development preview denied to User', () => assert.match(renderRoute('/preview/ui', 'User', true), /data-component="ForbiddenPage"/));
for (const from of [{ pathname: '/dashboard' }, { pathname: '//evil.example' }, { pathname: '/admin/../wallet' }, { pathname: '/admin-not-really' }, { pathname: '/admin/missing' }, null]) {
  test(`Admin return-to rejects ${JSON.stringify(from)}`, () => assert.equal(require('../src/app/router/adminWebRouting').resolveAdminReturnPath('SystemAdmin', from), '/admin'));
}
test('Admin return-to preserves approved deep-link query/hash', () => assert.equal(require('../src/app/router/adminWebRouting').resolveAdminReturnPath('SystemAdmin', { pathname: '/admin/users', search: '?page=2', hash: '#details' }), '/admin/users?page=2#details'));
test('User return-to never gains Admin access', () => assert.equal(require('../src/app/router/adminWebRouting').resolveAdminReturnPath('User', { pathname: '/admin/users' }), '/forbidden'));
test('Login has no registration form or forgot-password control even with legacy prop', () => {
  identity = null;
  const { AuthPage } = require('../src/features/auth/AuthPage');
  const html = renderToStaticMarkup(React.createElement(router.MemoryRouter, { initialEntries: ['/login?action=forgot'] }, React.createElement(AuthPage, { initialMode: 'register' })));
  assert.match(html, /auth-form-box login/);
  assert.doesNotMatch(html, /auth-form-box register|btn-open-forgot-password-modal|Register now|Đăng ký ngay/);
});
test('Admin dropdown retains bell without User view-all URL', () => {
  identity = { id: 'isolated-fixture', role: 'SystemAdmin' };
  const { NotificationsDropdown } = require('../src/components/layout/NotificationsDropdown');
  const html = renderToStaticMarkup(React.createElement(router.MemoryRouter, null, React.createElement(NotificationsDropdown, { isOpen: true, onClose() {} })));
  assert.match(html, /notifications/);
  assert.doesNotMatch(html, /href="\/notifications"/);
  assert.match(html, /class="ml-auto text-\[11px\]/);
});
for (const stage of ['pin_verify', 'pin_setup']) test(`Existing ${stage} panel renders without registration state`, () => {
  identity = null;
  const useState = React.useState;
  React.useState = initial => useState(initial === 'credentials' ? stage : initial);
  try {
    const { AuthPage } = require('../src/features/auth/AuthPage');
    const html = renderToStaticMarkup(React.createElement(router.MemoryRouter, null, React.createElement(AuthPage)));
    assert.match(html, stage === 'pin_setup' ? /Create Security PIN|Tạo Mã bảo mật/ : /Security PIN Verification|Xác thực Mã bảo mật/);
  } finally { React.useState = useState; }
});
