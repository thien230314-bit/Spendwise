# DATA & STATE MAPPING — SPENDWISE ARCHITECTURE
**Tài liệu Kiểm kê Ánh xạ Trạng thái React, Context Providers & API Services**  
**Dự án:** SpendWise Web Application  
**Ngày kiểm kê:** 12/10/2026  
**Nguồn xác thực:** `src/providers/*`, `src/services/*`, `src/types/*`

---

## 1. REACT CONTEXT PROVIDERS (HỆ THỐNG TRẠNG THÁI TOÀN CỤC)

| Provider | File Source | State Chính | API Services / Data Stores | Phạm vi Ảnh hưởng |
|---|---|---|---|---|
| **AuthProvider** | `src/providers/AuthProvider.tsx` | `currentUser`, `isAuthenticated`, `role`, `pendingChallenge`, `pendingEmailVerification`, `isSessionExpired` | `authApiService.ts`, `tokenStorage.ts`, `deviceTrustStorage.ts` | Toàn bộ ứng dụng, Route Protection, Header Profile |
| **FinancialProvider**| `src/providers/FinancialProvider.tsx` | `wallets`, `transactions`, `budgets`, `goals`, `categories`, `dashboardSummary`, `attentionItems`, `bankConnections` | `financialApiService.ts`, `bankApiService.ts`, `planningApiService.ts` | Dashboard, Wallet, Transactions, Budgets, Goals |
| **GroupProvider** | `src/providers/GroupProvider.tsx` | `groups`, `groupFundBalance`, `userPendingFundObligations`, `activeFundRounds` | `groupApiService.ts`, `groupMapper.ts` | GroupsPage, GroupDetailPage, Dashboard Summary |
| **BalanceVisibilityProvider** | `src/providers/BalanceVisibilityProvider.tsx` | `isHidden`, `toggleVisibility` | `localStorage: spendwise_balance_hidden` | Header, Dashboard, Wallet, Budgets, Goals |
| **SecurityPinChallengeProvider** | `src/providers/SecurityPinChallengeProvider.tsx` | `isOpen`, `challengePin`, `challengePinWithValue`, `onSuccess`, `onCancel` | `securityPinService.ts`, `SixDigitPinInput.tsx` | Wallet (Đóng ví), Transfer, Goals, Settings |
| **NotificationProvider** | `src/providers/NotificationProvider.tsx` | `notifications`, `unreadCount`, `markAsRead`, `refreshNotifications` | `notificationsApiService.ts` | Header Notification Bell, NotificationsPage |
| **ThemeProvider** | `src/providers/ThemeProvider.tsx` | `theme`, `resolvedTheme`, `setTheme` | `localStorage: spendwise_theme` (Light / Dark / System) | Toàn bộ giao diện HTML Root Element |

---

## 2. API SERVICES INVENTORY (CỔNG GIAO TIẾP BACKEND)

| Service Name | File Path | Các Method Quan Trọng | Endpoints Backend Phục vụ |
|---|---|---|---|
| **authApiService** | `src/services/authApiService.ts` | `login`, `register`, `verifyPin`, `setupPin`, `verifyEmailOtp`, `resendEmailOtp`, `refreshToken`, `logout`, `getProfile`, `updateProfile`, `changePassword` | `/api/auth/*`, `/api/users/profile` |
| **financialApiService** | `src/services/financialApiService.ts` | `getDashboardSummary`, `getWallets`, `createWallet`, `updateWallet`, `deleteWallet`, `getTransactions`, `createTransaction`, `updateTransaction`, `deleteTransaction` | `/api/financial/*`, `/api/wallets/*`, `/api/transactions/*` |
| **bankApiService** | `src/services/bankApiService.ts` | `getBankConnections`, `linkBank`, `unlinkBank`, `syncBank`, `getBankTransactions`, `topUpWallet`, `confirmTopUp` | `/api/banking/*`, `/api/sepay/*` |
| **transfersApiService** | `src/services/transfersApiService.ts` | `getRecipientByPhone`, `createTransfer`, `getTransfers`, `getTransferDetail` | `/api/transfers/*` |
| **planningApiService** | `src/services/planningApiService.ts` | `getBudgets`, `createBudget`, `updateBudget`, `deleteBudget`, `getGoals`, `createGoal`, `contributeGoal`, `withdrawGoal` | `/api/planning/budgets/*`, `/api/planning/goals/*` |
| **friendService** | `src/services/friendService.ts` | `getFriends`, `sendRequest`, `acceptRequest`, `rejectRequest`, `toggleFavorite`, `setNickname` | `/api/friends/*` |
| **groupApiService** | `src/services/groupApiService.ts` | `getGroups`, `createGroup`, `getGroupDetail`, `createFundRound`, `contributeFundRound`, `createExpense` | `/api/groups/*` |
| **casesApiService** | `src/services/casesApiService.ts` | `getCases`, `createCase`, `getDisputes`, `createDispute`, `getCaseMessages`, `sendMessage` | `/api/cases/*` |
| **systemOperationsService** | `src/services/systemOperationsService.ts` | `getSystemHealth`, `getSystemStats`, `getGlobalUsers`, `lockUser`, `getLedger`, `getAuditLogs`, `getConfiguration`, `updateConfiguration`, `getFeatureFlags`, `toggleFeatureFlag` | `/api/admin/*`, `/api/system/*` |
| **securityPinService** | `src/services/securityPinService.ts` | `validatePinResetToken`, `resetPinWithToken`, `changePin`, `verifyPinClient` | `/api/auth/pin/*` |

---

## 3. DERIVED FINANCIAL LOGIC (CÔNG THỨC TÍNH TOÁN DÒNG TIỀN)

1. **Tổng số dư khả dụng (Total Balance):**
   $$\text{Total Balance} = \sum_{w \in \text{Wallets}} w.\text{balance}$$
2. **Dòng tiền ròng tháng này (Net Cash Flow):**
   $$\text{Net Cash Flow} = \text{Total Income (Current Month)} - \text{Total Expenses (Current Month)}$$
3. **Ngân sách còn lại khả dụng (Budget Remaining):**
   $$\text{Budget Remaining} = \sum_{b \in \text{Budgets}} \max(0, b.\text{limitAmount} - b.\text{spentAmount})$$
4. **Tiến độ tích lũy mục tiêu (% Goal Progress):**
   $$\text{Progress} \% = \min\left(100, \left\lfloor \frac{\sum g.\text{currentAmount}}{\sum g.\text{targetAmount}} \times 100 \right\rfloor\right)$$
