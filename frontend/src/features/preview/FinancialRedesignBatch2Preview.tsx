import React, { useState } from 'react';
import {
  LayoutDashboard,
  Wallet,
  ArrowLeftRight,
  Send,
  PieChart,
  Target,
  Users,
  Bell,
  LifeBuoy,
  Settings,
  UserPlus,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { cn } from '../../utils/cn';
import { PreviewApprovalBar } from './PreviewApprovalBar';

// 1. Dashboard Layouts
import { DashboardFullParityRedesign } from '../dashboard/DashboardFullParityRedesign';
import { DashboardLayoutA } from './layouts/DashboardLayoutA';
import { DashboardLayoutB } from './layouts/DashboardLayoutB';

// 2. Wallet Layouts
import { WalletPage } from '../wallet/WalletPage';
import { WalletLayoutA } from './layouts/WalletLayoutA';
import { WalletLayoutB } from './layouts/WalletLayoutB';

// 3. Transactions Layouts
import { TransactionsPage } from '../transactions/TransactionsPage';
import { TransactionsLayoutA } from './layouts/TransactionsLayoutA';
import { TransactionsLayoutB } from './layouts/TransactionsLayoutB';

// 4. Transfer Layouts
import { TransferPage } from '../transfer/TransferPage';
import { TransferLayoutA } from './layouts/TransferLayoutA';
import { TransferLayoutB } from './layouts/TransferLayoutB';

// 5. Budget Layouts
import { BudgetPage } from '../budget/BudgetPage';
import { BudgetLayoutA } from './layouts/BudgetLayoutA';
import { BudgetLayoutB } from './layouts/BudgetLayoutB';

// 6. Goals Layouts
import { GoalsPage } from '../goals/GoalsPage';
import { GoalsLayoutA } from './layouts/GoalsLayoutA';
import { GoalsLayoutB } from './layouts/GoalsLayoutB';

// 7. Groups Layouts
import { GroupsPage } from '../groups/GroupsPage';
import { GroupsLayoutA } from './layouts/GroupsLayoutA';
import { GroupsLayoutB } from './layouts/GroupsLayoutB';

// 8. Friends Layouts
import { FriendsPage } from '../friends/FriendsPage';
import { FriendsLayoutA } from './layouts/FriendsLayoutA';
import { FriendsLayoutB } from './layouts/FriendsLayoutB';

// 9. Notifications Layouts
import { NotificationsPage } from '../notifications/NotificationsPage';
import { NotificationsLayoutA } from './layouts/NotificationsLayoutA';
import { NotificationsLayoutB } from './layouts/NotificationsLayoutB';

// 10. Support & Disputes Layouts
import { SupportDisputesPage } from '../support/SupportDisputesPage';
import { SupportLayoutA } from './layouts/SupportLayoutA';
import { SupportLayoutB } from './layouts/SupportLayoutB';

// 11. Settings Layouts
import { SettingsPage } from '../settings/SettingsPage';
import { SettingsLayoutA } from './layouts/SettingsLayoutA';
import { SettingsLayoutB } from './layouts/SettingsLayoutB';

// Popup UI Inspector & Safe Renderer
import { PopupInspectorDrawer } from './popups/PopupInspectorDrawer';
import { SafePopupRenderer } from './popups/SafePopupRenderer';
import { Layers, Check } from 'lucide-react';

type LayoutChoice = 'layout_a' | 'layout_b' | 'baseline';

export const FinancialRedesignBatch2Preview: React.FC = () => {
  const { locale } = useI18n();

  // Active sub-page in Full User Web preview
  const [activeTab, setActiveTab] = useState<
    'dashboard' | 'wallet' | 'transactions' | 'transfer' | 'budget' | 'goals' | 'groups' | 'friends' | 'notifications' | 'support' | 'settings'
  >('dashboard');

  const subPages = [
    { id: 'dashboard', label: locale === 'vi-VN' ? '1. Tổng quan (Dashboard)' : '1. Dashboard', icon: LayoutDashboard },
    { id: 'wallet', label: locale === 'vi-VN' ? '2. Ví (Wallet)' : '2. Wallet', icon: Wallet },
    { id: 'transactions', label: locale === 'vi-VN' ? '3. Giao dịch (Transactions)' : '3. Transactions', icon: ArrowLeftRight },
    { id: 'transfer', label: locale === 'vi-VN' ? '4. Chuyển tiền (Transfer)' : '4. Transfer', icon: Send },
    { id: 'budget', label: locale === 'vi-VN' ? '5. Ngân sách (Budgets)' : '5. Budgets', icon: PieChart },
    { id: 'goals', label: locale === 'vi-VN' ? '6. Mục tiêu (Goals)' : '6. Goals', icon: Target },
    { id: 'groups', label: locale === 'vi-VN' ? '7. Quỹ nhóm (Groups)' : '7. Groups', icon: Users },
    { id: 'friends', label: locale === 'vi-VN' ? '8. Bạn bè (Friends)' : '8. Friends', icon: UserPlus },
    { id: 'notifications', label: locale === 'vi-VN' ? '9. Thông báo (Notifications)' : '9. Notifications', icon: Bell },
    { id: 'support', label: locale === 'vi-VN' ? '10. Hỗ trợ & Khiếu nại (Support)' : '10. Support & Disputes', icon: LifeBuoy },
    { id: 'settings', label: locale === 'vi-VN' ? '11. Cài đặt (Settings)' : '11. Settings', icon: Settings },
  ];

  return (
    <div className="space-y-6">
      {/* Batch 2 Sub-Navigation Bar */}
      <div className="flex items-center gap-1.5 p-1 bg-slate-100 dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 w-fit max-w-full overflow-x-auto text-xs font-semibold no-scrollbar">
        {subPages.map((tab) => {
          const Icon = tab.icon;
          const isSelected = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as any)}
              className={cn(
                'px-4 py-2 rounded-xl transition-all whitespace-nowrap cursor-pointer flex items-center gap-2',
                isSelected
                  ? 'bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-xs font-bold'
                  : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
              )}
            >
              <Icon className="h-4 w-4 shrink-0" />
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* ================= 1. DASHBOARD PREVIEW ================= */}
      {activeTab === 'dashboard' && <DashboardPreviewSection />}

      {/* ================= 2. WALLET PREVIEW ================= */}
      {activeTab === 'wallet' && <WalletPreviewSection />}

      {/* ================= 3. TRANSACTIONS PREVIEW ================= */}
      {activeTab === 'transactions' && <TransactionsPreviewSection />}

      {/* ================= 4. TRANSFER PREVIEW ================= */}
      {activeTab === 'transfer' && <TransferPreviewSection />}

      {/* ================= 5. BUDGET PREVIEW ================= */}
      {activeTab === 'budget' && <BudgetPreviewSection />}

      {/* ================= 6. GOALS PREVIEW ================= */}
      {activeTab === 'goals' && <GoalsPreviewSection />}

      {/* ================= 7. GROUPS PREVIEW ================= */}
      {activeTab === 'groups' && <GroupsPreviewSection />}

      {/* ================= 8. FRIENDS PREVIEW ================= */}
      {activeTab === 'friends' && <FriendsPreviewSection />}

      {/* ================= 9. NOTIFICATIONS PREVIEW ================= */}
      {activeTab === 'notifications' && <NotificationsPreviewSection />}

      {/* ================= 10. SUPPORT & DISPUTES PREVIEW ================= */}
      {activeTab === 'support' && <SupportPreviewSection />}

      {/* ================= 11. SETTINGS PREVIEW ================= */}
      {activeTab === 'settings' && <SettingsPreviewSection />}
    </div>
  );
};

// Generic Layout Selector Header
interface LayoutBarProps {
  pageNameVi: string;
  pageNameEn: string;
  selectedLayout: LayoutChoice;
  onSelectLayout: (c: LayoutChoice) => void;
  onOpenInspector?: () => void;
}

const LayoutSelectorBar: React.FC<LayoutBarProps> = ({
  pageNameVi,
  pageNameEn,
  selectedLayout,
  onSelectLayout,
  onOpenInspector,
}) => {
  const { locale } = useI18n();
  return (
    <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <div className="flex items-center gap-2">
          <span className="px-2 py-0.5 rounded text-[10px] font-black uppercase tracking-wider bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
            LAYOUT OPTIMIZATION
          </span>
          <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? `${pageNameVi} — Phương án bố cục` : `${pageNameEn} Layout Proposals`}
          </h3>
        </div>
        <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
          {locale === 'vi-VN'
            ? 'Giữ nguyên 100% phong cách thiết kế SpendWise gốc — Tối ưu hóa cách sắp xếp, phân nhóm và nhịp điệu section.'
            : 'Preserve original SpendWise design — Optimize layout hierarchy, spacing and section arrangement.'}
        </p>
      </div>

      <div className="flex items-center gap-2.5 flex-wrap">
        {onOpenInspector && (
          <button
            type="button"
            onClick={onOpenInspector}
            className="px-3.5 py-1.5 rounded-xl bg-indigo-50 hover:bg-indigo-100 dark:bg-indigo-950/60 dark:hover:bg-indigo-900/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 text-xs font-bold transition-all cursor-pointer flex items-center gap-1.5 shadow-2xs"
          >
            <Layers className="h-4 w-4" />
            <span>{locale === 'vi-VN' ? 'XEM POPUP UI' : 'POPUP INSPECTOR'}</span>
          </button>
        )}

        <div className="flex items-center gap-1.5 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl text-xs font-bold shrink-0 border border-slate-200 dark:border-slate-700">
          <button
            type="button"
            onClick={() => onSelectLayout('layout_a')}
            className={cn(
              'px-3.5 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 font-semibold',
              selectedLayout === 'layout_a'
                ? 'bg-emerald-600 text-white shadow-md ring-2 ring-emerald-400/50 scale-[1.02]'
                : 'text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-200/50 dark:hover:bg-slate-700/50'
            )}
          >
            {selectedLayout === 'layout_a' && <Check className="w-3.5 h-3.5 stroke-[3]" />}
            <span>{locale === 'vi-VN' ? 'Layout A (Balanced)' : 'Layout A'}</span>
          </button>

          <button
            type="button"
            onClick={() => onSelectLayout('layout_b')}
            className={cn(
              'px-3.5 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 font-semibold',
              selectedLayout === 'layout_b'
                ? 'bg-cyan-600 text-white shadow-md ring-2 ring-cyan-400/50 scale-[1.02]'
                : 'text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-200/50 dark:hover:bg-slate-700/50'
            )}
          >
            {selectedLayout === 'layout_b' && <Check className="w-3.5 h-3.5 stroke-[3]" />}
            <span>{locale === 'vi-VN' ? 'Layout B (Efficient)' : 'Layout B'}</span>
          </button>

          <button
            type="button"
            onClick={() => onSelectLayout('baseline')}
            className={cn(
              'px-3.5 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 font-semibold',
              selectedLayout === 'baseline'
                ? 'bg-slate-900 text-white dark:bg-slate-700 shadow-md ring-2 ring-slate-400/50 scale-[1.02]'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-200/50 dark:hover:bg-slate-700/50'
            )}
          >
            {selectedLayout === 'baseline' && <Check className="w-3.5 h-3.5 stroke-[3]" />}
            <span>{locale === 'vi-VN' ? 'Bản gốc (Baseline)' : 'Original'}</span>
          </button>
        </div>
      </div>
    </div>
  );
};

