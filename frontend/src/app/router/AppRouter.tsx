import React from 'react';
import { BrowserRouter, Routes, Route, Navigate, Link } from 'react-router-dom';
import { RoleRoute } from '../../components/auth/RoleRoute';
import { SessionExpiredModal } from '../../components/auth/SessionExpiredModal';
import { ForcePasswordChangeModal } from '../../components/auth/ForcePasswordChangeModal';
import { LoginPage } from '../../features/auth/LoginPage';
import { ResetPasswordPage } from '../../features/auth/ResetPasswordPage';
import { ResetPinPage } from '../../features/auth/ResetPinPage';
import { ForbiddenPage } from '../../pages/ForbiddenPage';
import { useAuth } from '../../providers/AuthProvider';
import { WebAdminShell } from '../../screens/WebAdminShell';
import { ShieldCheck, AlertCircle } from 'lucide-react';

/**
 * Root Dispatcher for "/"
 * Directs SystemAdmin to /admin, blocks normal User with /forbidden,
 * and unauthenticated visitors to /login.
 */
const RootDispatcher: React.FC = () => {
  const { currentUser, isAuthenticated } = useAuth();

  if (!isAuthenticated || !currentUser) {
    return <Navigate to="/login" replace />;
  }

  if (currentUser.role === 'SystemAdmin') {
    return <Navigate to="/admin" replace />;
  }

  // Normal User trying to access Web platform
  return <Navigate to="/forbidden" replace />;
};

/**
 * Clean 404 Fallback Dispatcher
 */
const NotFoundFallback: React.FC = () => {
  const { currentUser, isAuthenticated } = useAuth();
  const isAdmin = isAuthenticated && currentUser?.role === 'SystemAdmin';

  return (
    <main
      className="min-h-screen flex flex-col items-center justify-center p-6 bg-[var(--bg,#F8F9FA)] dark:bg-[#0B1120] text-[var(--ink,#0F172A)] dark:text-[#F1F5F9]"
      role="main"
    >
      <div className="max-w-md w-full text-center space-y-5 bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 rounded-2xl p-8 shadow-xl">
        <div className="inline-flex p-3.5 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
          <AlertCircle className="w-10 h-10" />
        </div>
        <div className="space-y-1.5">
          <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
            HTTP 404 Not Found
          </span>
          <h1 className="text-2xl font-extrabold tracking-tight">
            Trang không tồn tại
          </h1>
          <p className="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">
            Đường dẫn bạn yêu cầu không khả dụng hoặc đã được tái cấu trúc cho hệ thống Quản trị SpendWise.
          </p>
        </div>
        <div className="pt-2">
          {isAdmin ? (
            <Link
              to="/admin"
              className="inline-flex items-center justify-center gap-2 w-full px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm transition shadow-sm"
            >
              <ShieldCheck className="w-4 h-4" />
              <span>Quay lại Bảng Quản trị Admin</span>
            </Link>
          ) : (
            <Link
              to="/login"
              className="inline-flex items-center justify-center gap-2 w-full px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm transition shadow-sm"
            >
              <span>Về màn hình Đăng nhập</span>
            </Link>
          )}
        </div>
      </div>
    </main>
  );
};

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

        {/* Root Route: Role-Aware Dispatching */}
        <Route path="/" element={<RootDispatcher />} />

        {/* ========================================================================= */}
        {/* SPENDWISE SYSTEM ADMIN CONSOLE (Google AI Studio Remake)                  */}
        {/* Strictly protected for SystemAdmin / Admin only.                          */}
        {/* ========================================================================= */}
        <Route
          path="/admin"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="overview" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/overview"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="overview" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/reports"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="reports" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/tasks"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="tasks" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/alerts"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="alerts" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/users"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="users" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/transactions"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="transactions" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/ledger"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="transactions" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/support"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="support" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/disputes"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="support" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/system"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="broadcast" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/notifications"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="broadcast" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/operations"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="tasks" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/banking"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="bank_gateways" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/audit"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="config_audit" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/health"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="ai_ops" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/configuration"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="config_audit" />
            </RoleRoute>
          }
        />
        <Route
          path="/admin/feature-flags"
          element={
            <RoleRoute allowedRoles={['SystemAdmin']}>
              <WebAdminShell initialScreen="system" initialSystemSubTab="config_audit" />
            </RoleRoute>
          }
        />

        {/* ========================================================================= */}
        {/* RETIRED WEB USER ROUTES -> Strict redirect to /forbidden                  */}
        {/* ========================================================================= */}
        <Route path="/dashboard" element={<Navigate to="/forbidden" replace />} />
        <Route path="/wallet" element={<Navigate to="/forbidden" replace />} />
        <Route path="/transactions" element={<Navigate to="/forbidden" replace />} />
        <Route path="/transfer" element={<Navigate to="/forbidden" replace />} />
        <Route path="/budget" element={<Navigate to="/forbidden" replace />} />
        <Route path="/goals" element={<Navigate to="/forbidden" replace />} />
        <Route path="/groups" element={<Navigate to="/forbidden" replace />} />
        <Route path="/friends" element={<Navigate to="/forbidden" replace />} />
        <Route path="/notifications" element={<Navigate to="/forbidden" replace />} />
        <Route path="/settings" element={<Navigate to="/forbidden" replace />} />
        <Route path="/support" element={<Navigate to="/forbidden" replace />} />
        <Route path="/disputes" element={<Navigate to="/forbidden" replace />} />

        {/* Catch-all 404 Fallback */}
        <Route path="*" element={<NotFoundFallback />} />
      </Routes>
    </BrowserRouter>
  );
};
