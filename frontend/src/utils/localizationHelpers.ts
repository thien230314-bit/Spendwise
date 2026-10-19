import { Transaction, BudgetCategoryItem, SupportedLocale, NotificationItem } from '../types';
import { GroupActivity } from '../types/groups';
import { formatMoney } from './money';

export const getLocalizedTxTitle = (tx: Transaction, locale: SupportedLocale): string => {
  if (locale === 'en-US' && tx.titleEn) {
    return tx.titleEn;
  }
  const raw = tx.title || '';
  if (raw === 'Khoản thu nhập' || raw === 'Income') {
    return locale === 'vi-VN' ? 'Khoản thu nhập' : 'Income';
  }
  if (raw === 'Khoản chi tiêu' || raw === 'Expense') {
    return locale === 'vi-VN' ? 'Khoản chi tiêu' : 'Expense';
  }
  return tx.title;
};

export const getLocalizedTxDesc = (tx: Transaction, locale: SupportedLocale): string => {
  if (locale === 'en-US' && tx.descriptionEn) {
    return tx.descriptionEn;
  }
  const raw = tx.description || '';
  if (raw === 'Khoản thu nhập' || raw === 'Thu nhập bổ sung' || raw === 'Income') {
    return locale === 'vi-VN' ? 'Khoản thu nhập' : 'Income';
  }
  if (raw === 'Khoản chi tiêu' || raw === 'Chi tiêu' || raw === 'Expense') {
    return locale === 'vi-VN' ? 'Khoản chi tiêu' : 'Expense';
  }
  return tx.description || '';
};

export const getLocalizedTxCounterparty = (tx: Transaction, locale: SupportedLocale): string => {
  if (locale === 'en-US' && tx.counterpartyEn) {
    return tx.counterpartyEn;
  }
  const raw = tx.counterparty || '';
  if (raw === 'Khoản thu nhập' || raw === 'Income') {
    return locale === 'vi-VN' ? 'Khoản thu nhập' : 'Income';
  }
  if (raw === 'Khoản chi tiêu' || raw === 'Expense') {
    return locale === 'vi-VN' ? 'Khoản chi tiêu' : 'Expense';
  }
  return tx.counterparty || '';
};

export const getLocalizedWalletName = (
  walletId?: string,
  walletName?: string,
  locale?: SupportedLocale
): string => {
  const isEn = locale === 'en-US';

  // If a custom wallet name is provided, check if it's one of the standard default names that need localization
  if (walletName) {
    if (walletName === 'Ví chính' || walletName === 'Main Wallet') {
      return isEn ? 'Main Wallet' : 'Ví chính';
    }
    if (walletName === 'Ví chính (Tiêu dùng)' || walletName === 'Main Wallet (Daily)') {
      return isEn ? 'Main Wallet (Daily)' : 'Ví chính (Tiêu dùng)';
    }
    if (
      walletName === 'Ví tiết kiệm' ||
      walletName === 'Savings' ||
      walletName === 'Savings Vault'
    ) {
      return isEn ? 'Savings Vault' : 'Ví tiết kiệm';
    }
    if (
      walletName === 'Quỹ dự phòng' ||
      walletName === 'Quỹ khẩn cấp' ||
      walletName === 'Emergency Fund'
    ) {
      return isEn ? 'Emergency Fund' : 'Quỹ dự phòng';
    }
    return walletName;
  }

  if (!walletId && !walletName) {
    return isEn ? 'Bank Sync / External' : 'Đồng bộ ngân hàng';
  }

  if (walletId === 'w-main') {
    return isEn ? 'Main Wallet (Daily)' : 'Ví chính (Tiêu dùng)';
  }
  if (walletId === 'w-savings') {
    return isEn ? 'Savings Vault' : 'Ví tiết kiệm';
  }
  if (walletId === 'w-emergency') {
    return isEn ? 'Emergency Fund' : 'Quỹ dự phòng';
  }

  return isEn ? 'Main Wallet (Daily)' : 'Ví chính (Tiêu dùng)';
};

export const getLocalizedCategoryName = (
  cat: string,
  t: (key: string) => string,
  categories?: { id: string; key: string; name: string; isSystem?: boolean }[]
): string => {
  if (!cat) return '';

  // 1. If categories list is provided, try resolving stable id / key / name
  if (categories && categories.length > 0) {
    const matched = categories.find((c) => c.key === cat || c.id === cat || c.name === cat);
    if (matched) {
      if (matched.key.startsWith('categories.')) {
        return t(matched.key);
      }
      return matched.name;
    }
  }

  // 2. Direct translation key
  if (cat.startsWith('categories.')) {
    return t(cat);
  }

  // 3. Known system category names or IDs
  switch (cat) {
    case 'food':
    case 'cat_exp_01':
    case 'Ăn uống':
    case 'Ăn uống & Cà phê':
    case 'Food & Dining':
      return t('categories.food');
    case 'transport':
    case 'cat_exp_03':
    case 'Đi lại':
    case 'Đi lại & Xăng xe':
    case 'Transport':
    case 'Transportation':
      return t('categories.transport');
    case 'shopping':
    case 'cat_exp_02':
    case 'Mua sắm':
    case 'Mua sắm & Tiêu dùng':
    case 'Shopping':
      return t('categories.shopping');
    case 'entertainment':
    case 'cat_exp_04':
    case 'Giải trí':
    case 'Giải trí & Xem phim':
    case 'Entertainment':
      return t('categories.entertainment');
    case 'utilities':
    case 'cat_exp_05':
    case 'Hóa đơn':
    case 'Hóa đơn & Nhà ở':
    case 'Bills & Utilities':
      return t('categories.utilities');
    case 'education':
    case 'cat_exp_06':
    case 'Giáo dục & Học tập':
    case 'Education & Learning':
    case 'Education':
      return t('categories.education');
    case 'savings_goals':
    case 'cat_exp_07':
    case 'Mục tiêu tiết kiệm':
    case 'Savings Goals':
      return t('categories.savings_goals');
    case 'cat_exp_08':
    case 'expense_other':
    case 'Chi tiêu khác':
    case 'Other Expenses':
      return t('categories.expense_other');
    case 'salary':
    case 'cat_inc_01':
    case 'Lương & Thưởng':
    case 'Salary & Income':
    case 'Salary':
      return t('categories.salary');
    case 'p2p':
    case 'cat_inc_02':
    case 'Chuyển tiền':
    case 'Chuyển khoản':
    case 'Direct Transfer':
      return t('categories.p2p');
    case 'investments':
    case 'cat_inc_03':
    case 'Đầu tư & Sinh lời':
    case 'Investments & Returns':
    case 'Investment & Profits':
      return t('categories.investments');
    case 'cat_inc_04':
    case 'income_other':
    case 'Thu nhập khác':
    case 'Other Income':
      return t('categories.income_other');
    case 'others':
    case 'Khác':
    case 'Other':
    case 'Others':
      return t('categories.others');
    default:
      // Custom category: return stored name directly without altering or translating
      return cat;
  }
};

export const getLocalizedBudgetName = (
  budget: BudgetCategoryItem,
  t: (key: string) => string,
  locale: SupportedLocale
): string => {
  if (locale === 'en-US') {
    if (budget.categoryKey && budget.categoryKey.startsWith('categories.')) {
      return t(budget.categoryKey);
    }
    return getLocalizedCategoryName(budget.name, t);
  }
  return budget.name || (budget.categoryKey ? t(budget.categoryKey) : 'Ngân sách');
};

