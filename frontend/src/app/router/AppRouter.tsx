import React from 'react';
import { BrowserRouter, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { AdminAppShell } from '../../components/admin/AdminAppShell';
import { ProtectedRoute } from '../../components/auth/ProtectedRoute';
import { RoleRoute } from '../../components/auth/RoleRoute';
import { SessionExpiredModal } from '../../components/auth/SessionExpiredModal';
import { ForcePasswordChangeModal } from '../../components/auth/ForcePasswordChangeModal';
import { LoginPage } from '../../features/auth/LoginPage';
import { ResetPasswordPage } from '../../features/auth/ResetPasswordPage';
import { ResetPinPage } from '../../features/auth/ResetPinPage';
import { ForbiddenPage } from '../../pages/ForbiddenPage';
import { useAuth } from '../../providers/AuthProvider';
import { resolveAdminReturnPath } from './adminWebRouting';

// Admin Feature Pages
import { AdminOverviewPage } from '../../features/admin/AdminOverviewPage';
import { AdminUsersPage } from '../../features/admin/AdminUsersPage';
import { AdminTransactionsPage } from '../../features/admin/AdminTransactionsPage';
import { AdminLedgerPage } from '../../features/admin/AdminLedgerPage';
import { AdminRiskPage } from '../../features/admin/AdminRiskPage';
import { AdminDisputesPage } from '../../features/admin/AdminDisputesPage';
import { AdminSupportPage } from '../../features/admin/AdminSupportPage';
import { AdminAuditPage } from '../../features/admin/AdminAuditPage';
import { AdminBankingPage } from '../../features/admin/AdminBankingPage';
import { AdminReconciliationPage } from '../../features/admin/AdminReconciliationPage';
import { AdminHealthPage } from '../../features/admin/AdminHealthPage';
import { AdminConfigurationPage } from '../../features/admin/AdminConfigurationPage';
import { AdminFeatureFlagsPage } from '../../features/admin/AdminFeatureFlagsPage';

import { UiPreviewCenterPage } from '../../features/preview/UiPreviewCenterPage';

// Root Dispatcher Component for "/" and fallback
const RootDispatcher: React.FC = () => {
  const { currentUser, isAuthenticated } = useAuth();

  if (!isAuthenticated || !currentUser) {
    return <Navigate to="/login" replace />;
  }

  if (currentUser.role === 'SystemAdmin') {
    return <Navigate to={resolveAdminReturnPath(currentUser.role)} replace />;
  }

  return <Navigate to="/forbidden" replace />;
};

const FallbackDispatcher: React.FC = () => (
  <main className="min-h-screen flex flex-col items-center justify-center gap-4" role="main">
    <h1>404</h1>
    <p>Trang không tồn tại / Page not found.</p>
    <a href="/login">Đăng nhập / Login</a>
  </main>
);

export const AppRouter: React.FC = () => {
  return (
    <BrowserRouter>
      {/* Session Expired dialog */}
      <SessionExpiredModal />

      {/* Force Password Change Modal (Unskippable when user mustChangePassword) */}
      <ForcePasswordChangeModal />

      <Routes>
        {/* Public Authentication Routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<Navigate to="/login" replace />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/reset-pin" element={<ResetPinPage />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />

        {/* Development UI Preview Center */}
        {import.meta.env.DEV && (
          <Route path="/preview/ui" element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <UiPreviewCenterPage />
            </RoleRoute>
          } />
        )}

        {/* Root Route: Role-Aware Dispatching */}
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <RootDispatcher />
            </ProtectedRoute>
          }
        />

        {/* ========================================================================= */}
        {/* SYSTEM ADMIN CONSOLE ROUTES (Strictly Role: 'SystemAdmin')                */}
        {/* Normal User trying to access /admin routes gets 403 Forbidden             */}
        {/* ========================================================================= */}
        <Route
          path="/admin"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminOverviewPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/overview"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminOverviewPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/users"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminUsersPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/transactions"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminTransactionsPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/ledger"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminLedgerPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/risk"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminRiskPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/disputes"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminDisputesPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/support"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminSupportPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/audit"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminAuditPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/sandbox"
          element={<Navigate to="/admin/banking" replace />}
        />
        <Route
          path="/admin/banking"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminBankingPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/reconciliation"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminReconciliationPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/health"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminHealthPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/configuration"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminConfigurationPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />
        <Route
          path="/admin/feature-flags"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <AdminAppShell>
                <AdminFeatureFlagsPage />
              </AdminAppShell>
            </RoleRoute>
          }
        />

        {/* Fallback route */}
        <Route path="*" element={<FallbackDispatcher />} />
      </Routes>
    </BrowserRouter>
  );
};
