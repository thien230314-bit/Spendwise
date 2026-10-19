import React, { useState, useEffect, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { AdminSidebar } from './AdminSidebar';
import { AdminHeader } from './AdminHeader';
import { FloatingAdminAiAssistant } from './FloatingAdminAiAssistant';
import { GlobalNetworkBanner } from '../layout/GlobalNetworkBanner';
import { AlertTriangle, Megaphone, Sliders } from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import {
  systemOperationsService,
  SystemMaintenanceState,
  EmergencyAnnouncement,
} from '../../services/systemOperationsService';

interface AdminAppShellProps {
  children: ReactNode;
  pageTitle?: string;
  pageSubtitle?: string;
}

const ADMIN_SIDEBAR_STORAGE_KEY = 'spendwise_admin_sidebar_collapsed';

export const AdminAppShell: React.FC<AdminAppShellProps> = ({
  children,
  pageTitle,
  pageSubtitle,
}) => {
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState<boolean>(() => {
    const saved = localStorage.getItem(ADMIN_SIDEBAR_STORAGE_KEY);
    return saved === 'true';
  });

  const [maintenanceState, setMaintenanceState] = useState<SystemMaintenanceState>(() => {
    return systemOperationsService.getMaintenanceState();
  });

  const [activeAnnouncement, setActiveAnnouncement] = useState<EmergencyAnnouncement | null>(() => {
    return systemOperationsService.getActiveAnnouncement();
  });

  useEffect(() => {
    const unsubMaintenance = systemOperationsService.subscribeMaintenance((newState) => {
      setMaintenanceState(newState);
    });
    const unsubAnnouncements = systemOperationsService.subscribeAnnouncements(() => {
      setActiveAnnouncement(systemOperationsService.getActiveAnnouncement());
    });
    return () => {
      unsubMaintenance();
      unsubAnnouncements();
    };
  }, []);

  const handleToggleSidebar = () => {
    setIsSidebarCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(ADMIN_SIDEBAR_STORAGE_KEY, String(next));
      return next;
    });
  };

  return (
    <div
      id="admin-app-shell"
      className="flex min-h-screen bg-[#F8F9FA] dark:bg-[#0B1120] text-slate-900 dark:text-slate-100 transition-colors duration-150"
    >
      {/* Admin Sidebar */}
      <AdminSidebar
        isCollapsed={isSidebarCollapsed}
        onToggleCollapse={handleToggleSidebar}
      />

      {/* Main Admin Content Canvas */}
      <div className="flex flex-1 flex-col min-w-0 overflow-x-hidden relative">
        <AdminHeader pageTitle={pageTitle} pageSubtitle={pageSubtitle} />

        {/* Global Network Connectivity Status Banner */}
        <GlobalNetworkBanner />

        {/* System Maintenance Alert Banner for Admins */}
        {maintenanceState.enabled && (
          <div
            id="admin-maintenance-active-banner"
            className="bg-amber-500 text-slate-950 px-6 py-2.5 text-xs font-semibold flex items-center justify-between shadow-xs border-b border-amber-600"
          >
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>
                {isVi
                  ? 'Chế độ bảo trì hệ thống đang BẬT: Người dùng thông thường đang bị tạm khóa truy cập.'
                  : 'System Maintenance Mode is ACTIVE: Normal user access is currently suspended.'}
              </span>
            </div>
            <Link
              to="/admin/configuration"
              className="inline-flex items-center gap-1 underline font-bold hover:text-white transition-colors"
            >
              <Sliders className="h-3 w-3" />
              <span>{isVi ? 'Quản lý cấu hình' : 'Manage Config'}</span>
            </Link>
          </div>
        )}

        {/* Active Emergency Announcement Strip for Admins */}
        {activeAnnouncement && (
          <div
            id="admin-announcement-active-strip"
            className="bg-rose-50 dark:bg-rose-950/40 border-b border-rose-200 dark:border-rose-900/50 text-rose-800 dark:text-rose-300 px-6 py-2 text-xs flex items-center justify-between"
          >
            <div className="flex items-center gap-2 truncate">
              <Megaphone className="h-3.5 w-3.5 shrink-0 text-rose-600 dark:text-rose-400" />
              <span className="font-bold text-[11px] uppercase tracking-wider text-rose-600 dark:text-rose-400">
                {isVi ? 'Thông báo khẩn đang phát:' : 'Broadcasting Announcement:'}
              </span>
              <span className="font-medium truncate">{activeAnnouncement.title}</span>
            </div>
            <Link
              to="/admin/configuration"
              className="text-[11px] underline font-semibold text-rose-600 dark:text-rose-400 shrink-0 ml-4 hover:text-rose-800"
            >
              {isVi ? 'Xem chi tiết' : 'Details'}
            </Link>
          </div>
        )}

        {/* Main Admin Content */}

        <main className="flex-1 p-6 lg:p-8 w-full max-w-[1600px] mx-auto pb-24">
          {children}
        </main>

        {/* SystemAdmin Floating AI Assistant */}
        <FloatingAdminAiAssistant />
      </div>
    </div>
  );
};