export const getLocalizedGoalCategory = (
  category: string,
  t: (key: string, params?: Record<string, any>) => string,
  locale?: SupportedLocale
): string => {
  switch (category) {
    case 'Mua sắm thiết bị':
    case 'Tech & Equipment':
    case 'categories.shopping':
      return t('goals.categoryEquipment');
    case 'Du lịch & Trải nghiệm':
    case 'Travel & Vacation':
      return t('goals.categoryTravel');
    case 'Quỹ khẩn cấp cá nhân':
    case 'Emergency Fund':
      return t('goals.categoryEmergency');
    case 'Đầu tư & Tương lai':
    case 'Investment & Future':
      return t('goals.categoryInvestment');
    case 'Phương tiện đi lại (Xe máy, Ô tô)':
    case 'Phương tiện':
    case 'Xe cộ & Đi lại':
    case 'Transportation':
      return t('goals.categoryVehicle');
    case 'Học tập & Giáo dục':
    case 'Giáo dục & Khóa học':
    case 'Giáo dục':
    case 'Education':
      return t('goals.categoryEducation');
    default:
      return category;
  }
};

/**
 * Normalizes and localizes notification account labels across system, security, support, wallets, and groups.
 * Backward compatible with old persisted strings.
 */
export const getLocalizedAccountDisplayName = (
  accountId?: string,
  accountName?: string,
  locale: SupportedLocale = 'vi-VN',
  accountType?: string
): string => {
  const isEn = locale === 'en-US';

  // Disputes & Support
  if (
    accountId === 'system-support' ||
    accountName === 'Khiếu nại & Tra soát' ||
    accountName === 'Disputes & Support'
  ) {
    return isEn ? 'Disputes & Support' : 'Khiếu nại & Tra soát';
  }

  // Security & Login
  if (
    accountId === 'security' ||
    accountType === 'security' ||
    accountName === 'Bảo mật & Đăng nhập' ||
    accountName === 'Security & Login' ||
    accountName === 'Khôi phục tài khoản' ||
    accountName === 'Account Recovery'
  ) {
    return isEn ? 'Security & Login' : 'Bảo mật & Đăng nhập';
  }

  // Risk & Fraud
  if (
    accountId === 'system-risk' ||
    accountName === 'Cảnh báo Rủi ro & Gian lận' ||
    accountName === 'Risk & Fraud Alerts'
  ) {
    return isEn ? 'Risk & Fraud Alerts' : 'Cảnh báo Rủi ro & Gian lận';
  }

  // Reconciliation & Audit
  if (
    accountId === 'system-audit' ||
    accountName === 'Đối soát & Kiểm toán' ||
    accountName === 'Reconciliation & Audit'
  ) {
    return isEn ? 'Reconciliation & Audit' : 'Đối soát & Kiểm toán';
  }

  // System Announcements
  if (
    accountId === 'system' ||
    accountId === 'system-announcement' ||
    accountType === 'system' ||
    accountName === 'Thông báo từ hệ thống' ||
    accountName === 'System announcements' ||
    accountName === 'System'
  ) {
    return isEn ? 'System announcements' : 'Thông báo từ hệ thống';
  }

  // Budgets
  if (
    accountId === 'budget' ||
    accountType === 'budget' ||
    accountName === 'Ngân sách' ||
    accountName === 'Budgets' ||
    accountName === 'Budget'
  ) {
    return isEn ? 'Budgets' : 'Ngân sách';
  }

  // Recurring Transactions
  if (
    accountId === 'recurring' ||
    accountType === 'recurring' ||
    accountName === 'Giao dịch định kỳ' ||
    accountName === 'Recurring' ||
    accountName === 'Subscriptions'
  ) {
    return isEn ? 'Recurring' : 'Giao dịch định kỳ';
  }

  // Friends
  if (
    accountId === 'friend' ||
    accountType === 'friend' ||
    accountName === 'Bạn bè' ||
    accountName === 'Friends'
  ) {
    return isEn ? 'Friends' : 'Bạn bè';
  }

  // Groups
  if (accountId === 'group-all') {
    return isEn ? 'Groups & Shared Funds' : 'Nhóm & Quỹ chung';
  }

  // All
  if (accountId === 'all') {
    return isEn ? 'All Accounts' : 'Tất cả tài khoản';
  }

  // Default to wallet localization helper, or preserve custom user-authored names
  return getLocalizedWalletName(accountId, accountName, locale);
};

/**
 * Format notification timestamp and relative time based on active locale.
 * Backward-compatible with legacy "Vừa xong" and "Just now" strings.
 */
