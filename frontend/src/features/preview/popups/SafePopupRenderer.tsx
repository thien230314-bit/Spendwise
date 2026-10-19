import React, { useState } from 'react';
import {
  X,
  Shield,
  CheckCircle2,
  AlertCircle,
  Lock,
  ArrowRight,
  TrendingUp,
  Receipt,
  Wallet,
  Send,
  PieChart,
  Target,
  Users,
  ChevronDown,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { cn } from '../../../utils/cn';
import { formatMoney } from '../../../utils/money';

// Import production safe modals
import { SecurityPinChallengeModal } from '../../../components/security/SecurityPinChallengeModal';
import { CreateGroupModal } from '../../groups/modals/CreateGroupModal';
import { InviteMemberModal } from '../../groups/modals/InviteMemberModal';
import { FundContributionModal } from '../../groups/modals/FundContributionModal';
import { CreateFundRoundModal } from '../../groups/modals/CreateFundRoundModal';
import { AddSharedExpenseModal } from '../../groups/modals/AddSharedExpenseModal';
import { OwnerSettingsModal } from '../../groups/modals/OwnerSettingsModal';
import { CategoryManagerModal } from '../../transactions/modals/CategoryManagerModal';
import { CreateRecurringModal } from '../../transactions/modals/CreateRecurringModal';
import { CreateRuleModal } from '../../transactions/modals/CreateRuleModal';
import { AddFriendModal } from '../../friends/modals/AddFriendModal';
import { EditNicknameModal } from '../../friends/modals/EditNicknameModal';
import { AccountPrefModal } from '../../notifications/AccountPrefModal';

interface SafePopupRendererProps {
  popupId: string | null;
  stateMode: 'default' | 'prefill' | 'error';
  onClose: () => void;
}

export const SafePopupRenderer: React.FC<SafePopupRendererProps> = ({
  popupId,
  stateMode,
  onClose,
}) => {
  const { locale, t } = useI18n();

  // Internal test states for forms
  const [testPin, setTestPin] = useState('');
  const [testName, setTestName] = useState(stateMode === 'prefill' ? 'Ví Chi Tiêu Hàng Ngày' : '');
  const [testAmount, setTestAmount] = useState(stateMode === 'prefill' ? '5000000' : '');
  const [testCategory, setTestCategory] = useState(stateMode === 'prefill' ? 'Ăn uống & Cà phê' : '');
  const [testNote, setTestNote] = useState(stateMode === 'prefill' ? 'Chi tiêu cố định trong tháng' : '');

  if (!popupId) return null;

  // 1. PIN CHALLENGE POPUP (Used across Dashboard, Transfer, Wallet, Goals)
  if (popupId === 'dash-pin-challenge' || popupId === 'transfer-pin' || popupId === 'goal-withdraw-pin') {
    return (
      <SecurityPinChallengeModal
        isOpen={true}
        title={locale === 'vi-VN' ? 'Xác thực mã PIN bảo mật 6 số' : '6-Digit Security PIN Challenge'}
        description={
          locale === 'vi-VN'
            ? 'Vui lòng nhập mã PIN bảo mật để hoàn tất phiên giao dịch an toàn (Chế độ Preview)'
            : 'Enter your 6-digit security PIN to authorize this action (Preview Mode)'
        }
        amount={1500000}
        onSuccess={() => onClose()}
        onCancel={onClose}
      />
    );
  }

  // 2. GROUPS MODALS (Reusing exact production modals with dummy callbacks)
  if (popupId === 'group-create') {
    return (
      <CreateGroupModal
        isOpen={true}
        onClose={onClose}
        onSubmit={async () => {
          onClose();
        }}
      />
    );
  }

  if (popupId === 'group-invite-member') {
    return (
      <InviteMemberModal
        isOpen={true}
        onClose={onClose}
        groupId="demo-group-id"
      />
    );
  }

  if (popupId === 'group-fund-contrib') {
    return (
      <FundContributionModal
        isOpen={true}
        onClose={onClose}
        groupId="demo-group-id"
      />
    );
  }

  if (popupId === 'group-create-round') {
    return (
      <CreateFundRoundModal
        isOpen={true}
        onClose={onClose}
        groupId="demo-group-id"
      />
    );
  }

  if (popupId === 'group-add-expense') {
    return (
      <AddSharedExpenseModal
        isOpen={true}
        onClose={onClose}
        group={{
          id: 'demo-group-id',
          name: 'Gia đình nhỏ & Chuyến đi Đà Lạt',
          type: 'Trip',
          members: [
            { id: 'm1', name: 'Bạn (Quản trị)', role: 'Owner' },
            { id: 'm2', name: 'Minh Trang', role: 'Member' },
          ],
          sharedFund: { balance: 14500000 },
        } as any}
      />
    );
  }

  if (popupId === 'group-edit-settings') {
    return (
      <OwnerSettingsModal
        isOpen={true}
        onClose={onClose}
        group={{
          id: 'demo-group-id',
          name: 'Gia đình nhỏ & Chuyến đi Đà Lạt',
          description: 'Quỹ tiết kiệm và chia sẻ chi tiêu cho kỳ nghỉ chung',
          type: 'Trip',
          members: [
            { id: 'm1', name: 'Bạn (Quản trị)', role: 'Owner' },
            { id: 'm2', name: 'Minh Trang', role: 'Member' },
          ],
        } as any}
        currentUserId="user-me"
        onUpdateGroup={async () => {}}
        onTransferOwnership={async () => {}}
        onCloseGroup={async () => {}}
        onRequestGroupDeletion={async () => {}}
        onCancelGroupDeletion={async () => {}}
      />
    );
  }

  // 3. TRANSACTIONS MODALS (Reusing exact production components)
  if (popupId === 'tx-category-mgr') {
    return <CategoryManagerModal isOpen={true} onClose={onClose} />;
  }

  if (popupId === 'tx-recurring-create') {
    return (
      <CreateRecurringModal
        isOpen={true}
        onClose={onClose}
        onSuccess={() => onClose()}
        wallets={[{ id: 'w1', name: 'Ví Chính' } as any]}
        categories={[{ id: 'c1', name: 'Tiền nhà' } as any]}
      />
    );
  }

  if (popupId === 'tx-rule-create') {
    return (
      <CreateRuleModal
        isOpen={true}
        onClose={onClose}
        onSuccess={() => onClose()}
        categories={[{ id: 'c1', name: 'Ăn uống' } as any]}
      />
    );
  }

  // 3B. ADD EXPENSE MODAL (Authentic full form with inputs, select category, select wallet, date, notes)
  if (popupId === 'tx-add-expense' || popupId === 'dash-expense') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="h-9 w-9 rounded-2xl bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400 flex items-center justify-center font-bold">
                −
              </div>
              <div>
                <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Thêm khoản chi tiêu mới' : 'Add Expense Transaction'}
                </h3>
                <p className="text-[11px] text-slate-400">
                  {locale === 'vi-VN' ? 'Nhập thông tin giao dịch phát sinh' : 'Fill expense details'}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="space-y-4 text-xs">
            {/* Số tiền */}
            <div className="space-y-1.5">
              <label className="font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Số tiền (VND) *' : 'Amount (VND) *'}
              </label>
              <div className="relative">
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: 65000' : 'e.g. 65000'}
                  value={testAmount || (stateMode === 'prefill' ? '125000' : '')}
                  onChange={(e) => setTestAmount(e.target.value)}
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold placeholder:font-normal focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">
                  VND
                </span>
              </div>
            </div>

            {/* Danh mục & Ví thanh toán */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Danh mục chi tiêu' : 'Category'}
                </label>
                <select
                  value={testCategory}
                  onChange={(e) => setTestCategory(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none focus:ring-2 focus:ring-rose-500 cursor-pointer"
                >
                  <option value="Ăn uống & Cà phê">Ăn uống & Cà phê</option>
                  <option value="Đi lại & Xăng xe">Đi lại & Xăng xe</option>
                  <option value="Mua sắm">Mua sắm siêu thị</option>
                  <option value="Hóa đơn & Tiện ích">Hóa đơn điện nước</option>
                  <option value="Nhà cửa & Tiền thuê">Tiền thuê nhà</option>
                  <option value="Giải trí & Du lịch">Giải trí & Xem phim</option>
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Ví thanh toán' : 'Payment Wallet'}
                </label>
                <select
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none focus:ring-2 focus:ring-rose-500 cursor-pointer"
                >
                  <option value="w1">Ví Chi Tiêu Hàng Ngày (12.450.000 ₫)</option>
                  <option value="w2">Ví Tiết Kiệm (50.000.000 ₫)</option>
                  <option value="w3">Tài khoản Ngân hàng Vietcombank (8.200.000 ₫)</option>
                </select>
              </div>
            </div>

            {/* Ngày giờ & Địa điểm */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Ngày & Giờ giao dịch *' : 'Date & Time *'}
                </label>
                <input
                  type="datetime-local"
                  defaultValue="2026-10-13T11:25"
                  className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Địa điểm / Đối tác' : 'Merchant / Partner'}
                </label>
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: Highlands Coffee' : 'e.g. Starbucks'}
                  defaultValue={stateMode === 'prefill' ? 'Highlands Coffee Hàm Nghi' : ''}
                  className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>
            </div>

            {/* Ghi chú */}
            <div className="space-y-1.5">
              <label className="font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Ghi chú giao dịch' : 'Description'}
              </label>
              <input
                type="text"
                placeholder={locale === 'vi-VN' ? 'Ví dụ: Gặp đối tác bàn công việc' : 'e.g. Lunch with client'}
                value={testNote}
                onChange={(e) => setTestNote(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="px-5 py-2 rounded-xl text-xs font-bold bg-rose-600 hover:bg-rose-700 text-white shadow-xs cursor-pointer"
            >
              {locale === 'vi-VN' ? 'Lưu khoản chi' : 'Save Expense'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 3C. ADD INCOME MODAL (Authentic full form with inputs, select category, select wallet, date, notes)
  if (popupId === 'tx-add-income' || popupId === 'dash-income') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="h-9 w-9 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold">
                +
              </div>
              <div>
                <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Thêm giao dịch thu nhập' : 'Add Income Transaction'}
                </h3>
                <p className="text-[11px] text-slate-400">
                  {locale === 'vi-VN' ? 'Ghi nhận dòng tiền thu vào ví' : 'Record incoming cashflow'}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="space-y-4 text-xs">
            {/* Số tiền */}
            <div className="space-y-1.5">
              <label className="font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Số tiền nhận được (VND) *' : 'Income Amount (VND) *'}
              </label>
              <div className="relative">
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: 25000000' : 'e.g. 25000000'}
                  value={testAmount || (stateMode === 'prefill' ? '25000000' : '')}
                  onChange={(e) => setTestAmount(e.target.value)}
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold placeholder:font-normal focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">
                  VND
                </span>
              </div>
            </div>

            {/* Danh mục & Ví nhận */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Nguồn thu nhập' : 'Income Source'}
                </label>
                <select
                  value={testCategory}
                  onChange={(e) => setTestCategory(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer"
                >
                  <option value="Lương cố định">Lương hàng tháng</option>
                  <option value="Thưởng dự án">Thưởng KPI / Dự án</option>
                  <option value="Lợi nhuận đầu tư">Cổ tức & Đầu tư</option>
                  <option value="Thu nhập phụ">Làm thêm Freelance</option>
                  <option value="Được tặng / Khác">Quà tặng / Thu khác</option>
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Ví thụ hưởng' : 'Destination Wallet'}
                </label>
                <select
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer"
                >
                  <option value="w1">Ví Chi Tiêu Hàng Ngày (12.450.000 ₫)</option>
                  <option value="w2">Ví Tiết Kiệm (50.000.000 ₫)</option>
                  <option value="w3">Tài khoản Ngân hàng Vietcombank (8.200.000 ₫)</option>
                </select>
              </div>
            </div>

            {/* Ngày giờ & Nguồn chi trả */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Ngày & Giờ nhận *' : 'Received Date & Time *'}
                </label>
                <input
                  type="datetime-local"
                  defaultValue="2026-10-13T11:25"
                  className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Đối tác chi trả' : 'Payer'}
                </label>
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: Công ty TNHH ABC' : 'e.g. Acme Corp'}
                  defaultValue={stateMode === 'prefill' ? 'Công ty Cổ phần Công nghệ' : ''}
                  className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>
            </div>

            {/* Ghi chú */}
            <div className="space-y-1.5">
              <label className="font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Ghi chú nguồn thu' : 'Description'}
              </label>
              <input
                type="text"
                placeholder={locale === 'vi-VN' ? 'Ví dụ: Lương tháng 10/2026' : 'e.g. October 2026 Salary'}
                value={testNote}
                onChange={(e) => setTestNote(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="px-5 py-2 rounded-xl text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs cursor-pointer"
            >
              {locale === 'vi-VN' ? 'Ghi nhận Thu nhập' : 'Record Income'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4. PRESENTATIONAL ADAPTERS (Built strictly on original SpendWise markup & tokens)
  // 4A. WALLET CREATE / EDIT / CLOSE
  if (popupId === 'wallet-create' || popupId === 'wallet-edit') {
    const isEdit = popupId === 'wallet-edit';
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold">
                <Wallet className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isEdit
                  ? locale === 'vi-VN' ? 'Chỉnh sửa ví nội bộ (Prefilled)' : 'Edit Internal Wallet'
                  : locale === 'vi-VN' ? 'Thêm ví nội bộ mới' : 'Create Internal Wallet'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Tên ví' : 'Wallet Name'} <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={testName}
                onChange={(e) => setTestName(e.target.value)}
                placeholder="Ví dụ: Ví Tiền Mặt, Quỹ Tiết Kiệm..."
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs focus:ring-2 focus:ring-emerald-500 outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Số dư khởi tạo (VND)' : 'Initial Balance (VND)'}
              </label>
              <input
                type="text"
                disabled={isEdit}
                value={testAmount}
                onChange={(e) => setTestAmount(e.target.value)}
                placeholder="0"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs disabled:opacity-50"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs">
              {isEdit ? (locale === 'vi-VN' ? 'Lưu thay đổi' : 'Save Changes') : (locale === 'vi-VN' ? 'Tạo ví mới' : 'Create Wallet')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (popupId === 'wallet-close') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0">
              <AlertCircle className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Xác nhận đóng ví nội bộ' : 'Close Wallet Confirmation'}
              </h3>
              <p className="text-xs text-slate-400">{locale === 'vi-VN' ? 'Hành động này không thể hoàn tác' : 'This action is irreversible'}</p>
            </div>
          </div>

          <div className="p-3.5 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200/80 dark:border-amber-900 text-xs text-amber-800 dark:text-amber-300 space-y-1">
            <p className="font-semibold">{locale === 'vi-VN' ? 'Lưu ý kiểm tra số dư ví:' : 'Balance Requirement:'}</p>
            <p>{locale === 'vi-VN' ? 'Ví phải có số dư bằng 0đ trước khi đóng. Toàn bộ tiền cần được chuyển sang ví khác.' : 'Wallet balance must be 0 VND prior to closure.'}</p>
          </div>

          <div className="flex items-center justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Quay lại' : 'Back'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-rose-600 hover:bg-rose-700 text-white shadow-xs">
              {locale === 'vi-VN' ? 'Xác nhận Đóng ví' : 'Confirm Close Wallet'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4B. BUDGET CREATE / EDIT / DELETE
  if (popupId === 'budget-create' || popupId === 'budget-edit') {
    const isEdit = popupId === 'budget-edit';
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold">
                <PieChart className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isEdit
                  ? locale === 'vi-VN' ? 'Chỉnh sửa ngân sách (Prefilled)' : 'Edit Budget'
                  : locale === 'vi-VN' ? 'Tạo ngân sách chi tiêu' : 'Create Budget'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Danh mục chi tiêu' : 'Expense Category'}
              </label>
              <input
                type="text"
                value={testCategory}
                onChange={(e) => setTestCategory(e.target.value)}
                placeholder="Chọn danh mục..."
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Hạn mức ngân sách (VND)' : 'Budget Limit (VND)'}
              </label>
              <input
                type="text"
                value={testAmount}
                onChange={(e) => setTestAmount(e.target.value)}
                placeholder="Ví dụ: 5.000.000"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs">
              {isEdit ? (locale === 'vi-VN' ? 'Lưu thay đổi' : 'Save Changes') : (locale === 'vi-VN' ? 'Tạo ngân sách' : 'Create Budget')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (popupId === 'budget-delete') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95 duration-150">
          <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 flex items-center justify-center">
            <AlertCircle className="h-5 w-5" />
          </div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Xác nhận xóa ngân sách này?' : 'Delete Budget Confirmation?'}
          </h3>
          <p className="text-xs text-slate-500">
            {locale === 'vi-VN'
              ? 'Ngân sách cho danh mục "Ăn uống & Cà phê" trong tháng 10/2026 sẽ bị gỡ bỏ.'
              : 'Budget ceiling for Food & Drinks in 10/2026 will be deleted.'}
          </p>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-rose-600 text-white">
              {locale === 'vi-VN' ? 'Xác nhận Xóa' : 'Confirm Delete'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4C. GOALS CREATE / EDIT / CONTRIBUTE / DELETE
  if (popupId === 'goal-create' || popupId === 'goal-edit') {
    const isEdit = popupId === 'goal-edit';
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold">
                <Target className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isEdit
                  ? locale === 'vi-VN' ? 'Chỉnh sửa mục tiêu (Prefilled)' : 'Edit Goal'
                  : locale === 'vi-VN' ? 'Tạo mục tiêu tích lũy mới' : 'Create New Goal'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Tên mục tiêu' : 'Goal Name'}
              </label>
              <input
                type="text"
                value={testName || (isEdit ? 'Quỹ Mua Laptop Gaming 2026' : '')}
                onChange={(e) => setTestName(e.target.value)}
                placeholder="Ví dụ: Mua Macbook, Đi du lịch..."
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Số tiền mục tiêu (VND)' : 'Target Amount (VND)'}
              </label>
              <input
                type="text"
                value={testAmount || (isEdit ? '35000000' : '')}
                onChange={(e) => setTestAmount(e.target.value)}
                placeholder="30.000.000"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white text-xs"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs">
              {isEdit ? (locale === 'vi-VN' ? 'Lưu thay đổi' : 'Save Changes') : (locale === 'vi-VN' ? 'Tạo mục tiêu' : 'Create Goal')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4C2. GOAL CONTRIBUTE (Nạp tiền tích lũy vào mục tiêu)
  if (popupId === 'goal-contribute') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 flex items-center justify-center font-bold">
                <Target className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Nạp tiền vào mục tiêu tích lũy' : 'Contribute to Goal'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="space-y-4 text-xs">
            <div>
              <label className="block font-bold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Ví nguồn trích tiền *' : 'Source Wallet *'}
              </label>
              <select className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none">
                <option>Ví Chi Tiêu Hàng Ngày (Số dư: 12.450.000 ₫)</option>
                <option>Ví Chính Vietcombank (Số dư: 8.200.000 ₫)</option>
              </select>
            </div>

            <div>
              <label className="block font-bold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Số tiền nạp vào hũ (VND) *' : 'Contribution Amount (VND) *'}
              </label>
              <input
                type="text"
                placeholder="Ví dụ: 2000000"
                defaultValue="2000000"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
              />
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs">
              {locale === 'vi-VN' ? 'Xác nhận Nạp' : 'Confirm'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4C3. GOAL WITHDRAW (Rút tiền từ mục tiêu về ví)
  if (popupId === 'goal-withdraw') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-amber-100 dark:bg-amber-950/60 text-amber-600 flex items-center justify-center font-bold">
                <Target className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Rút tiền từ mục tiêu về ví' : 'Withdraw from Goal'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="space-y-4 text-xs">
            <div>
              <label className="block font-bold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Ví nhận tiền hoàn về *' : 'Destination Wallet *'}
              </label>
              <select className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium focus:outline-none">
                <option>Ví Chi Tiêu Hàng Ngày</option>
                <option>Ví Chính Vietcombank</option>
              </select>
            </div>

            <div>
              <label className="block font-bold text-slate-700 dark:text-slate-300 mb-1">
                {locale === 'vi-VN' ? 'Số tiền rút (VND) *' : 'Withdraw Amount (VND) *'}
              </label>
              <input
                type="text"
                placeholder="Ví dụ: 1000000"
                defaultValue="1000000"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
              />
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-bold bg-amber-600 hover:bg-amber-700 text-white shadow-xs">
              {locale === 'vi-VN' ? 'Xác nhận Rút' : 'Withdraw'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4D. TRANSACTIONS DETAIL DRAWER
  if (popupId === 'tx-detail-drawer') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 flex items-center justify-center font-bold">
                <Receipt className="h-4 w-4" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Chi tiết giao dịch đối soát' : 'Transaction Audit Detail'}
              </h3>
            </div>
            <button onClick={onClose} className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 space-y-2 text-xs border border-slate-100 dark:border-slate-800">
            <div className="flex justify-between">
              <span className="text-slate-500">Mô tả:</span>
              <span className="font-bold text-slate-800 dark:text-slate-200">Thanh toán vé máy bay Vietnam Airlines</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Số tiền:</span>
              <span className="font-bold text-rose-600 tabular-nums">- 2.450.000 ₫</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Ví trích nợ:</span>
              <span className="font-semibold text-slate-700 dark:text-slate-300">Ví Chính (Vietcombank)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Thời gian:</span>
              <span className="text-slate-600 dark:text-slate-400">12/10/2026 14:30:25</span>
            </div>
          </div>

          <div className="flex justify-end pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-slate-900 dark:bg-slate-100 text-white dark:text-slate-900">
              {locale === 'vi-VN' ? 'Đóng chi tiết' : 'Close Details'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 4E. TRANSFER REVIEW MODAL
  if (popupId === 'transfer-review-modal') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
          <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 flex items-center justify-center">
              <Send className="h-4 w-4" />
            </div>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Xác nhận thông tin chuyển tiền' : 'Confirm Transfer Details'}
            </h3>
          </div>

          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 space-y-2 text-xs">
            <div className="flex justify-between">
              <span className="text-slate-500">Người nhận:</span>
              <span className="font-bold text-slate-900 dark:text-white">Nguyễn Văn An (MBBank)</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Số tài khoản:</span>
              <span className="font-mono font-bold text-slate-800 dark:text-slate-200">03892817291</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Số tiền:</span>
              <span className="font-bold text-emerald-600 text-sm tabular-nums">1.500.000 ₫</span>
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">
              {locale === 'vi-VN' ? 'Hủy bỏ' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 text-white">
              {locale === 'vi-VN' ? 'Tiếp tục nhập PIN' : 'Proceed to PIN'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 5. FRIENDS POPUPS (Reusing exact production components with mock targets)
  if (popupId === 'friend-add') {
    return (
      <AddFriendModal
        isOpen={true}
        onClose={onClose}
        targetUser={{
          id: 'mock-target-user',
          name: stateMode === 'prefill' ? 'Bùi Đức Hải' : 'Người dùng mới',
          phone: stateMode === 'prefill' ? '0988776655' : '',
        }}
        onSuccess={() => onClose()}
      />
    );
  }

  if (popupId === 'friend-edit-nickname') {
    return (
      <EditNicknameModal
        isOpen={true}
        onClose={onClose}
        friend={{
          userId: 'mock-friend-id',
          name: 'Nguyễn Văn An',
          phone: '0912***678',
          nickname: 'An Coder Du Lịch',
          isFavorite: true,
        } as any}
        currentUserId="user-me"
        onSuccess={() => onClose()}
      />
    );
  }

  if (popupId === 'friend-unfriend') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 flex items-center justify-center">
            <AlertCircle className="h-5 w-5" />
          </div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Xác nhận Hủy kết bạn với Nguyễn Văn An?' : 'Confirm Unfriend Contact?'}
          </h3>
          <p className="text-xs text-slate-500 leading-relaxed">
            {locale === 'vi-VN'
              ? 'Người này sẽ không còn hiển thị trong danh bạ bạn bè nhanh, nhưng toàn bộ lịch sử hóa đơn chung đã kết toán vẫn được bảo lưu.'
              : 'Contact will be removed from quick list while past shared expenses are safely retained.'}
          </p>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">
              {locale === 'vi-VN' ? 'Hủy bỏ' : 'Cancel'}
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-rose-600 text-white">
              {locale === 'vi-VN' ? 'Xác nhận Hủy bạn' : 'Unfriend'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 6. NOTIFICATIONS POPUPS
  if (popupId === 'notif-account-pref') {
    return (
      <AccountPrefModal
        isOpen={true}
        onClose={onClose}
        initialAccountId="w-main"
        onSavedToast={() => onClose()}
      />
    );
  }

  if (popupId === 'notif-detail') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">Chi tiết thông báo giao dịch</h3>
            <button onClick={onClose} className="p-1 rounded-lg text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
          </div>
          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 text-xs space-y-2">
            <div className="flex justify-between"><span className="text-slate-500">Tiêu đề:</span><span className="font-bold text-slate-900 dark:text-white">Biến động số dư: +2.500.000 ₫</span></div>
            <div className="flex justify-between"><span className="text-slate-500">Mã giao dịch:</span><span className="font-mono font-bold">TX-VCB-883921</span></div>
            <div className="flex justify-between"><span className="text-slate-500">Nội dung:</span><span>Trả tiền ăn tối gia đình</span></div>
            <div className="flex justify-between"><span className="text-slate-500">Thời gian:</span><span className="font-mono text-slate-400">12/10/2026 18:45</span></div>
          </div>
          <div className="flex justify-end pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-slate-900 dark:bg-white text-white dark:text-slate-900">
              Đóng
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 7. SUPPORT & DISPUTES POPUPS
  if (popupId === 'support-create-case') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">Gửi phiếu yêu cầu hỗ trợ kỹ thuật</h3>
            <button onClick={onClose} className="p-1 rounded-lg text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
          </div>
          <div className="space-y-3 text-xs">
            <div>
              <label className="font-bold block mb-1">Tiêu đề yêu cầu *</label>
              <input type="text" defaultValue="Lỗi đồng bộ webhook SePay" className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 font-medium" />
            </div>
            <div>
              <label className="font-bold block mb-1">Phân loại sự cố</label>
              <select className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                <option>Lỗi hệ thống & Kết nối</option>
                <option>Thắc mắc tính năng & Báo cáo</option>
                <option>Bảo mật & Tài khoản</option>
              </select>
            </div>
            <div>
              <label className="font-bold block mb-1">Mô tả chi tiết</label>
              <textarea rows={3} placeholder="Mô tả cụ thể vấn đề bạn gặp phải..." className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 resize-none" />
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">Hủy</button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-bold bg-emerald-600 text-white shadow-xs">Gửi yêu cầu</button>
          </div>
        </div>
      </div>
    );
  }

  if (popupId === 'support-create-dispute') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">Tạo khiếu nại giao dịch đối soát</h3>
            <button onClick={onClose} className="p-1 rounded-lg text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
          </div>
          <div className="space-y-3 text-xs">
            <div>
              <label className="font-bold block mb-1">Mã giao dịch cần khiếu nại *</label>
              <input type="text" defaultValue="TX-VCB-883921" className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 font-mono font-bold" />
            </div>
            <div>
              <label className="font-bold block mb-1">Lý do khiếu nại</label>
              <select className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                <option>Bị trừ tiền 2 lần cho 1 đơn hàng</option>
                <option>Giao dịch không nhận diện được người thụ hưởng</option>
                <option>Số tiền bị trừ sai lệch so với hóa đơn</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">Hủy</button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-bold bg-rose-600 text-white shadow-xs">Mở khiếu nại</button>
          </div>
        </div>
      </div>
    );
  }

  // 8. SETTINGS POPUPS
  if (popupId === 'settings-edit-profile') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">Chỉnh sửa thông tin cá nhân</h3>
            <button onClick={onClose} className="p-1 rounded-lg text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
          </div>
          <div className="space-y-3 text-xs">
            <div>
              <label className="font-bold block mb-1">Họ và tên hiển thị *</label>
              <input type="text" defaultValue="Người dùng SpendWise" className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 font-bold" />
            </div>
            <div>
              <label className="font-bold block mb-1">Số điện thoại liên hệ</label>
              <input type="text" defaultValue="0912345678" className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 font-mono" />
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">Hủy</button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-bold bg-emerald-600 text-white shadow-xs">Lưu thay đổi</button>
          </div>
        </div>
      </div>
    );
  }

  if (popupId === 'settings-logout-confirm') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
        <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95">
          <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 flex items-center justify-center">
            <AlertCircle className="h-5 w-5" />
          </div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            Xác nhận Đăng xuất khỏi SpendWise?
          </h3>
          <p className="text-xs text-slate-500 leading-relaxed">
            Phiên làm việc trên trình duyệt này sẽ kết thúc. Bạn sẽ cần nhập lại thông tin mật khẩu hoặc mã PIN để truy cập lại.
          </p>
          <div className="flex justify-end gap-2 pt-2">
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700">
              Ở lại
            </button>
            <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-rose-600 text-white">
              Xác nhận Đăng xuất
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Fallback dialog for other catalog items
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4 animate-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Giao diện Popup Preview' : 'Popup Preview Presentation'}
          </h3>
          <button onClick={onClose} className="p-1 rounded-lg text-slate-400 hover:text-slate-600">
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="text-xs text-slate-500">
          {locale === 'vi-VN'
            ? `Đang xem giao diện: ${popupId}. Chế độ an toàn DEV Preview đang hoạt động.`
            : `Inspecting popup: ${popupId}. Safe DEV preview mode active.`}
        </p>
        <div className="flex justify-end pt-2">
          <button onClick={onClose} className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 text-white">
            {locale === 'vi-VN' ? 'Đóng popup' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};