// 1. Dashboard Section
const DashboardPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Dashboard Tổng quan"
        pageNameEn="Dashboard"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="dashboard"
        pageName={`Tổng quan Dashboard (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`dash-${selectedLayout}`}
        route="/dashboard"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 16 Feature IDs gốc SpendWise. Tối ưu hóa phân bổ lưới, dải KPI và biểu đồ trực quan."
      />

      {selectedLayout === 'layout_a' && (
        <DashboardLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && <DashboardLayoutB />}
      {selectedLayout === 'baseline' && <DashboardFullParityRedesign />}

      <PopupInspectorDrawer
        pageKey="dashboard"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 2. Wallet Section
const WalletPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Ví của tôi (Wallet)"
        pageNameEn="Wallet"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="wallet"
        pageName={`Ví của tôi (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`wallet-${selectedLayout}`}
        route="/wallet"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 14 Feature IDs gốc: Ví nội bộ, SePay Open Banking Hub, Bảng biến động số dư."
      />

      {selectedLayout === 'layout_a' && (
        <WalletLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <WalletLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <WalletPage />}

      <PopupInspectorDrawer
        pageKey="wallet"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 3. Transactions Section
const TransactionsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Lịch sử giao dịch (Transactions)"
        pageNameEn="Transactions"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="transactions"
        pageName={`Lịch sử giao dịch (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`tx-${selectedLayout}`}
        route="/transactions"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 14 Feature IDs gốc: 3 tabs (History, Recurring, Rules), tìm kiếm & lọc đa tiêu chí."
      />

      {selectedLayout === 'layout_a' && (
        <TransactionsLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <TransactionsLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <TransactionsPage />}

      <PopupInspectorDrawer
        pageKey="transactions"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 4. Transfer Section
const TransferPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Chuyển tiền (Transfer)"
        pageNameEn="Transfer"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="transfer"
        pageName={`Chuyển tiền (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`transfer-${selectedLayout}`}
        route="/transfer"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 8 Feature IDs gốc: Tra cứu SĐT 10 số, Gợi ý bạn bè, Lời nhắn, PIN 6 số, Lịch sử lệnh."
      />

      {selectedLayout === 'layout_a' && (
        <TransferLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <TransferLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <TransferPage />}

      <PopupInspectorDrawer
        pageKey="transfer"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 5. Budget Section
const BudgetPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Ngân sách (Budgets)"
        pageNameEn="Budgets"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="budget"
        pageName={`Ngân sách (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`budget-${selectedLayout}`}
        route="/budget"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 8 Feature IDs gốc: Bộ chọn tháng MM/YYYY, Thước đo hạn mức, Tiến độ danh mục."
      />

      {selectedLayout === 'layout_a' && (
        <BudgetLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <BudgetLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <BudgetPage />}

      <PopupInspectorDrawer
        pageKey="budget"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 6. Goals Section
const GoalsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Mục tiêu tích lũy (Goals)"
        pageNameEn="Goals"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="goals"
        pageName={`Mục tiêu tích lũy (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`goals-${selectedLayout}`}
        route="/goals"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 8 Feature IDs gốc: Thống kê tổng tích lũy, Thẻ mục tiêu, Deadline, Nạp/Rút PIN 6 số."
      />

      {selectedLayout === 'layout_a' && (
        <GoalsLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <GoalsLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <GoalsPage />}

      <PopupInspectorDrawer
        pageKey="goals"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 7. Groups Section
const GroupsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Quỹ nhóm & Chi tiêu chung (Groups)"
        pageNameEn="Groups & Shared Funds"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="groups"
        pageName={`Quỹ nhóm (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`groups-${selectedLayout}`}
        route="/groups"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 16 Feature IDs gốc: Danh sách nhóm, Tổng quỹ tích lũy, Đợt góp, Chi tiết nhóm, Hoạt động chi tiêu."
      />

      {selectedLayout === 'layout_a' && <GroupsLayoutA />}
      {selectedLayout === 'layout_b' && <GroupsLayoutB />}
      {selectedLayout === 'baseline' && <GroupsPage />}

      <PopupInspectorDrawer
        pageKey="groups"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 8. Friends Section
const FriendsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Bạn bè & Danh bạ (Friends)"
        pageNameEn="Friends & Contacts"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="friends"
        pageName={`Bạn bè (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`friends-${selectedLayout}`}
        route="/friends"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 8 Feature IDs gốc: Danh bạ, Tra cứu SĐT, Lời mời kết nối, Biệt danh, Chuyển tiền nhanh."
      />

      {selectedLayout === 'layout_a' && (
        <FriendsLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <FriendsLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <FriendsPage />}

      <PopupInspectorDrawer
        pageKey="friends"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 9. Notifications Section
const NotificationsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Trung tâm thông báo (Notifications)"
        pageNameEn="Notifications Hub"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="notifications"
        pageName={`Thông báo (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`notifs-${selectedLayout}`}
        route="/notifications"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 6 Feature IDs gốc: Lọc ví tài khoản, Tùy chỉnh ngưỡng thông báo, Đánh dấu đã đọc."
      />

      {selectedLayout === 'layout_a' && (
        <NotificationsLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <NotificationsLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <NotificationsPage />}

      <PopupInspectorDrawer
        pageKey="notifications"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 10. Support & Disputes Section
const SupportPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Hỗ trợ & Khiếu nại (Support & Disputes)"
        pageNameEn="Support & Disputes"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="support"
        pageName={`Hỗ trợ & Khiếu nại (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`support-${selectedLayout}`}
        route="/support"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 8 Feature IDs gốc: Tạo yêu cầu hỗ trợ, Tạo khiếu nại giao dịch, Lịch sử đối soát."
      />

      {selectedLayout === 'layout_a' && (
        <SupportLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <SupportLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <SupportDisputesPage />}

      <PopupInspectorDrawer
        pageKey="support"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};

// 11. Settings Section
const SettingsPreviewSection: React.FC = () => {
  const [selectedLayout, setSelectedLayout] = useState<LayoutChoice>('layout_a');
  const [isInspectorOpen, setIsInspectorOpen] = useState(false);
  const [activePopupId, setActivePopupId] = useState<string | null>(null);
  const [stateMode, setStateMode] = useState<'default' | 'prefill' | 'error'>('default');

  return (
    <div className="space-y-6">
      <LayoutSelectorBar
        pageNameVi="Cài đặt tài khoản & Bảo mật (Settings)"
        pageNameEn="Account Settings"
        selectedLayout={selectedLayout}
        onSelectLayout={setSelectedLayout}
        onOpenInspector={() => setIsInspectorOpen(true)}
      />

      <PreviewApprovalBar
        pageKey="settings"
        pageName={`Cài đặt (${
          selectedLayout === 'layout_a'
            ? 'Layout A: Balanced'
            : selectedLayout === 'layout_b'
            ? 'Layout B: Efficient'
            : 'Original Baseline'
        })`}
        previewVersion={selectedLayout === 'layout_a' ? 'layout-v1-A' : selectedLayout === 'layout_b' ? 'layout-v1-B' : 'original-v2.0'}
        fingerprint={`settings-${selectedLayout}`}
        route="/settings"
        parityStatus="VERIFIED"
        parityMissingCount={0}
        parityAuditNotes="Giữ nguyên 100% design system & 10 Feature IDs gốc: Đổi PIN 6 số, Đổi mật khẩu, Hồ sơ cá nhân, Quyền riêng tư."
      />

      {selectedLayout === 'layout_a' && (
        <SettingsLayoutA
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'layout_b' && (
        <SettingsLayoutB
          onOpenPopup={(id, mode) => {
            setActivePopupId(id);
            setStateMode(mode || 'default');
          }}
        />
      )}
      {selectedLayout === 'baseline' && <SettingsPage />}

      <PopupInspectorDrawer
        pageKey="settings"
        isOpen={isInspectorOpen}
        onClose={() => setIsInspectorOpen(false)}
        selectedPopupId={activePopupId}
        onSelectPopup={(id, mode) => {
          setActivePopupId(id);
          setStateMode(mode);
        }}
      />

      <SafePopupRenderer
        popupId={activePopupId}
        stateMode={stateMode}
        onClose={() => setActivePopupId(null)}
      />
    </div>
  );
};