export const getLocalizedNotificationTime = (
  time: string,
  timestamp?: number,
  locale: SupportedLocale = 'vi-VN'
): string => {
  const isVi = locale === 'vi-VN';

  if (timestamp && typeof timestamp === 'number' && !isNaN(timestamp) && timestamp > 0) {
    const diffMs = Math.max(0, Date.now() - timestamp);
    // Under 2 minutes -> "Vừa xong" / "Just now"
    if (diffMs < 2 * 60 * 1000) {
      return isVi ? 'Vừa xong' : 'Just now';
    }

    const date = new Date(timestamp);
    const now = new Date();
    const isToday =
      date.getDate() === now.getDate() &&
      date.getMonth() === now.getMonth() &&
      date.getFullYear() === now.getFullYear();

    const timeStr = date.toLocaleTimeString(isVi ? 'vi-VN' : 'en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    });

    if (isToday) {
      return `${timeStr} • ${isVi ? 'Hôm nay' : 'Today'}`;
    }

    const dateStr = date.toLocaleDateString(isVi ? 'vi-VN' : 'en-US', {
      day: '2-digit',
      month: '2-digit',
      year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
    });
    return `${timeStr} • ${dateStr}`;
  }

  // Fallback for legacy persisted string
  if (time === 'Vừa xong' || time === 'Just now') {
    return isVi ? 'Vừa xong' : 'Just now';
  }

  return time;
};

const LEGACY_ACTION_LABEL_MAP: Record<string, string> = {
  'Xem chi tiết': 'common.viewDetails',
  'View details': 'common.viewDetails',
  'Xem tiến độ': 'notifications.actionViewProgress',
  'View progress': 'notifications.actionViewProgress',
  'Đến Sổ giao dịch': 'notifications.actionOpenLedger',
  'Go to Ledger': 'notifications.actionOpenLedger',
  'Đặt lại mã PIN': 'notifications.actionResetPin',
  'Reset PIN': 'notifications.actionResetPin',
  'Thiết lập mã PIN mới': 'notifications.actionSetupPin',
  'Set up new PIN': 'notifications.actionSetupPin',
  'Thay đổi mật khẩu': 'passwordRecovery.changePassword',
  'Change password': 'passwordRecovery.changePassword',
  'Xem tiến độ khiếu nại': 'notifications.actionViewDisputeProgress',
  'View dispute progress': 'notifications.actionViewDisputeProgress',
};

/**
 * Resolves actionLabelKey to localized string, mapping legacy Vietnamese strings backwards-compatibly.
 */
export const getLocalizedActionLabel = (
  actionLabelKey: string | undefined,
  t: (key: string) => string
): string => {
  if (!actionLabelKey) return '';
  const mappedKey = LEGACY_ACTION_LABEL_MAP[actionLabelKey] || actionLabelKey;
  const translated = t(mappedKey);
  if (translated === mappedKey && !mappedKey.includes('.')) {
    return actionLabelKey;
  }
  return translated;
};

export const LEGACY_ERROR_MAP: Record<string, string> = {
  'Vui lòng đăng nhập.': 'common.loginRequired',
  'Vui lòng đăng nhập': 'common.loginRequired',
  'Vui lòng đăng nhập để tiếp tục.': 'common.loginRequired',
  'Vui lòng đăng nhập để thực hiện thao tác.': 'common.loginRequired',
  'Vui lòng đăng nhập để gửi lời mời kết bạn.': 'friends.errorLoginRequired',
  'Vui lòng đăng nhập để tạo chi phí.': 'common.loginRequired',
  'Vui lòng đăng nhập để tạo yêu cầu đóng quỹ.': 'common.loginRequired',
  'Vui lòng đăng nhập để phân loại giao dịch.': 'common.loginRequired',
  'Vui lòng đăng nhập để nhập giao dịch ngân hàng.': 'common.loginRequired',
  'Không tìm thấy nhóm.': 'groups.errorNotFound',
  'Không tìm thấy nhóm': 'groups.errorNotFound',
  'Chỉ Chủ nhóm mới có quyền thực hiện thao tác này.': 'groups.errorOwnerOnly',
  'Chỉ Chủ nhóm mới có quyền tạo đợt đóng quỹ.': 'groups.errorOwnerOnly',
  'Chỉ Chủ nhóm mới có quyền tạo yêu cầu đóng quỹ.': 'groups.errorOwnerOnly',
  'Số tiền phải lớn hơn 0': 'groups.errorInvalidAmount',
  'Số tiền chuyển phải lớn hơn 0.': 'groups.errorInvalidAmount',
  'Số tiền điều chỉnh phải lớn hơn 0.': 'transactions.errorInvalidAmount',
  'Số dư không đủ để thực hiện giao dịch.': 'wallet.errorInsufficientBalance',
  'Số dư ví nguồn không đủ.': 'wallet.errorInsufficientBalance',
  'Số dư ví không đủ để khởi tạo số tiền ban đầu cho mục tiêu.': 'goals.errorInsufficientWalletBalance',
  'Số dư ví không đủ để tích lũy mục tiêu.': 'goals.errorInsufficientWalletBalance',
  'Số dư ví không đủ để thực hiện thao tác mục tiêu.': 'goals.errorInsufficientWalletBalance',
  'Ví thanh toán không tồn tại hoặc không hợp lệ.': 'wallet.errorNotFound',
  'Không tìm thấy ví thanh toán.': 'wallet.errorNotFound',
  'Không tìm thấy ví.': 'wallet.errorNotFound',
  'Ví không tồn tại.': 'wallet.errorNotFound',
  'Chứng từ giao dịch là bắt buộc.': 'transactions.errorEvidenceRequired',
  'Lý do điều chỉnh giao dịch là bắt buộc.': 'transactions.errorCorrectionReasonRequired',
  'Lý do điều chỉnh là bắt buộc.': 'transactions.errorCorrectionReasonRequired',
  'Không tìm thấy giao dịch.': 'transactions.errorNotFound',
  'Bạn chỉ có quyền chỉnh sửa giao dịch của chính mình.': 'transactions.errorUnauthorizedCorrection',
  'Giao dịch đã dùng hết lượt chỉnh sửa. Vui lòng gửi yêu cầu hỗ trợ để được cấp thêm lượt.': 'transactions.errorNoCorrectionsLeft',
  'Không thể chỉnh sửa danh mục hệ thống.': 'categories.errorSystemImmutable',
  'Không thể xóa danh mục hệ thống.': 'categories.errorSystemImmutable',
  'Không thể chỉnh sửa hoặc xóa danh mục hệ thống.': 'categories.errorSystemImmutable',
  'Hai người đã là bạn bè.': 'friends.errorAlreadyFriends',
  'Bạn đã gửi lời mời kết bạn trước đó và đang chờ phản hồi.': 'friends.errorRequestPending',
  'Vui lòng nhập lời nhắn kết bạn.': 'friends.errorMessageRequired',
  'Không thể tự gửi lời mời kết bạn cho chính mình.': 'friends.errorSelfRequest',
  'Nhóm chưa thiết lập tài khoản ngân hàng nhận tiền.': 'groups.errorBankNotConfigured',
  'Vui lòng nhập email hoặc số điện thoại.': 'groups.errorSearchIdentifierRequired',
  'Bạn đã là thành viên của nhóm này.': 'groups.errorAlreadyMember',
  'Tài khoản người dùng này hiện không hoạt động hoặc đang bị hạn chế.': 'groups.errorUserInactive',
  'Không tìm thấy người dùng với thông tin này trong hệ thống.': 'friends.errorUserNotFound',
  'Chỉ Chủ nhóm mới có quyền mời thành viên mới.': 'groups.errorOwnerOnly',
  'Không thể chuyển quyền Chủ nhóm cho thành viên chưa kích hoạt.': 'groups.errorInactiveMemberTransfer',
  'Thành viên này đang có nghĩa vụ chưa hoàn tất trong đợt góp quỹ đang hoạt động. Không thể xóa thành viên khỏi nhóm.': 'groups.errorPendingObligationRemove',
  'Không có yêu cầu xóa nào đang chờ phản hồi.': 'groups.errorNoPendingDeletion',
  'Thao tác hoàn quỹ và xóa nhóm đang được xử lý hoặc đã hoàn tất.': 'groups.errorFinalizationInFlight',
  'Không tìm thấy nhóm cần xóa.': 'groups.errorNotFound',
  'Chỉ Chủ nhóm mới có quyền thực hiện hoàn quỹ và xóa nhóm.': 'groups.errorOwnerOnly',
  'Số tiền không hợp lệ. Phải là số nguyên dương (> 0 ₫).': 'groups.errorInvalidAmount',
  'Số dư quỹ không hợp lệ. Phải là số nguyên an toàn không âm.': 'groups.errorRefundInvalidBalance',
  'Quỹ nhóm còn số dư nhưng tổng tiền đóng góp của các thành viên bằng 0. Không thể tính toán hoàn trả theo tỷ lệ.': 'groups.errorRefundBasisZero',
  'Không thể tạo ví.': 'wallet.errorCreateFailed',
  'Không thể cập nhật ví.': 'wallet.errorUpdateFailed',
  'Không thể đóng ví.': 'wallet.errorCloseFailed',
  'Không thể đặt ví mặc định.': 'wallet.errorSetDefaultFailed',
  'Vui lòng nhập tên ví.': 'wallet.errorWalletNameRequired',
  'Không thể đóng ví cuối cùng. Người dùng phải duy trì ít nhất một ví.': 'wallet.errorCannotCloseLastWallet',

  // Categories & Rules & Recurring
  'Vui lòng nhập tên danh mục.': 'categories.errorCategoryNameRequired',
  'Không tìm thấy danh mục.': 'categories.errorCategoryNotFound',
  'Vui lòng nhập tên giao dịch định kỳ.': 'recurring.errorRecurringNameRequired',
  'Số tiền định kỳ phải lớn hơn 0.': 'recurring.errorRecurringAmountPositive',
  'Vui lòng nhập mẫu mô tả (pattern).': 'rules.errorPatternRequired',
  'Vui lòng chọn danh mục đích.': 'rules.errorTargetCategoryRequired',

  // Friends
  'Chỉ có thể kết bạn với tài khoản người dùng thông thường.': 'friends.errorOnlyStandardUsers',
  'Tài khoản của bạn hiện đang bị hạn chế, không thể gửi hoặc nhận lời mời kết bạn.': 'friends.errorUserRestricted',
  'Tài khoản người gửi lời mời không còn hoạt động hoặc đã bị hạn chế.': 'friends.errorRecipientRestricted',
  'Bạn không có quyền phản hồi lời mời này.': 'friends.errorUnauthorizedResponse',
  'Bạn không có quyền từ chối lời mời này.': 'friends.errorUnauthorizedReject',
  'Lời mời này đã được xử lý trước đó.': 'friends.errorRequestAlreadyProcessed',

  // Transactions & Evidence
  'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi tạo khoản thu thủ công.': 'transactions.errorManualIncomeEvidenceRequired',
  'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi tạo khoản chi thủ công.': 'transactions.errorManualExpenseEvidenceRequired',
  'Giao dịch tài chính đã chốt (Finalized) không thể xóa để đảm bảo tính toàn vẹn và bất biến của sổ cái kế toán.': 'transactions.errorFinalizedImmutable',
  'Quản trị viên chỉ có quyền kiểm tra và cấp lượt sửa, không thể trực tiếp sửa sổ cái tài chính.': 'transactions.errorAdminCannotMutateLedger',
  'Chỉ cho phép điều chỉnh giao dịch thu/chi thủ công.': 'transactions.errorOnlyManualAdjustable',
  'Không tìm thấy ví thanh toán nguồn của giao dịch.': 'transactions.errorSourceWalletNotFound',
  'Không tìm thấy ví thanh toán được chỉ định.': 'transactions.errorTargetWalletNotFound',
  'Số dư ví không đủ để điều chỉnh giảm số tiền thu nhập này.': 'transactions.errorInsufficientBalanceForIncomeReduction',
  'Số dư ví không đủ cho số tiền chi tiêu sau điều chỉnh.': 'transactions.errorInsufficientBalanceForExpenseIncrease',
  'Điều chỉnh giao dịch thủ công yêu cầu cung cấp lý do điều chỉnh để ghi nhận vào lịch sử kiểm toán.': 'transactions.errorAuditReasonRequired',
  'Dung lượng lưu trữ của trình duyệt đã bị đầy (QuotaExceeded). Không thể lưu chứng từ đính kèm. Giao dịch chưa được tạo.': 'transactions.errorStorageQuotaExceeded',
  'Không thể lưu trữ tệp chứng từ vào bộ nhớ trình duyệt. Giao dịch chưa được tạo.': 'transactions.errorStorageWriteFailed',
  'Giao dịch này đã được ghi nhận vào sổ giao dịch trước đó.': 'transactions.errorBankTxAlreadyImported',
  'Không tìm thấy giao dịch ngân hàng.': 'transactions.errorBankTxNotFound',
  'Giao dịch nhận tiền (credit) chỉ có thể chọn hạng mục Thu nhập.': 'transactions.errorCreditIncomeOnly',
  'Giao dịch chi tiền (debit) chỉ có thể chọn hạng mục Chi tiêu.': 'transactions.errorDebitExpenseOnly',
  'Danh mục này đã có ngân sách trong tháng được chọn.': 'transactions.errorBudgetMonthExists',
  'Công cụ mô phỏng webhook chỉ khả dụng trong môi trường phát triển (DEV).': 'transactions.errorSimulateDevOnly',
  'Không tìm thấy sự kiện Webhook đã xử lý nào để mô phỏng gửi trùng lặp.': 'transactions.errorSimulateNoEvents',
  'Không tìm thấy liên kết ngân hàng (BankConnection) đang ở trạng thái Active nào để mô phỏng nhận giao dịch. Vui lòng liên kết tài khoản ngân hàng trước.': 'transactions.errorSimulateNoBankConnection',
  'Bạn chỉ có quyền phân loại giao dịch của chính mình.': 'transactions.errorOnlyOwnTransactions',

  // Goals
  'Số tiền khởi tạo không thể âm.': 'goals.errorInitialAmountNegative',
  'Số tiền khởi tạo không thể vượt quá số tiền mục tiêu.': 'goals.errorInitialAmountExceedsTarget',
  'Không tìm thấy ví nguồn đã chọn.': 'goals.errorSourceWalletNotFound',
  'Vui lòng chọn ví nguồn cho số tiền khởi tạo.': 'goals.errorSourceWalletRequiredForSeed',
  'Không tìm thấy mục tiêu.': 'goals.errorGoalNotFound',
  'Mục tiêu đang có số dư tích lũy. Vui lòng rút tiền về ví trước khi xóa.': 'goals.errorHasSavingsBeforeDelete',
  'Mục tiêu đã có lịch sử giao dịch và không thể xóa trực tiếp. Vui lòng sử dụng tính năng Hủy mục tiêu.': 'goals.errorHasHistoryUseCancel',
  'Vui lòng chọn ví để tích lũy.': 'goals.errorSelectWalletToContribute',
  'Vui lòng chọn ví nhận tiền.': 'goals.errorSelectWalletToWithdraw',
  'Không tìm thấy ví nhận đã chọn.': 'goals.errorDestWalletNotFound',
  'Số tiền rút vượt quá số dư hiện có của mục tiêu.': 'goals.errorWithdrawExceedsGoal',
  'Ví nguồn và ví đích không được trùng nhau.': 'goals.errorWalletsMustDiffer',

  // Groups
  'Nhóm đang ở chế độ chỉ đọc do có yêu cầu giải tán đang chờ xử lý.': 'groups.errorGroupFrozen',
  'Thành viên này hiện đã là Chủ nhóm.': 'groups.errorAlreadyOwner',
  'Thông tin tìm kiếm phải là email hợp lệ hoặc số điện thoại Việt Nam gồm đúng 10 chữ số (bắt đầu bằng 0, không chứa khoảng trắng hay ký tự đặc biệt).': 'groups.errorInvalidPhoneOrEmail',
  'Chỉ có thể mời người dùng đã là bạn bè vào nhóm.': 'groups.errorMustBeFriendToInvite',
  'Thành viên này đã có trong nhóm hoặc đang chờ phản hồi.': 'groups.errorMemberAlreadyInGroupOrPending',
  'Không tìm thấy thành viên.': 'groups.errorMemberNotFound',
  'Không thể xóa Chủ nhóm. Vui lòng chuyển quyền Chủ nhóm cho thành viên khác trước.': 'groups.errorCannotRemoveOwner',
  'Thành viên này còn yêu cầu đóng quỹ đang chờ xử lý. Hãy hoàn tất hoặc hủy yêu cầu trước khi xóa.': 'groups.errorPendingFundRequestRemove',
  'Chỉ thành viên hoạt động mới có thể đóng góp quỹ.': 'groups.errorActiveMembersOnlyContribute',
  'Tài khoản ngân hàng nhận tiền không thuộc về Chủ nhóm hiện tại.': 'groups.errorBankNotOwner',
  'Mã nhóm trong thông tin thanh toán không khớp với nhóm hiện tại.': 'groups.errorGroupIdMismatch',
  'Ngữ cảnh thanh toán đóng góp quỹ không hợp lệ.': 'groups.errorInvalidPaymentContext',
  'Ngữ cảnh thanh toán đợt đóng quỹ không khớp hoặc không hợp lệ.': 'groups.errorInvalidRoundPaymentContext',
  'Thông tin yêu cầu đóng quỹ trong lệnh thanh toán không khớp.': 'groups.errorFundRequestMismatch',
  'Thông tin người gửi không khớp với tài khoản hiện tại.': 'groups.errorSenderMismatch',
  'Người nhận không khớp với Chủ nhóm thụ hưởng.': 'groups.errorRecipientMismatch',
  'Tài khoản ngân hàng nhận không khớp với tài khoản đã cấu hình.': 'groups.errorBankMismatch',
  'Số tiền thanh toán phải là số nguyên dương hợp lệ.': 'groups.errorInvalidPaymentAmount',
  'Lệnh chuyển tiền chưa được xác nhận hoàn tất.': 'groups.errorPaymentNotConfirmed',
  'Không tìm thấy yêu cầu đóng quỹ.': 'groups.errorFundRequestNotFound',
  'Số tiền thanh toán không khớp với số tiền của yêu cầu.': 'groups.errorFundRequestAmountMismatch',
  'Bạn không nằm trong danh sách yêu cầu đóng quỹ này.': 'groups.errorNotInRecipientList',
  'Yêu cầu đóng quỹ này đã được thanh toán trước đó.': 'groups.errorFundRequestAlreadyPaid',
  'Yêu cầu đóng quỹ đã quá hạn thanh toán.': 'groups.errorFundRequestExpired',
  'Vui lòng nhập tên đợt góp quỹ.': 'groups.errorFundRoundTitleRequired',
  'Tổng quỹ cần thu phải là số nguyên dương hợp lệ (> 0 ₫).': 'groups.errorFundRoundTargetInvalid',
  'Nhóm chưa có thành viên hoạt động để tham gia đợt đóng quỹ.': 'groups.errorNoActiveMembersForRound',
  'Không tìm thấy đợt góp quỹ.': 'groups.errorFundRoundNotFound',
  'Bạn không có nghĩa vụ đóng trong đợt quỹ này.': 'groups.errorNoObligationInRound',
  'Đợt góp quỹ đã kết thúc hoặc không còn hoạt động. Không thể tiếp nhận thêm thanh toán.': 'groups.errorFundRoundEnded',
  'Bạn đã hoàn tất phần đóng quỹ của mình trong đợt này.': 'groups.errorFundRoundAlreadyCompletedByUser',
  'Số tiền yêu cầu phải là số nguyên lớn hơn 0.': 'groups.errorFundRequestAmountPositive',
  'Mục đích thu quỹ không được để trống.': 'groups.errorFundPurposeRequired',
  'Ngày hết hạn không hợp lệ.': 'groups.errorInvalidDueDate',
  'Hạn đóng quỹ phải là hôm nay hoặc một ngày trong tương lai.': 'groups.errorDueDateMustBeFuture',
  'Vui lòng chọn ít nhất một thành viên nhận yêu cầu.': 'groups.errorSelectAtLeastOneRecipient',
  'Chủ nhóm là người thụ hưởng, không thể gửi yêu cầu đóng quỹ cho chính mình.': 'groups.errorCannotRequestSelf',
  'Danh sách người nhận không được chứa thành viên trùng lặp.': 'groups.errorDuplicateRecipients',
  'Chỉ thành viên hoạt động mới có thể tạo chi phí nhóm.': 'groups.errorActiveMembersOnlyExpense',
  'Tên khoản chi không được để trống.': 'groups.errorExpenseTitleRequired',
  'Số tiền chi tiêu phải là số nguyên dương lớn hơn 0.': 'groups.errorExpenseAmountPositive',
  'Số dư quỹ chung không đủ để thanh toán khoản chi này.': 'groups.errorInsufficientFundBalance',
  'Vui lòng chọn người thanh toán.': 'groups.errorSelectPayer',
  'Người thanh toán phải là thành viên chính thức đang hoạt động trong nhóm.': 'groups.errorPayerMustBeActiveMember',
  'Lỗi ghi nhận giao dịch cá nhân.': 'groups.errorRecordPersonalTxFailed',
  'Lỗi cập nhật quỹ nhóm.': 'groups.errorUpdateGroupFundFailed',

  // Support & Disputes
  'Bạn cần đăng nhập để tạo yêu cầu hỗ trợ.': 'support.errorLoginRequired',
  'Vui lòng nhập tiêu đề yêu cầu hỗ trợ.': 'support.errorTitleRequired',
  'Vui lòng nhập nội dung chi tiết yêu cầu.': 'support.errorDescRequired',
  'Yêu cầu thêm lượt sửa cần liên kết với một giao dịch cụ thể.': 'support.errorEditRequestRequiresLinkedTx',
  'Không tìm thấy sổ cái tài chính của bạn để xác thực giao dịch.': 'support.errorLedgerNotFound',
  'Lỗi đọc trạng thái sổ cái tài chính.': 'support.errorLedgerReadFailed',
  'Giao dịch liên kết không tồn tại trong sổ cái của bạn.': 'support.errorLinkedTxNotFound',
  'Giao dịch liên kết không có thông tin sở hữu hoặc không thuộc về bạn.': 'support.errorLinkedTxUnauthorized',
  'Chỉ hỗ trợ yêu cầu thêm lượt sửa cho các giao dịch thu hoặc chi thủ công.': 'support.errorOnlyManualTxEditRequest',
  'Chỉ Quản trị viên hệ thống mới có quyền xử lý vé hỗ trợ.': 'support.errorAdminOnlyProcess',
  'Vui lòng nhập nội dung phản hồi hoặc chọn trạng thái mới.': 'support.errorResponseOrStatusRequired',
  'Không tìm thấy yêu cầu hỗ trợ.': 'support.errorCaseNotFound',
  'Bạn cần đăng nhập để phản hồi.': 'support.errorLoginToReply',
  'Vui lòng nhập nội dung phản hồi hoặc tải lên bằng chứng bổ sung.': 'support.errorResponseOrEvidenceRequired',
  'Bạn không có quyền phản hồi yêu cầu này.': 'support.errorReplyUnauthorized',
  'Chỉ Quản trị viên hệ thống mới có quyền cấp thêm lượt sửa.': 'support.errorAdminOnlyGrantAllowance',
  'Chỉ có thể cấp thêm lượt sửa cho các yêu cầu thuộc danh mục Yêu cầu chỉnh sửa giao dịch (transaction_edit).': 'support.errorOnlyTransactionEditCategory',
  'Yêu cầu hỗ trợ này đã được cấp thêm lượt sửa trước đó. Không thể cấp trùng lặp.': 'support.errorAlreadyGrantedAllowance',
  'Yêu cầu hỗ trợ này không liên kết với giao dịch hợp lệ.': 'support.errorInvalidLinkedTx',
  'Không tìm thấy dữ liệu sổ cái tài chính của người dùng này.': 'support.errorUserLedgerNotFound',
  'Lỗi đọc trạng thái sổ cái tài chính của người dùng.': 'support.errorUserLedgerReadFailed',
  'Dữ liệu giao dịch trong sổ cái người dùng không hợp lệ.': 'support.errorUserTxInvalid',
  'Giao dịch liên kết không tồn tại trong sổ cái của người dùng này.': 'support.errorUserLinkedTxNotFound',
  'Giao dịch liên kết không có thông tin chủ sở hữu hoặc không thuộc sở hữu của người dùng gửi yêu cầu.': 'support.errorUserLinkedTxNotOwned',
  'Chỉ có thể cấp thêm lượt sửa cho giao dịch thu hoặc chi thủ công.': 'support.errorOnlyManualTxGrant',
  'Không thể lưu cập nhật sổ cái của người dùng.': 'support.errorSaveUserLedgerFailed',
  'Bạn cần đăng nhập để gửi khiếu nại giao dịch.': 'support.errorLoginToDispute',
  'Bạn chỉ có thể khiếu nại giao dịch thuộc tài khoản của chính mình.': 'support.errorOnlyDisputeOwnTx',
  'Giao dịch liên kết không thuộc quyền sở hữu của bạn.': 'support.errorDisputeTxNotOwned',
  'Vui lòng nhập mô tả chi tiết lý do khiếu nại.': 'support.errorDisputeReasonDescRequired',
  'Chỉ Quản trị viên hệ thống mới có quyền xử lý khiếu nại giao dịch.': 'support.errorAdminOnlyProcessDispute',
  'Không tìm thấy hồ sơ khiếu nại giao dịch.': 'support.errorDisputeNotFound',
  'Vui lòng nhập nội dung giải trình hoặc đính kèm tài liệu bổ sung.': 'support.errorExplanationOrEvidenceRequired',
  'Bạn không có quyền phản hồi khiếu nại này.': 'support.errorDisputeReplyUnauthorized',
};

const VIETNAMESE_ACCENT_REGEX = /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i;

/**
 * Normalizes status codes to localized human-readable labels.
 */
export const getLocalizedStatusLabel = (status: string, locale: SupportedLocale): string => {
  const isVi = locale === 'vi-VN';
  const s = (status || '').trim();
  switch (s.toLowerCase()) {
    case 'active':
    case 'hoạt động':
      return isVi ? 'Hoạt động' : 'Active';
    case 'restricted':
    case 'bị giới hạn':
      return isVi ? 'Bị giới hạn' : 'Restricted';
    case 'suspended':
    case 'tạm đình chỉ':
      return isVi ? 'Tạm đình chỉ' : 'Suspended';
    case 'locked':
    case 'bị khóa':
      return isVi ? 'Bị khóa' : 'Locked';
    case 'pending':
    case 'chờ xử lý':
    case 'chờ tiếp nhận':
      return isVi ? 'Chờ xử lý' : 'Pending';
    case 'open':
    case 'đang mở':
      return isVi ? 'Đang mở' : 'Open';
    case 'in_progress':
    case 'in progress':
    case 'đang xử lý':
      return isVi ? 'Đang xử lý' : 'In progress';
    case 'in_review':
    case 'in review':
    case 'đang rà soát':
    case 'đang xem xét':
      return isVi ? 'Đang rà soát' : 'In review';
    case 'need_more_info':
    case 'need more info':
    case 'cần bổ sung thông tin':
      return isVi ? 'Cần bổ sung thông tin' : 'Need more info';
    case 'resolved':
    case 'đã giải quyết':
      return isVi ? 'Đã giải quyết' : 'Resolved';
    case 'closed':
    case 'đã đóng':
      return isVi ? 'Đã đóng' : 'Closed';
    case 'submitted':
    case 'đã tiếp nhận':
      return isVi ? 'Đã tiếp nhận' : 'Submitted';
    case 'rejected':
    case 'đã từ chối':
      return isVi ? 'Đã từ chối' : 'Rejected';
    case 'approved':
    case 'đã duyệt':
    case 'đã chấp nhận':
      return isVi ? 'Đã duyệt' : 'Approved';
    case 'completed':
    case 'hoàn tất':
      return isVi ? 'Hoàn tất' : 'Completed';
    default:
      return status;
  }
};

/**
 * Returns localized user-facing error message with priority on semantic errorCode.
 * When locale === en-US and error string contains Vietnamese characters, it avoids leaking
 * raw Vietnamese by returning fallbackKey or common.errorOccurred.
 */
export const getLocalizedErrorMessage = (
  resOrError: { errorCode?: string; errorParams?: Record<string, any>; error?: string } | string | null | undefined,
  locale: SupportedLocale,
  t: (key: string, params?: Record<string, any>) => string,
  fallbackKey?: string
): string => {
  if (!resOrError) {
    return fallbackKey ? t(fallbackKey) : t('common.errorOccurred');
  }

  if (typeof resOrError === 'string') {
    const trimmed = resOrError.trim();
    const mappedKey = LEGACY_ERROR_MAP[trimmed];
    if (mappedKey) {
      return t(mappedKey);
    }
    if (locale === 'en-US') {
      if (fallbackKey) {
        return t(fallbackKey);
      }
      if (VIETNAMESE_ACCENT_REGEX.test(trimmed)) {
        return t('common.errorOccurred');
      }
      return trimmed;
    }
    return resOrError;
  }

  if (resOrError.errorCode) {
    return t(resOrError.errorCode, resOrError.errorParams);
  }

  if (resOrError.error) {
    const trimmed = resOrError.error.trim();
    const mappedKey = LEGACY_ERROR_MAP[trimmed];
    if (mappedKey) {
      return t(mappedKey, resOrError.errorParams);
    }
    if (locale === 'en-US') {
      if (fallbackKey) {
        return t(fallbackKey, resOrError.errorParams);
      }
      if (VIETNAMESE_ACCENT_REGEX.test(trimmed)) {
        return t('common.errorOccurred');
      }
      return trimmed;
    }
    return resOrError.error;
  }

  return fallbackKey ? t(fallbackKey) : t('common.errorOccurred');
};

/**
 * Backward-compatible notification resolver that reads semantic keys first,
 * resolves dynamic parameters (money, categories, statuses) at render-time,
 * and maps legacy persisted notifications between Vietnamese and English.
 */
export const getLocalizedNotificationItem = (
  item: NotificationItem,
  locale: SupportedLocale,
  t: (key: string, params?: Record<string, any>) => string
): { title: string; message: string; detail?: string } => {
  let title = item.title;
  let message = item.message;
  let detail = item.detail;

  // Resolve dynamic parameters at render time
  const resolvedParams: Record<string, any> = item.i18nParams ? { ...item.i18nParams } : {};

  // 1. Resolve categoryName
  if (resolvedParams.categoryName !== undefined && resolvedParams.categoryName !== null) {
    const rawCat = String(resolvedParams.categoryName);
    if (rawCat.startsWith('categories.')) {
      resolvedParams.categoryName = t(rawCat);
    } else {
      resolvedParams.categoryName = getLocalizedCategoryName(rawCat, t);
    }
  }

  // 2. Resolve money parameters
  const moneyKeys = ['amount', 'spent', 'limit', 'balance', 'requiredAmount', 'paidAmount'];
  for (const k of moneyKeys) {
    if (resolvedParams[k] !== undefined && resolvedParams[k] !== null) {
      const val = resolvedParams[k];
      if (typeof val === 'number') {
        resolvedParams[k] = formatMoney(val, locale);
      } else if (typeof val === 'string') {
        const digits = val.replace(/[^\d]/g, '');
        if (digits.length > 0) {
          const num = Number(digits);
          if (!isNaN(num)) {
            resolvedParams[k] = formatMoney(num, locale);
          }
        }
      }
    }
  }

  // 3. Resolve status / statusLabel
  if (resolvedParams.status !== undefined && resolvedParams.status !== null) {
    resolvedParams.status = getLocalizedStatusLabel(String(resolvedParams.status), locale);
  }
  if (resolvedParams.statusLabel !== undefined && resolvedParams.statusLabel !== null) {
    resolvedParams.statusLabel = getLocalizedStatusLabel(String(resolvedParams.statusLabel), locale);
  }

  // 4. Semantic keys have first priority
  if (item.titleKey) {
    title = t(item.titleKey, resolvedParams);
  }
  if (item.messageKey) {
    message = t(item.messageKey, resolvedParams);
  }
  if (item.detailKey) {
    detail = t(item.detailKey, resolvedParams);
  }

  // If semantic keys were present, return immediately
  if (item.titleKey && item.messageKey) {
    return { title, message, detail };
  }

  // 5. Backward compatibility for legacy notifications
  if (locale === 'en-US') {
    const legacyTitleMap: Record<string, string> = {
      'Cần phân loại giao dịch': 'notifications.reviewRequiredTitle',
      'Yêu cầu đặt lại mật khẩu đã được gửi': 'notifications.passwordResetRequestedTitle',
      'Mật khẩu đã được đặt lại thành công': 'notifications.passwordResetSuccessTitle',
      'Yêu cầu đặt lại mã PIN bảo mật': 'notifications.pinResetRequestedTitle',
      'Mã PIN bảo mật đã được đặt lại thành công': 'notifications.pinResetSuccessTitle',
      'Mã PIN bảo mật đã được đặt lại bởi Quản trị viên': 'notifications.pinAdminResetTitle',
      'Mật khẩu đã được đặt lại bởi Quản trị viên': 'notifications.adminPasswordResetTitle',
      'Trạng thái tài khoản đã thay đổi': 'notifications.accountStatusChangedTitle',
      'Đổi mật khẩu thành công': 'notifications.securityPasswordChangedTitle',
      'Yêu cầu khôi phục tài khoản đã được gửi': 'notifications.recoveryRequestedTitle',
      'Mật khẩu của bạn đã được đặt lại': 'notifications.recoveryApprovedTitle',
      'Yêu cầu khôi phục tài khoản cần bổ sung thông tin': 'notifications.recoveryMoreInfoTitle',
      'Yêu cầu khôi phục tài khoản bị từ chối': 'notifications.recoveryRejectedTitle',
      'Yêu cầu của bạn đã được gửi thành công': 'notifications.supportSubmittedTitle',
      'Cập nhật yêu cầu hỗ trợ': 'notifications.supportUpdatedTitle',
      'Khiếu nại của bạn đã được gửi thành công': 'notifications.disputeSubmittedTitle',
      'Cập nhật hồ sơ khiếu nại': 'notifications.disputeUpdatedTitle',
      'Cấp thêm lượt chỉnh sửa giao dịch': 'notifications.correctionGrantedTitle',
      'Lời mời kết bạn mới': 'notifications.friendRequestTitle',
      'Lời mời kết bạn đã được chấp nhận': 'notifications.friendAcceptedTitle',
      'Lời mời kết bạn bị từ chối': 'notifications.friendRejectedTitle',
    };

    if (!item.titleKey && legacyTitleMap[item.title]) {
      title = t(legacyTitleMap[item.title]);
    } else if (!item.titleKey) {
      if (item.title.startsWith('Sắp chạm hạn mức ')) {
        const cat = item.title.replace('Sắp chạm hạn mức ', '').trim();
        title = t('notifications.budgetNearTitle', { categoryName: getLocalizedCategoryName(cat, t) });
      } else if (item.title.startsWith('Đã vượt hạn mức ')) {
        const cat = item.title.replace('Đã vượt hạn mức ', '').trim();
        title = t('notifications.budgetExceededTitle', { categoryName: getLocalizedCategoryName(cat, t) });
      } else if (item.title.endsWith(' đến hạn thanh toán')) {
        const rec = item.title.replace(' đến hạn thanh toán', '').trim();
        title = t('notifications.recurringDueTitle', { recurringTitle: rec });
      }
    }

    const legacyMessageMap: Record<string, string> = {
      'Giao dịch đã được ghi nhận vào lịch sử và thống kê nhưng cần bạn chọn hạng mục chính xác.': 'notifications.reviewRequiredMessage',
      'Vui lòng kiểm tra email đã đăng ký với SpendWise để tiếp tục đặt lại mật khẩu.': 'notifications.passwordResetRequestedMessage',
      'Mật khẩu đăng nhập tài khoản SpendWise của bạn đã được thay đổi thành công qua liên kết xác thực an toàn.': 'notifications.passwordResetSuccessMessage',
      'Yêu cầu đặt lại mã PIN bảo mật 6 số của bạn đã được tiếp nhận. Hướng dẫn và liên kết thiết lập lại đã được gửi đến email của bạn.': 'notifications.pinResetRequestedMessage',
      'Mã PIN bảo mật 6 chữ số của bạn đã được cập nhật thành công. Hãy dùng mã này để xác thực khi thực hiện các giao dịch quan trọng.': 'notifications.pinResetSuccessMessage',
      'Quản trị viên đã đặt lại mã PIN bảo mật 6 số của bạn. Bạn cần thiết lập mã PIN mới trước khi thực hiện các giao dịch tài chính.': 'notifications.pinAdminResetMessage',
      'Mật khẩu tài khoản của bạn đã được cập nhật an toàn. Yêu cầu đổi mật khẩu bắt buộc sau khi đặt lại đã hoàn tất.': 'notifications.securityPasswordChangedMessage',
      'Yêu cầu của bạn đang chờ quản trị viên xem xét.': 'notifications.recoveryRequestedMessage',
      'Yêu cầu khôi phục tài khoản của bạn đã được phê duyệt. Mật khẩu tạm thời đã được cấp, vui lòng đăng nhập và đổi mật khẩu mới.': 'notifications.recoveryApprovedMessage',
    };

    if (!item.messageKey && legacyMessageMap[item.message]) {
      message = t(legacyMessageMap[item.message]);
    } else if (!item.messageKey) {
      // Dynamic message parsing for legacy budget/recurring notifications
      const nearMatch = item.message.match(/([\d\.,]+)\s*₫\s*\/\s*([\d\.,]+)\s*₫\s*\((\d+)%\)/);
      if (nearMatch) {
        const spent = Number(nearMatch[1].replace(/[^\d]/g, ''));
        const limit = Number(nearMatch[2].replace(/[^\d]/g, ''));
        const percent = Number(nearMatch[3]);
        message = t('notifications.budgetNearMessage', {
          spent: formatMoney(spent, locale),
          limit: formatMoney(limit, locale),
          percent,
        });
      } else {
        const exceedMatch = item.message.match(/([\d\.,]+)\s*₫\s*\/\s*([\d\.,]+)\s*₫/);
        if (exceedMatch && item.title.includes('hạn mức')) {
          const spent = Number(exceedMatch[1].replace(/[^\d]/g, ''));
          const limit = Number(exceedMatch[2].replace(/[^\d]/g, ''));
          message = t('notifications.budgetExceededMessage', {
            spent: formatMoney(spent, locale),
            limit: formatMoney(limit, locale),
          });
        } else {
          const dueMatch = item.message.match(/([\d\.,]+)\s*₫/);
          if (dueMatch && item.title.includes('đến hạn thanh toán')) {
            const amt = Number(dueMatch[1].replace(/[^\d]/g, ''));
            message = t('notifications.recurringDueTodayMessage', {
              amount: formatMoney(amt, locale),
            });
          }
        }
      }
    }
  } else if (locale === 'vi-VN') {
    // Switching back EN -> VI for legacy notifications
    if (!item.titleKey) {
      if (item.title.startsWith('Approaching ') && item.title.endsWith(' budget limit')) {
        const cat = item.title.replace('Approaching ', '').replace(' budget limit', '').trim();
        title = t('notifications.budgetNearTitle', { categoryName: getLocalizedCategoryName(cat, t) });
      } else if (item.title.endsWith(' budget limit exceeded')) {
        const cat = item.title.replace(' budget limit exceeded', '').trim();
        title = t('notifications.budgetExceededTitle', { categoryName: getLocalizedCategoryName(cat, t) });
      } else if (item.title.endsWith(' payment is due')) {
        const rec = item.title.replace(' payment is due', '').trim();
        title = t('notifications.recurringDueTitle', { recurringTitle: rec });
      }
    }
    if (!item.messageKey) {
      const enNearMatch = item.message.match(/₫?([\d\.,]+)\s*(?:₫)?\s*of\s*₫?([\d\.,]+)\s*(?:₫)?\s*\((\d+)%\)/i);
      if (enNearMatch) {
        const spent = Number(enNearMatch[1].replace(/[^\d]/g, ''));
        const limit = Number(enNearMatch[2].replace(/[^\d]/g, ''));
        const percent = Number(enNearMatch[3]);
        message = t('notifications.budgetNearMessage', {
          spent: formatMoney(spent, locale),
          limit: formatMoney(limit, locale),
          percent,
        });
      } else {
        const enDueMatch = item.message.match(/₫?([\d\.,]+)/);
        if (enDueMatch && item.title.includes('payment is due')) {
          const amt = Number(enDueMatch[1].replace(/[^\d]/g, ''));
          message = t('notifications.recurringDueTodayMessage', {
            amount: formatMoney(amt, locale),
          });
        }
      }
    }
  }

  return { title, message, detail };
};

/**
 * Resolves GroupActivity to localized text.
 * Priority 1: Semantic keys (titleKey, descriptionKey, i18nParams)
 * Priority 2: Pattern match legacy SpendWise system activity strings (preserves user names, group names, purposes)
 * Priority 3: Fallback raw title / description
 */
export const getLocalizedGroupActivity = (
  activity: GroupActivity,
  locale: SupportedLocale,
  t: (key: string, params?: Record<string, any>) => string
): { title: string; description: string } => {
  const isVi = locale === 'vi-VN';
  const params: Record<string, any> = activity.i18nParams ? { ...activity.i18nParams } : {};

  // Format any money parameters in params
  const moneyKeys = ['amount', 'spent', 'targetAmount', 'balance'];
  for (const k of moneyKeys) {
    if (params[k] !== undefined && params[k] !== null) {
      const val = params[k];
      if (typeof val === 'number') {
        params[k] = formatMoney(val, locale);
      } else if (typeof val === 'string') {
        const digits = val.replace(/[^\d]/g, '');
        if (digits.length > 0) {
          const num = Number(digits);
          if (!isNaN(num)) {
            params[k] = formatMoney(num, locale);
          }
        }
      }
    }
  }

  // Priority 1: Semantic keys
  if (activity.titleKey) {
    const title = t(activity.titleKey, params);
    const description = activity.descriptionKey
      ? t(activity.descriptionKey, params)
      : activity.description;
    return { title, description };
  }

  // Priority 2: Known legacy SpendWise system activity mapping
  if (!isVi) {
    if (activity.title === 'Khởi tạo nhóm') {
      const m = activity.description.match(/^(.+?)\s+đã tạo nhóm\s+"(.+?)"$/);
      return {
        title: 'Group Created',
        description: m ? `${m[1]} created group "${m[2]}"` : activity.description,
      };
    }
    if (activity.title === 'Thiết lập tài khoản nhận tiền') {
      const m = activity.description.match(/^(.+?)\s+đã thiết lập tài khoản nhận tiền\s+\((.+?)\)\s+cho quỹ nhóm\.?$/);
      return {
        title: 'Receiving Bank Configured',
        description: m ? `${m[1]} configured receiving bank (${m[2]}) for group fund.` : activity.description,
      };
    }
    if (activity.title === 'Mời thành viên') {
      const m = activity.description.match(/^(.+?)\s+đã gửi lời mời tham gia nhóm đến\s+(.+?)(?:\s+\((.+?)\))?$/);
      return {
        title: 'Member Invited',
        description: m
          ? `${m[1]} sent an invitation to ${m[2]}${m[3] ? ` (${m[3]})` : ''}`
          : activity.description,
      };
    }
    if (activity.title === 'Thành viên mới gia nhập') {
      const m = activity.description.match(/^(.+?)\s+đã chấp nhận lời mời và tham gia nhóm$/);
      return {
        title: 'New Member Joined',
        description: m ? `${m[1]} accepted the invitation and joined the group` : activity.description,
      };
    }
    if (activity.title === 'Xóa thành viên') {
      const m = activity.description.match(/^Đã xóa\s+(.+?)\s+khỏi nhóm$/);
      return {
        title: 'Member Removed',
        description: m ? `${m[1]} was removed from the group` : activity.description,
      };
    }
    if (activity.title === 'Chuyển quyền Chủ nhóm') {
      const m = activity.description.match(/^Đã chuyển giao quyền Chủ nhóm cho\s+(.+)$/);
      return {
        title: 'Ownership Transferred',
        description: m ? `Group ownership transferred to ${m[1]}` : activity.description,
      };
    }
    if (activity.title === 'Đóng quỹ (Bank-Direct)' || activity.title === 'Đóng quỹ theo yêu cầu') {
      const m = activity.description.match(/^(.+?)\s+đã chuyển\s+([\d\.,]+)\s*₫\s+vào quỹ nhóm qua\s+(.+?)\.?$/);
      const amt = m ? Number(m[2].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: activity.title === 'Đóng quỹ theo yêu cầu' ? 'Fund Request Contribution' : 'Fund Contribution (Bank-Direct)',
        description: m
          ? `${m[1]} contributed ${formatMoney(amt, locale)} to group fund via ${m[3]}.`
          : activity.description,
      };
    }
    if (activity.title === 'Tạo đợt đóng quỹ') {
      const m = activity.description.match(/^Đã tạo đợt góp\s+"(.+?)"\s+với tổng cần thu\s+([\d\.,]+)\s*₫(?:,\s+phân bổ đều mức đóng góp cho\s+(\d+)\s+thành viên)?\.?$/);
      const amt = m ? Number(m[2].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: 'Fund Round Created',
        description: m
          ? `Created fund round "${m[1]}" with target ${formatMoney(amt, locale)}${m[3] ? ` allocated across ${m[3]} members` : ''}.`
          : activity.description,
      };
    }
    if (activity.title === 'Đóng phần quỹ (Bank-Direct)') {
      const m = activity.description.match(/^(.+?)\s+đã chuyển\s+([\d\.,]+)\s*₫\s+cho đợt\s+"(.+?)"\s+qua\s+(.+?)\.?$/);
      const amt = m ? Number(m[2].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: 'Fund Round Payment (Bank-Direct)',
        description: m
          ? `${m[1]} paid ${formatMoney(amt, locale)} for "${m[3]}" via ${m[4]}.`
          : activity.description,
      };
    }
    if (activity.title === 'Hoàn tất đợt đóng quỹ') {
      const m = activity.description.match(/^Đợt đóng quỹ\s+"(.+?)"\s+đã hoàn tất thu đủ 100% mục tiêu\s*\(([\d\.,]+)\s*₫\)\.?$/);
      const amt = m ? Number(m[2].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: 'Fund Round Completed',
        description: m
          ? `Fund round "${m[1]}" completed with 100% of target (${formatMoney(amt, locale)}) collected.`
          : activity.description,
      };
    }
    if (activity.title === 'Chi từ quỹ chung') {
      const m = activity.description.match(/^Đã chi\s+([\d\.,]+)\s*₫\s+từ quỹ chung cho\s+"(.+?)"\.?$/);
      const amt = m ? Number(m[1].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: 'Expense from Shared Fund',
        description: m
          ? `Spent ${formatMoney(amt, locale)} from shared fund for "${m[2]}".`
          : activity.description,
      };
    }
    if (activity.title.endsWith(' đã thanh toán')) {
      const payer = activity.title.replace(' đã thanh toán', '').trim();
      const m = activity.description.match(/^Đã chi\s+([\d\.,]+)\s*₫\s+cho\s+"(.+?)"\.?$/);
      const amt = m ? Number(m[1].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: `${payer} Paid`,
        description: m
          ? `Spent ${formatMoney(amt, locale)} for "${m[2]}".`
          : activity.description,
      };
    }
    if (activity.title === 'Yêu cầu đóng quỹ') {
      const m = activity.description.match(/^Đã tạo yêu cầu đóng\s+([\d\.,]+)\s*₫\/người cho\s+"(.+?)"\.?$/);
      const amt = m ? Number(m[1].replace(/[^\d]/g, '')) : 0;
      return {
        title: 'Fund Request Created',
        description: m
          ? `Created fund request of ${formatMoney(amt, locale)}/person for "${m[2]}".`
          : activity.description,
      };
    }
    if (activity.title === 'Yêu cầu xóa nhóm') {
      const m = activity.description.match(/^(.+?)\s+đã gửi yêu cầu xóa nhóm\.\s+Cần tất cả\s+(\d+)\s+thành viên đang hoạt động đồng ý\.?$/);
      return {
        title: 'Group Deletion Requested',
        description: m
          ? `${m[1]} requested group deletion. Requires approval from all ${m[2]} active members.`
          : activity.description,
      };
    }
    if (activity.title === 'Hủy yêu cầu xóa nhóm') {
      const m = activity.description.match(/^(.+?)\s+đã hủy yêu cầu xóa nhóm\.\s+Nhóm hoạt động bình thường trở lại\.?$/);
      return {
        title: 'Group Deletion Cancelled',
        description: m
          ? `${m[1]} cancelled group deletion request. Group resumed normal activity.`
          : activity.description,
      };
    }
    if (activity.title === 'Từ chối yêu cầu xóa nhóm') {
      const m = activity.description.match(/^(.+?)\s+đã từ chối yêu cầu xóa nhóm\.\s+Yêu cầu xóa bị hủy bỏ và nhóm hoạt động bình thường\.?$/);
      return {
        title: 'Group Deletion Rejected',
        description: m
          ? `${m[1]} rejected group deletion request. Request cancelled and group resumed normal activity.`
          : activity.description,
      };
    }
    if (activity.title === 'Đồng thuận xóa nhóm') {
      const m = activity.description.match(/^Tất cả thành viên\s+\((\d+)\/(\d+)\)\s+đã đồng ý xóa nhóm\.\s+Yêu cầu ở trạng thái Sẵn sàng xử lý\.?$/);
      return {
        title: 'Deletion Consensus Reached',
        description: m
          ? `All members (${m[1]}/${m[2]}) agreed to delete group. Request is now Ready to process.`
          : activity.description,
      };
    }
    if (activity.title === 'Phản hồi biểu quyết xóa nhóm') {
      const m = activity.description.match(/^(.+?)\s+đã đồng ý xóa nhóm\s+\((\d+)\/(\d+)\s+thành viên đã đồng ý\)\.?$/);
      return {
        title: 'Deletion Vote Response',
        description: m
          ? `${m[1]} agreed to delete group (${m[2]}/${m[3]} members approved).`
          : activity.description,
      };
    }
    if (activity.title === 'Hoàn tiền giải tán quỹ') {
      const m = activity.description.match(/^Đã hoàn trả\s+([\d\.,]+)\s*₫\s+cho\s+(.+?)(?:\s+qua (?:Bank-Direct|ví SpendWise))?\.?$/);
      const amt = m ? Number(m[1].replace(/[^\d]/g, '')) : activity.amount || 0;
      return {
        title: 'Fund Dissolution Refund',
        description: m
          ? `Refunded ${formatMoney(amt, locale)} to ${m[2]} to SpendWise wallet.`
          : activity.description,
      };
    }
  }

  // Priority 3: Fallback raw title & description
  return { title: activity.title, description: activity.description };
};

