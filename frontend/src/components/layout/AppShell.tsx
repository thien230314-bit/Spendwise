import React, { useState, useEffect, ReactNode } from 'react';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { FloatingAiAssistant } from '../ai/FloatingAiAssistant';
import { MaintenanceScreen } from './MaintenanceScreen';
import { EmergencyAnnouncementBanner } from './EmergencyAnnouncementBanner';
import { GlobalNetworkBanner } from './GlobalNetworkBanner';
import { useAuth } from '../../providers/AuthProvider';
import {
  systemOperationsService,
  SystemMaintenanceState,
} from '../../services/systemOperationsService';

interface AppShellProps {
  children: ReactNode;
  pageTitle?: string;
  pageSubtitle?: string;
}

const SIDEBAR_STORAGE_KEY = 'spendwise_sidebar_collapsed';

export const AppShell: React.FC<AppShellProps> = ({ children, pageTitle, pageSubtitle }) => {
  const { currentUser } = useAuth();
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState<boolean>(() => {
    const saved = localStorage.getItem(SIDEBAR_STORAGE_KEY);
    return saved === 'true';
  });

  const [maintenanceState, setMaintenanceState] = useState<SystemMaintenanceState>(() => {
    return systemOperationsService.getMaintenanceState();
  });

  useEffect(() => {
    const unsubscribe = systemOperationsService.subscribeMaintenance((newState) => {
      setMaintenanceState(newState);
    });
    return () => unsubscribe();
  }, []);

  const handleToggleSidebar = () => {
    setIsSidebarCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_STORAGE_KEY, String(next));
      return next;
    });
  };

  // Block normal users during active maintenance
  const isNormalUser = currentUser?.role !== 'SystemAdmin';
  if (maintenanceState.enabled && isNormalUser) {
    return <MaintenanceScreen maintenanceState={maintenanceState} />;
  }

  return (
    <div className="flex min-h-screen bg-[#F8F9FA] dark:bg-[#0B1120] text-slate-900 dark:text-slate-100 transition-colors duration-150">
      {/* Fixed/Collapsible Sidebar */}
      <Sidebar isCollapsed={isSidebarCollapsed} onToggleCollapse={handleToggleSidebar} />

      {/* Main App Canvas */}
      <div className="flex flex-1 flex-col min-w-0 overflow-x-hidden relative">
        <Header pageTitle={pageTitle} pageSubtitle={pageSubtitle} />

        {/* Global Network Connectivity Status Banner (Online/Offline/Reconnected) */}
        <GlobalNetworkBanner />

        {/* Global Emergency Announcement Banner */}
        <EmergencyAnnouncementBanner />

        <main className="flex-1 p-6 lg:p-8 w-full max-w-[1600px] mx-auto pb-24">
          {children}
        </main>

        {/* Global Floating AI Assistant */}
        <FloatingAiAssistant />
      </div>
    </div>
  );
};
