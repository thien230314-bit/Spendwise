export type AdminScreenId =
  | 'overview'
  | 'reports'
  | 'tasks'
  | 'alerts'
  | 'users'
  | 'transactions'
  | 'support'
  | 'system';

export type AdminTimeframeId = '7d' | '30d' | 'q4_2026';

export interface AdminOverviewWindowStats {
  timeframe: AdminTimeframeId;
  labelVi: string;
  labelEn: string;
  periodRangeVi: string;
  periodRangeEn: string;
  gmvVnd: number;
  gmvDeltaLabelVi: string;
  gmvDeltaLabelEn: string;
  activeUsers: number;
  verifiedAuthRatePct: number;
  activeGroupFundsBalanceVnd: number;
  activeGroupFundsCount: number;
  reconciliationRatePct: number;
  openTasksCount: number;
  dailySeries: Array<{
    dateLabel: string;
    personalFlowVnd: number;
    groupFundFlowVnd: number;
    txCount: number;
  }>;
}

export interface AdminActivityReportRow {
  id: string;
  code: string;
  domain: 'wallet_p2p' | 'group_funds' | 'banking' | 'budget_goals';
  domainLabelVi: string;
  domainLabelEn: string;
  titleVi: string;
  titleEn: string;
  periodLabel: string;
  txCount: number;
  grossVolumeVnd: number;
  netSettledVnd: number;
  successRatePct: number;
  varianceVnd: number;
  status: 'reconciled' | 'variance_review' | 'scheduled';
  statusLabelVi: string;
  statusLabelEn: string;
  ownerUnitVi: string;
  ownerUnitEn: string;
  summaryNoteVi: string;
  summaryNoteEn: string;
}

export interface AdminOperationalTask {
  id: string;
  code: string;
  titleVi: string;
  titleEn: string;
  categoryVi: string;
  categoryEn: string;
  priority: 'high' | 'medium' | 'low';
  status: 'pending' | 'in_review' | 'completed';
  assignee: string;
  createdAt: string;
  dueAt: string;
  amountVnd?: number;
  referenceEntity: string;
  descriptionVi: string;
  descriptionEn: string;
  operatorNoteVi: string;
  operatorNoteEn: string;
}

export interface AdminSystemAlert {
  id: string;
  code: string;
  severity: 'critical' | 'warning' | 'info';
  state: 'unacknowledged' | 'acknowledged' | 'resolved';
  serviceVi: string;
  serviceEn: string;
  titleVi: string;
  titleEn: string;
  detailVi: string;
  detailEn: string;
  triggeredAt: string;
  affectedCountLabelVi: string;
  affectedCountLabelEn: string;
  runbookHintVi: string;
  runbookHintEn: string;
}

export const ADMIN_OVERVIEW_WINDOWS: Record<AdminTimeframeId, AdminOverviewWindowStats> = {
  '7d': {
    timeframe: '7d',
    labelVi: '7 ngày qua',
    labelEn: 'Last 7 days',
    periodRangeVi: '28/10/2026 – 03/11/2026',
    periodRangeEn: 'Oct 28, 2026 – Nov 03, 2026',
    gmvVnd: 1_142_800_000,
    gmvDeltaLabelVi: '+11,4% so với 7 ngày trước',
    gmvDeltaLabelEn: '+11.4% vs previous 7 days',
    activeUsers: 6_480,
    verifiedAuthRatePct: 95.2,
    activeGroupFundsBalanceVnd: 1_295_000_000,
    activeGroupFundsCount: 312,
    reconciliationRatePct: 99.86,
    openTasksCount: 4,
    dailySeries: [
      { dateLabel: '28/10', personalFlowVnd: 92_000_000, groupFundFlowVnd: 54_000_000, txCount: 1_120 },
      { dateLabel: '29/10', personalFlowVnd: 104_500_000, groupFundFlowVnd: 61_200_000, txCount: 1_245 },
      { dateLabel: '30/10', personalFlowVnd: 98_300_000, groupFundFlowVnd: 49_800_000, txCount: 1_190 },
      { dateLabel: '31/10', personalFlowVnd: 128_000_000, groupFundFlowVnd: 74_500_000, txCount: 1_510 },
      { dateLabel: '01/11', personalFlowVnd: 115_200_000, groupFundFlowVnd: 68_000_000, txCount: 1_380 },
      { dateLabel: '02/11', personalFlowVnd: 89_400_000, groupFundFlowVnd: 52_100_000, txCount: 1_095 },
      { dateLabel: '03/11', personalFlowVnd: 118_900_000, groupFundFlowVnd: 36_900_000, txCount: 1_290 },
    ],
  },
  '30d': {
    timeframe: '30d',
    labelVi: '30 ngày qua',
    labelEn: 'Last 30 days',
    periodRangeVi: '05/10/2026 – 03/11/2026',
    periodRangeEn: 'Oct 05, 2026 – Nov 03, 2026',
    gmvVnd: 4_820_500_000,
    gmvDeltaLabelVi: '+14,2% so với kỳ 30 ngày trước',
    gmvDeltaLabelEn: '+14.2% vs prior 30-day window',
    activeUsers: 18_420,
    verifiedAuthRatePct: 94.6,
    activeGroupFundsBalanceVnd: 1_295_000_000,
    activeGroupFundsCount: 312,
    reconciliationRatePct: 99.84,
    openTasksCount: 4,
    dailySeries: [
      { dateLabel: 'T1 · 10', personalFlowVnd: 680_000_000, groupFundFlowVnd: 390_000_000, txCount: 8_140 },
      { dateLabel: 'T2 · 10', personalFlowVnd: 745_000_000, groupFundFlowVnd: 425_000_000, txCount: 8_920 },
      { dateLabel: 'T3 · 10', personalFlowVnd: 812_000_000, groupFundFlowVnd: 468_000_000, txCount: 9_640 },
      { dateLabel: 'T4 · 10', personalFlowVnd: 895_000_000, groupFundFlowVnd: 512_500_000, txCount: 10_480 },
      { dateLabel: 'Đầu T11', personalFlowVnd: 523_500_000, groupFundFlowVnd: 369_500_000, txCount: 6_310 },
    ],
  },
  q4_2026: {
    timeframe: 'q4_2026',
    labelVi: 'Quý 4/2026',
    labelEn: 'Q4 2026',
    periodRangeVi: '01/10/2026 – 03/11/2026 (Lũy kế quý)',
    periodRangeEn: 'Oct 01, 2026 – Nov 03, 2026 (QTD)',
    gmvVnd: 5_490_200_000,
    gmvDeltaLabelVi: '+18,9% so với cùng kỳ Quý 3/2026',
    gmvDeltaLabelEn: '+18.9% vs Q3 2026 QTD',
    activeUsers: 21_150,
    verifiedAuthRatePct: 94.9,
    activeGroupFundsBalanceVnd: 1_295_000_000,
    activeGroupFundsCount: 312,
    reconciliationRatePct: 99.81,
    openTasksCount: 4,
    dailySeries: [
      { dateLabel: 'Tuần 40', personalFlowVnd: 710_000_000, groupFundFlowVnd: 402_000_000, txCount: 8_420 },
      { dateLabel: 'Tuần 41', personalFlowVnd: 790_000_000, groupFundFlowVnd: 448_000_000, txCount: 9_210 },
      { dateLabel: 'Tuần 42', personalFlowVnd: 845_000_000, groupFundFlowVnd: 492_000_000, txCount: 9_890 },
      { dateLabel: 'Tuần 43', personalFlowVnd: 912_000_000, groupFundFlowVnd: 538_000_000, txCount: 10_760 },
      { dateLabel: 'Tuần 44', personalFlowVnd: 685_200_000, groupFundFlowVnd: 348_000_000, txCount: 7_980 },
    ],
  },
};

export const ADMIN_ACTIVITY_REPORTS_FIXTURES: AdminActivityReportRow[] = [
  {
    id: 'rpt_101',
    code: 'RPT-2026-10A',
    domain: 'wallet_p2p',
    domainLabelVi: 'Ví cá nhân & Chuyển P2P',
    domainLabelEn: 'Personal Wallets & P2P',
    titleVi: 'Đối soát chuyển khoản nội bộ P2P & QR SpendID',
    titleEn: 'Internal P2P & SpendID QR Transfer Reconciliation',
    periodLabel: '10/2026',
    txCount: 24_680,
    grossVolumeVnd: 2_640_000_000,
    netSettledVnd: 2_640_000_000,
    successRatePct: 99.94,
    varianceVnd: 0,
    status: 'reconciled',
    statusLabelVi: 'Đã khớp lệnh',
    statusLabelEn: 'Reconciled',
    ownerUnitVi: 'Khối Thanh toán Nội bộ',
    ownerUnitEn: 'Core Ledger Ops',
    summaryNoteVi:
      '100% bút toán chuyển tiền hai chiều khớp cặp correlationId, không phát sinh lệch số dư khả dụng.',
    summaryNoteEn:
      '100% of bidirectional P2P entries matched by correlationId with zero liquid balance drift.',
  },
  {
    id: 'rpt_102',
    code: 'RPT-2026-10B',
    domain: 'group_funds',
    domainLabelVi: 'Quỹ nhóm & Giải ngân',
    domainLabelEn: 'Group Funds & Disbursements',
    titleVi: 'Tổng hợp góp quỹ định kỳ (Fixed) & giải ngân 24h (Free)',
    titleEn: 'Fixed Fund Cycles & Free Fund 24h Disbursement Audit',
    periodLabel: '10/2026',
    txCount: 8_420,
    grossVolumeVnd: 1_295_000_000,
    netSettledVnd: 1_292_550_000,
    successRatePct: 99.62,
    varianceVnd: 2_450_000,
    status: 'variance_review',
    statusLabelVi: 'Đang rà soát khiếu nại',
    statusLabelEn: 'Variance Under Review',
    ownerUnitVi: 'Ban Quản trị Quỹ nhóm',
    ownerUnitEn: 'Group Governance Ops',
    summaryNoteVi:
      'Có 1 yêu cầu giải ngân 2.450.000 ₫ tại Quỹ Du lịch Đà Lạt đang trong trạng thái tạm giữ để đối chiếu chứng từ.',
    summaryNoteEn:
      '1 disbursement request (2,450,000 VND in Da Lat Trip Fund) is held for receipt verification.',
  },
  {
    id: 'rpt_103',
    code: 'RPT-2026-10C',
    domain: 'banking',
    domainLabelVi: 'Ngân hàng liên kết',
    domainLabelEn: 'Linked Banking Gateways',
    titleVi: 'Đối soát nạp/rút Napas 247 (VCB, TCB, BIDV, MB)',
    titleEn: 'Napas 247 Top-Up & Withdrawal Gateway Settlement',
    periodLabel: '10/2026',
    txCount: 6_190,
    grossVolumeVnd: 685_500_000,
    netSettledVnd: 670_500_000,
    successRatePct: 99.48,
    varianceVnd: 15_000_000,
    status: 'variance_review',
    statusLabelVi: 'Chờ xác nhận mã tham chiếu',
    statusLabelEn: 'Pending Reference Match',
    ownerUnitVi: 'Khối Đối soát Ngân hàng',
    ownerUnitEn: 'Banking Settlement Team',
    summaryNoteVi:
      '1 giao dịch nạp ví 15.000.000 ₫ qua cổng Vietcombank ghi nhận sai cú pháp nội dung chuyển khoản, đang chờ xử lý tại hàng đợi tác vụ.',
    summaryNoteEn:
      '1 top-up of 15,000,000 VND via Vietcombank had a malformed transfer memo and awaits manual matching.',
  },
  {
    id: 'rpt_104',
    code: 'RPT-2026-10D',
    domain: 'budget_goals',
    domainLabelVi: 'Ngân sách & Mục tiêu',
    domainLabelEn: 'Budgets & Savings Goals',
    titleVi: 'Phân bổ tiết kiệm tự động & thanh toán hóa đơn định kỳ',
    titleEn: 'Automated Goal Allocations & Recurring Bill Executions',
    periodLabel: '10/2026',
    txCount: 4_200,
    grossVolumeVnd: 200_000_000,
    netSettledVnd: 200_000_000,
    successRatePct: 99.98,
    varianceVnd: 0,
    status: 'reconciled',
    statusLabelVi: 'Đã khớp lệnh',
    statusLabelEn: 'Reconciled',
    ownerUnitVi: 'Khối Tự động hóa Tài chính',
    ownerUnitEn: 'Financial Automation Unit',
    summaryNoteVi:
      'Toàn bộ lệnh trích ngân sách và giữ chỗ mục tiêu tiết kiệm khớp 100% với số dư khả dụng của ví nguồn.',
    summaryNoteEn:
      'All recurring bill runs and savings goal reservations reconciled with source wallet liquid balances.',
  },
  {
    id: 'rpt_105',
    code: 'RPT-2026-11A',
    domain: 'wallet_p2p',
    domainLabelVi: 'Ví cá nhân & Chuyển P2P',
    domainLabelEn: 'Personal Wallets & P2P',
    titleVi: 'Báo cáo nhanh đầu kỳ Tháng 11/2026 (01/11 – 03/11)',
    titleEn: 'Early November 2026 Flash Ledger Report (Nov 01 – Nov 03)',
    periodLabel: '11/2026',
    txCount: 3_765,
    grossVolumeVnd: 448_300_000,
    netSettledVnd: 448_300_000,
    successRatePct: 99.95,
    varianceVnd: 0,
    status: 'scheduled',
    statusLabelVi: 'Đang lũy kế kỳ mới',
    statusLabelEn: 'Active Period Rolling',
    ownerUnitVi: 'Khối Thanh toán Nội bộ',
    ownerUnitEn: 'Core Ledger Ops',
    summaryNoteVi:
      'Chu kỳ đối soát tháng 11/2026 đang ghi nhận tự động theo giờ, không có sai lệch tồn đọng.',
    summaryNoteEn:
      'November 2026 settlement window is actively accumulating hourly with zero ledger discrepancies.',
  },
];

export const ADMIN_OPERATIONAL_TASKS_FIXTURES: AdminOperationalTask[] = [
  {
    id: 'tsk_01',
    code: 'TOPUP-8841',
    titleVi: 'Đối soát thủ công lệnh nạp ví sai mã tham chiếu Vietcombank',
    titleEn: 'Manual match for Vietcombank top-up with malformed reference code',
    categoryVi: 'Đối soát Ngân hàng',
    categoryEn: 'Banking Settlement',
    priority: 'high',
    status: 'pending',
    assignee: 'Trần Hoàng Nam · Ops Ca 1',
    createdAt: '03/11/2026 · 09:15',
    dueAt: '03/11/2026 · 17:00',
    amountVnd: 15_000_000,
    referenceEntity: 'VCB-FT2630789120 · Ví chính (u_minh)',
    descriptionVi:
      'Khách hàng chuyển khoản 15.000.000 ₫ qua Napas 247 nhưng thiếu hậu tố mã tham chiếu SWTOPUP. Sao kê ngân hàng đã báo Có.',
    descriptionEn:
      'User transferred 15,000,000 VND via Napas 247 omitting the SWTOPUP suffix. Bank statement credit is confirmed.',
    operatorNoteVi: 'Đã kiểm tra bản chụp sao kê VCB, khớp số tài khoản đuôi 9921.',
    operatorNoteEn: 'Verified VCB statement snapshot; matches source account ending 9921.',
  },
  {
    id: 'tsk_02',
    code: 'DSP-4092',
    titleVi: 'Thẩm định chứng từ khiếu nại khoản chi 24h tại Quỹ Du lịch Đà Lạt',
    titleEn: 'Verify receipt evidence for disputed 24h spend in Da Lat Trip Fund',
    categoryVi: 'Tranh chấp Quỹ nhóm',
    categoryEn: 'Group Fund Dispute',
    priority: 'high',
    status: 'in_review',
    assignee: 'Lê Thu Hà · Quản trị Rủi ro',
    createdAt: '03/11/2026 · 08:40',
    dueAt: '03/11/2026 · 18:30',
    amountVnd: 2_450_000,
    referenceEntity: 'Quỹ Du lịch Đà Lạt · Yêu cầu #req_dl_09',
    descriptionVi:
      'Thành viên gửi yêu cầu đối chiếu hóa đơn đặt cọc homestay 2.450.000 ₫. Chủ quỹ đã bổ sung ảnh chụp biên lai thanh toán.',
    descriptionEn:
      'Member requested invoice verification for 2,450,000 VND homestay deposit. Fund owner uploaded payment receipt.',
    operatorNoteVi: 'Biên lai hợp lệ, đang chờ xác nhận hoàn tất đóng hồ sơ khiếu nại.',
    operatorNoteEn: 'Receipt is valid; awaiting final sign-off to close dispute ticket.',
  },
  {
    id: 'tsk_03',
    code: 'WDR-7730',
    titleVi: 'Xác minh hạn mức rút tiền giá trị cao về Techcombank',
    titleEn: 'High-value withdrawal velocity check to Techcombank account',
    categoryVi: 'Kiểm soát Rủi ro',
    categoryEn: 'Risk & Compliance',
    priority: 'medium',
    status: 'pending',
    assignee: 'Phạm Minh Quân · Risk Ops',
    createdAt: '03/11/2026 · 10:05',
    dueAt: '03/11/2026 · 19:00',
    amountVnd: 85_000_000,
    referenceEntity: 'TCB-19038821 · Ví Tiết kiệm',
    descriptionVi:
      'Lệnh rút 85.000.000 ₫ đã qua bước xác thực mã PIN và sinh trắc học thiết bị tin cậy, cần duyệt hạn mức ca trực.',
    descriptionEn:
      '85,000,000 VND withdrawal passed PIN and trusted-device biometric checks; requires shift supervisor release.',
    operatorNoteVi: 'Thiết bị khớp lịch sử đăng nhập 90 ngày gần nhất.',
    operatorNoteEn: 'Device fingerprint matches 90-day login history.',
  },
  {
    id: 'tsk_04',
    code: 'GOV-3108',
    titleVi: 'Kiểm tra biên bản bàn giao quyền Chủ quỹ sau bầu cử',
    titleEn: 'Audit post-election fund ownership transfer ledger snapshot',
    categoryVi: 'Quản trị Quỹ nhóm',
    categoryEn: 'Fund Governance',
    priority: 'low',
    status: 'in_review',
    assignee: 'Nguyễn Phương Thảo · Governance',
    createdAt: '02/11/2026 · 16:20',
    dueAt: '04/11/2026 · 12:00',
    amountVnd: 42_000_000,
    referenceEntity: 'Quỹ Gia đình Việt · Revision #4',
    descriptionVi:
      'Đối chiếu chữ ký số PIN và lịch sử biểu quyết 100% đồng thuận khi chuyển giao quyền quản trị quỹ định kỳ.',
    descriptionEn:
      'Verify PIN signature and unanimous confirmation round log for fixed fund ownership handoff.',
    operatorNoteVi: 'Biên bản bầu cử hợp lệ, không có giao dịch treo trong lúc chuyển giao.',
    operatorNoteEn: 'Election log valid; zero pending reservations during ownership transition.',
  },
  {
    id: 'tsk_05',
    code: 'REC-6204',
    titleVi: 'Đối soát lô thanh toán hóa đơn định kỳ đầu tháng 11/2026',
    titleEn: 'Verify early-November recurring bill batch execution log',
    categoryVi: 'Vận hành Hệ thống',
    categoryEn: 'Batch Operations',
    priority: 'low',
    status: 'completed',
    assignee: 'Trần Hoàng Nam · Ops Ca 1',
    createdAt: '01/11/2026 · 07:00',
    dueAt: '01/11/2026 · 12:00',
    amountVnd: 64_200_000,
    referenceEntity: 'Batch #RB-20261101 · 214 lệnh',
    descriptionVi:
      'Kiểm tra kết quả chạy tự động 214 hóa đơn điện, nước, internet định kỳ ngày đầu tháng.',
    descriptionEn:
      'Verified automated execution of 214 monthly utility and subscription bills on Nov 1.',
    operatorNoteVi: 'Hoàn tất 214/214 lệnh, không phát sinh lỗi trừ kép.',
    operatorNoteEn: 'Completed 214/214 executions with zero duplicate debits.',
  },
];

export const ADMIN_SYSTEM_ALERTS_FIXTURES: AdminSystemAlert[] = [
  {
    id: 'alt_901',
    code: 'ALT-901',
    severity: 'critical',
    state: 'unacknowledged',
    serviceVi: 'Cổng đối soát Ngân hàng BIDV',
    serviceEn: 'BIDV Napas Webhook Gateway',
    titleVi: 'Độ trễ webhook báo Có từ cổng BIDV tăng trên 4,2 giây',
    titleEn: 'Inbound settlement webhook latency from BIDV exceeded 4.2s threshold',
    detailVi:
      'Thời gian phản hồi trung bình tăng từ 0,8s lên 4,2s trong 15 phút qua. Hệ thống đã tự động kích hoạt cơ chế hàng đợi đối soát dự phòng.',
    detailEn:
      'Mean webhook callback time rose from 0.8s to 4.2s over the last 15m. Fallback polling reconciliation queue is active.',
    triggeredAt: '03/11/2026 · 10:18',
    affectedCountLabelVi: '12 lệnh nạp đang chờ đối soát',
    affectedCountLabelEn: '12 top-ups queued for polling',
    runbookHintVi: 'Kiểm tra kết nối kênh thuê riêng BIDV hoặc giữ chế độ polling 30 giây/lần.',
    runbookHintEn: 'Verify BIDV leased-line health and maintain 30s fallback polling interval.',
  },
  {
    id: 'alt_902',
    code: 'ALT-902',
    severity: 'warning',
    state: 'unacknowledged',
    serviceVi: 'Bảo mật PIN & Sinh trắc học',
    serviceEn: 'PIN & Biometric Auth Guard',
    titleVi: 'Phát hiện 3 phiên nhập sai mã PIN giao dịch liên tiếp',
    titleEn: '3 consecutive failed transaction PIN attempts across untrusted browsers',
    detailVi:
      'Cơ chế khóa tạm thời 15 phút đã tự động kích hoạt đối với các phiên vượt ngưỡng thử sai mã PIN.',
    detailEn:
      'Automatic 15-minute cooldown lock was enforced on sessions exceeding the failed PIN threshold.',
    triggeredAt: '03/11/2026 · 09:42',
    affectedCountLabelVi: '3 phiên đã khóa bảo vệ tự động',
    affectedCountLabelEn: '3 sessions auto-locked safely',
    runbookHintVi: 'Theo dõi yêu cầu khôi phục PIN qua OTP email nếu người dùng liên hệ Hỗ trợ.',
    runbookHintEn: 'Monitor OTP PIN recovery requests if affected users contact Support.',
  },
  {
    id: 'alt_903',
    code: 'ALT-903',
    severity: 'warning',
    state: 'acknowledged',
    serviceVi: 'Bộ lập lịch Quỹ nhóm Định kỳ',
    serviceEn: 'Fixed Fund Deadline Evaluator',
    titleVi: '18 kỳ đóng góp Quỹ định kỳ sắp chạm mốc hạn chót 24 giờ',
    titleEn: '18 fixed-fund contribution cycles approaching 24h deadline cutoff',
    detailVi:
      'Hệ thống đã gửi thông báo nhắc hạn tự động tới 46 thành viên chưa hoàn tất nghĩa vụ đóng góp của kỳ hiện tại.',
    detailEn:
      'Automated reminder notifications were dispatched to 46 members with pending cycle obligations.',
    triggeredAt: '03/11/2026 · 08:00',
    affectedCountLabelVi: '18 quỹ nhóm · 46 thành viên',
    affectedCountLabelEn: '18 group funds · 46 members',
    runbookHintVi: 'Không cần can thiệp thủ công; bộ đánh giá hạn chót sẽ tự động khóa sổ đúng giờ.',
    runbookHintEn: 'No manual action required; deadline evaluator will settle cycles on schedule.',
  },
  {
    id: 'alt_904',
    code: 'ALT-904',
    severity: 'info',
    state: 'acknowledged',
    serviceVi: 'Hàng đợi Thông báo Đẩy (Push)',
    serviceEn: 'Push Notification Dispatcher',
    titleVi: 'Đã hoàn tất gửi lại 94 thông báo biến động số dư sau bảo trì ngắn',
    titleEn: 'Completed retry delivery for 94 balance notifications after brief maintenance',
    detailVi:
      'Toàn bộ hàng đợi thông báo tồn đọng trong 3 phút chuyển mạch đã được gửi tới thiết bị người dùng không trùng lặp.',
    detailEn:
      'All queued balance notifications from the 3-minute failover window were delivered idempotently.',
    triggeredAt: '03/11/2026 · 06:30',
    affectedCountLabelVi: '94 thông báo đẩy (100% thành công)',
    affectedCountLabelEn: '94 push events (100% delivered)',
    runbookHintVi: 'Lưu nhật ký vận hành định kỳ ca sáng.',
    runbookHintEn: 'Archive in morning shift operational log.',
  },
];

export type AdminUserAccountStatus = 'active' | 'restricted' | 'suspended' | 'locked';

export interface AdminAssignedSupportCaseScope {
  caseCode: string;
  titleVi: string;
  titleEn: string;
  categoryVi: string;
  categoryEn: string;
  assignedOperator: string;
  openedAt: string;
}

export interface AdminUserSessionFixture {
  id: string;
  deviceLabel: string;
  platform: 'iOS' | 'Android' | 'Web';
  locationLabel: string;
  ipMasked: string;
  lastActiveAt: string;
  isTrustedBiometric: boolean;
  status: 'active' | 'revoked';
  revokedAt?: string;
  revokedReason?: string;
}

export interface AdminUserGroupMembershipFixture {
  fundId: string;
  fundName: string;
  fundTypeVi: string;
  fundTypeEn: string;
  roleVi: string;
  roleEn: string;
  obligationStateVi: string;
  obligationStateEn: string;
}

export interface AdminUserRelationshipFixture {
  connectedFriendsCount: number;
  closeFriendsCount: number;
  pendingFriendRequestsCount: number;
  groupFunds: AdminUserGroupMembershipFixture[];
}

export interface AdminUserAuditEntry {
  id: string;
  referenceCode: string;
  timestamp: string;
  actionLabelVi: string;
  actionLabelEn: string;
  detailVi: string;
  detailEn: string;
  reason: string;
  operator: string;
}

export interface AdminUserDirectoryEntry {
  id: string;
  spendId: string;
  fullName: string;
  emailMasked: string;
  phoneMasked: string;
  accountStatus: AdminUserAccountStatus;
  kycTierVi: string;
  kycTierEn: string;
  joinedAt: string;
  lastActiveAt: string;
  pinConfigured: boolean;
  biometricEnrolled: boolean;
  passwordResetInitiatedAt?: string;
  pinResetInitiatedAt?: string;
  assignedSupportCase: AdminAssignedSupportCaseScope | null;
  scopedProfile: {
    occupationVi: string;
    occupationEn: string;
    activeWalletsCount: number;
    linkedBanksCount: number;
    securityNoteVi: string;
    securityNoteEn: string;
  };
  sessions: AdminUserSessionFixture[];
  relationships: AdminUserRelationshipFixture;
  auditLog: AdminUserAuditEntry[];
}

export const ADMIN_USER_DIRECTORY_FIXTURES: AdminUserDirectoryEntry[] = [
  {
    id: 'usr_admin_01',
    spendId: 'SW-MINH-9921',
    fullName: 'Nguyễn Nhật Minh',
    emailMasked: 'minh.ng***@spendwise.vn',
    phoneMasked: '0903 ••• 921',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '12/03/2025',
    lastActiveAt: '03/11/2026 · 10:14',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: {
      caseCode: 'SUP-2026-801',
      titleVi: 'Đối soát lệnh nạp ví Vietcombank 15.000.000 ₫ thiếu hậu tố mã tham chiếu',
      titleEn: 'Reconcile 15,000,000 VND Vietcombank top-up missing reference suffix',
      categoryVi: 'Đối soát Nạp/Rút',
      categoryEn: 'Top-up & Settlement',
      assignedOperator: 'Trần Hoàng Nam · Ca 1',
      openedAt: '03/11/2026 · 09:15',
    },
    scopedProfile: {
      occupationVi: 'Kỹ sư phần mềm · Nhân viên văn phòng',
      occupationEn: 'Software Engineer · Office Worker',
      activeWalletsCount: 3,
      linkedBanksCount: 2,
      securityNoteVi: 'Thiết bị chính đã đăng ký Face ID; không có cảnh báo đăng nhập lạ.',
      securityNoteEn: 'Primary device enrolled with Face ID; no anomalous login flags.',
    },
    sessions: [
      {
        id: 'ses_minh_01',
        deviceLabel: 'iPhone 16 Pro · iOS 19.1',
        platform: 'iOS',
        locationLabel: 'TP. Hồ Chí Minh, VN',
        ipMasked: '113.161.•••.42',
        lastActiveAt: '03/11/2026 · 10:14',
        isTrustedBiometric: true,
        status: 'active',
      },
      {
        id: 'ses_minh_02',
        deviceLabel: 'MacBook Pro M3 · Safari 18',
        platform: 'Web',
        locationLabel: 'TP. Hồ Chí Minh, VN',
        ipMasked: '113.161.•••.42',
        lastActiveAt: '02/11/2026 · 21:30',
        isTrustedBiometric: false,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 14,
      closeFriendsCount: 5,
      pendingFriendRequestsCount: 1,
      groupFunds: [
        {
          fundId: 'grp_family_vn',
          fundName: 'Quỹ Gia đình Việt',
          fundTypeVi: 'Quỹ định kỳ (Fixed)',
          fundTypeEn: 'Fixed Cycle Fund',
          roleVi: 'Chủ quỹ',
          roleEn: 'Fund Owner',
          obligationStateVi: 'Đã hoàn tất kỳ 10/2026',
          obligationStateEn: 'Oct 2026 cycle settled',
        },
        {
          fundId: 'grp_dalat_trip',
          fundName: 'Quỹ Du lịch Đà Lạt',
          fundTypeVi: 'Quỹ tự do (Free)',
          fundTypeEn: 'Free Shared Fund',
          roleVi: 'Thành viên · Có quyền chi',
          roleEn: 'Member · Spending enabled',
          obligationStateVi: 'Đang hoạt động',
          obligationStateEn: 'Active member',
        },
      ],
    },
    auditLog: [
      {
        id: 'aud_init_01',
        referenceCode: 'AUD-USR-20261103-0915',
        timestamp: '03/11/2026 · 09:15',
        actionLabelVi: 'Cấp quyền xem hồ sơ theo Case hỗ trợ',
        actionLabelEn: 'Scoped profile access granted via Support Case',
        detailVi: 'Kích hoạt phạm vi truy cập theo hồ sơ SUP-2026-801',
        detailEn: 'Activated least-privilege inspection scope under SUP-2026-801',
        reason: 'Rà soát lịch sử phiên và liên kết ngân hàng để khớp lệnh nạp 15tr.',
        operator: 'Trần Hoàng Nam · Ca 1',
      },
    ],
  },
  {
    id: 'usr_admin_02',
    spendId: 'SW-NGOC-4410',
    fullName: 'Trần Bảo Ngọc',
    emailMasked: 'ngoc.tr***@spendwise.vn',
    phoneMasked: '0918 ••• 410',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '18/04/2025',
    lastActiveAt: '03/11/2026 · 09:50',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: {
      caseCode: 'SUP-2026-804',
      titleVi: 'Thẩm định chứng từ khiếu nại khoản chi 24h tại Quỹ Du lịch Đà Lạt',
      titleEn: 'Verify receipt evidence for 24h spend dispute in Da Lat Trip Fund',
      categoryVi: 'Khiếu nại Quỹ nhóm',
      categoryEn: 'Group Fund Dispute',
      assignedOperator: 'Lê Thu Hà · Quản trị Rủi ro',
      openedAt: '03/11/2026 · 08:40',
    },
    scopedProfile: {
      occupationVi: 'Chuyên viên Thiết kế · Freelancer',
      occupationEn: 'Product Designer · Freelancer',
      activeWalletsCount: 2,
      linkedBanksCount: 1,
      securityNoteVi: 'Xác thực vân tay Touch ID đang bật cho mọi giao dịch quỹ nhóm.',
      securityNoteEn: 'Touch ID biometric enabled for all group fund disbursements.',
    },
    sessions: [
      {
        id: 'ses_ngoc_01',
        deviceLabel: 'iPhone 15 · iOS 18.4',
        platform: 'iOS',
        locationLabel: 'TP. Đà Nẵng, VN',
        ipMasked: '14.169.•••.88',
        lastActiveAt: '03/11/2026 · 09:50',
        isTrustedBiometric: true,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 11,
      closeFriendsCount: 4,
      pendingFriendRequestsCount: 0,
      groupFunds: [
        {
          fundId: 'grp_dalat_trip',
          fundName: 'Quỹ Du lịch Đà Lạt',
          fundTypeVi: 'Quỹ tự do (Free)',
          fundTypeEn: 'Free Shared Fund',
          roleVi: 'Chủ quỹ',
          roleEn: 'Fund Owner',
          obligationStateVi: '1 khoản chi 24h đang rà soát chứng từ',
          obligationStateEn: '1 24h spend under receipt review',
        },
      ],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_03',
    spendId: 'SW-KHANH-7302',
    fullName: 'Lê Quốc Khánh',
    emailMasked: 'khanh.le***@gmail.com',
    phoneMasked: '0987 ••• 302',
    accountStatus: 'restricted',
    kycTierVi: 'eKYC Cấp 2 · Đã xác thực',
    kycTierEn: 'eKYC Tier 2 · Verified',
    joinedAt: '05/06/2025',
    lastActiveAt: '03/11/2026 · 09:41',
    pinConfigured: true,
    biometricEnrolled: false,
    assignedSupportCase: {
      caseCode: 'SUP-2026-809',
      titleVi: 'Yêu cầu mở khóa hạn chế chiều rút và đặt lại mã PIN sau 3 lần nhập sai',
      titleEn: 'Request to lift outflow restriction and reset PIN after 3 failed attempts',
      categoryVi: 'Bảo mật & Mã PIN',
      categoryEn: 'Security & PIN Recovery',
      assignedOperator: 'Phạm Minh Quân · Risk Ops',
      openedAt: '03/11/2026 · 09:45',
    },
    scopedProfile: {
      occupationVi: 'Kinh doanh tự do',
      occupationEn: 'Small Business Owner',
      activeWalletsCount: 2,
      linkedBanksCount: 2,
      securityNoteVi: 'Đang tạm hạn chế chiều chuyển ra do nhập sai PIN 3 lần trên trình duyệt mới.',
      securityNoteEn: 'Outflow temporarily restricted due to 3 failed PIN attempts on a new browser.',
    },
    sessions: [
      {
        id: 'ses_khanh_01',
        deviceLabel: 'Samsung Galaxy S24 · Android 15',
        platform: 'Android',
        locationLabel: 'Hà Nội, VN',
        ipMasked: '27.72.•••.19',
        lastActiveAt: '03/11/2026 · 09:30',
        isTrustedBiometric: false,
        status: 'active',
      },
      {
        id: 'ses_khanh_02',
        deviceLabel: 'Windows 11 · Chrome 131 (Trình duyệt lạ)',
        platform: 'Web',
        locationLabel: 'Hải Phòng, VN',
        ipMasked: '171.224.•••.91',
        lastActiveAt: '03/11/2026 · 09:41',
        isTrustedBiometric: false,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 7,
      closeFriendsCount: 2,
      pendingFriendRequestsCount: 2,
      groupFunds: [
        {
          fundId: 'grp_football_hn',
          fundName: 'Quỹ CLB Bóng đá Nội bộ',
          fundTypeVi: 'Quỹ định kỳ (Fixed)',
          fundTypeEn: 'Fixed Cycle Fund',
          roleVi: 'Thành viên',
          roleEn: 'Member',
          obligationStateVi: 'Đã đóng góp kỳ 11/2026',
          obligationStateEn: 'Nov 2026 cycle paid',
        },
      ],
    },
    auditLog: [
      {
        id: 'aud_init_03',
        referenceCode: 'AUD-SEC-20261103-0942',
        timestamp: '03/11/2026 · 09:42',
        actionLabelVi: 'Tự động hạn chế chiều chuyển tiền ra',
        actionLabelEn: 'Automatic outflow restriction triggered',
        detailVi: 'Phát hiện 3 lần nhập sai mã PIN giao dịch liên tiếp',
        detailEn: '3 consecutive failed transaction PIN attempts detected',
        reason: 'Quy tắc bảo vệ tự động ALT-902',
        operator: 'Hệ thống Bảo mật PIN',
      },
    ],
  },
  {
    id: 'usr_admin_04',
    spendId: 'SW-THAO-5188',
    fullName: 'Phạm Phương Thảo',
    emailMasked: 'thao.ph***@outlook.com',
    phoneMasked: '0932 ••• 188',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '22/07/2025',
    lastActiveAt: '02/11/2026 · 20:10',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: null,
    scopedProfile: {
      occupationVi: 'Giảng viên đại học',
      occupationEn: 'University Lecturer',
      activeWalletsCount: 2,
      linkedBanksCount: 1,
      securityNoteVi: 'Tài khoản hoạt động ổn định.',
      securityNoteEn: 'Account operating normally.',
    },
    sessions: [
      {
        id: 'ses_thao_01',
        deviceLabel: 'iPad Air M2 · iPadOS 18',
        platform: 'iOS',
        locationLabel: 'TP. Hồ Chí Minh, VN',
        ipMasked: '115.78.•••.12',
        lastActiveAt: '02/11/2026 · 20:10',
        isTrustedBiometric: true,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 9,
      closeFriendsCount: 3,
      pendingFriendRequestsCount: 0,
      groupFunds: [],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_05',
    spendId: 'SW-HUY-6044',
    fullName: 'Vũ Gia Huy',
    emailMasked: 'huy.vu***@spendwise.vn',
    phoneMasked: '0974 ••• 044',
    accountStatus: 'suspended',
    kycTierVi: 'eKYC Cấp 1 · Cơ bản',
    kycTierEn: 'eKYC Tier 1 · Basic',
    joinedAt: '14/08/2025',
    lastActiveAt: '01/11/2026 · 14:22',
    pinConfigured: true,
    biometricEnrolled: false,
    assignedSupportCase: {
      caseCode: 'SUP-2026-812',
      titleVi: 'Xác minh nguồn tiền chuyển khoản tần suất cao và yêu cầu mở lại tài khoản',
      titleEn: 'Verify high-frequency P2P transfer velocity and account reinstatement request',
      categoryVi: 'Tuân thủ & Rủi ro',
      categoryEn: 'Compliance & Risk',
      assignedOperator: 'Phạm Minh Quân · Risk Ops',
      openedAt: '02/11/2026 · 11:00',
    },
    scopedProfile: {
      occupationVi: 'Nhân viên kinh doanh',
      occupationEn: 'Sales Representative',
      activeWalletsCount: 1,
      linkedBanksCount: 3,
      securityNoteVi: 'Đang tạm khóa để đối chiếu chứng từ thu hộ nhóm mua chung.',
      securityNoteEn: 'Temporarily suspended pending group-buying collection receipt review.',
    },
    sessions: [
      {
        id: 'ses_huy_01',
        deviceLabel: 'Xiaomi 14 · Android 14',
        platform: 'Android',
        locationLabel: 'Bình Dương, VN',
        ipMasked: '42.116.•••.77',
        lastActiveAt: '01/11/2026 · 14:22',
        isTrustedBiometric: false,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 23,
      closeFriendsCount: 2,
      pendingFriendRequestsCount: 4,
      groupFunds: [
        {
          fundId: 'grp_office_lunch',
          fundName: 'Quỹ Ăn trưa Văn phòng',
          fundTypeVi: 'Quỹ tự do (Free)',
          fundTypeEn: 'Free Shared Fund',
          roleVi: 'Thành viên',
          roleEn: 'Member',
          obligationStateVi: 'Tạm dừng giao dịch quỹ',
          obligationStateEn: 'Fund actions paused',
        },
      ],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_06',
    spendId: 'SW-LINH-2095',
    fullName: 'Đặng Thùy Linh',
    emailMasked: 'linh.da***@gmail.com',
    phoneMasked: '0909 ••• 095',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '01/09/2025',
    lastActiveAt: '03/11/2026 · 08:15',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: null,
    scopedProfile: {
      occupationVi: 'Kế toán trưởng',
      occupationEn: 'Chief Accountant',
      activeWalletsCount: 3,
      linkedBanksCount: 2,
      securityNoteVi: 'Không có bất thường.',
      securityNoteEn: 'No anomalies.',
    },
    sessions: [
      {
        id: 'ses_linh_01',
        deviceLabel: 'iPhone 15 Pro · iOS 18.4',
        platform: 'iOS',
        locationLabel: 'Hà Nội, VN',
        ipMasked: '14.232.•••.54',
        lastActiveAt: '03/11/2026 · 08:15',
        isTrustedBiometric: true,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 16,
      closeFriendsCount: 6,
      pendingFriendRequestsCount: 0,
      groupFunds: [],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_07',
    spendId: 'SW-TUNG-8310',
    fullName: 'Hoàng Sơn Tùng',
    emailMasked: 'tung.ho***@spendwise.vn',
    phoneMasked: '0945 ••• 310',
    accountStatus: 'locked',
    kycTierVi: 'eKYC Cấp 2 · Đã xác thực',
    kycTierEn: 'eKYC Tier 2 · Verified',
    joinedAt: '19/05/2025',
    lastActiveAt: '30/10/2026 · 23:18',
    pinConfigured: true,
    biometricEnrolled: false,
    assignedSupportCase: {
      caseCode: 'SUP-2026-815',
      titleVi: 'Khách hàng báo mất điện thoại, yêu cầu thu hồi phiên và đặt lại mật khẩu + PIN',
      titleEn: 'User reported lost phone; requested session revocation and password + PIN reset',
      categoryVi: 'Khẩn cấp · Mất thiết bị',
      categoryEn: 'Emergency · Lost Device',
      assignedOperator: 'Trần Hoàng Nam · Ca 1',
      openedAt: '31/10/2026 · 07:30',
    },
    scopedProfile: {
      occupationVi: 'Kiến trúc sư',
      occupationEn: 'Architect',
      activeWalletsCount: 2,
      linkedBanksCount: 1,
      securityNoteVi: 'Đã khóa bảo vệ khẩn cấp theo yêu cầu chủ tài khoản; cần thu hồi 2 phiên cũ.',
      securityNoteEn: 'Emergency lock enabled per owner request; 2 old sessions require revocation.',
    },
    sessions: [
      {
        id: 'ses_tung_01',
        deviceLabel: 'iPhone 14 · iOS 18.1 (Thiết bị báo mất)',
        platform: 'iOS',
        locationLabel: 'TP. Nha Trang, VN',
        ipMasked: '113.185.•••.10',
        lastActiveAt: '30/10/2026 · 23:18',
        isTrustedBiometric: true,
        status: 'active',
      },
      {
        id: 'ses_tung_02',
        deviceLabel: 'iPad Mini 6 · iPadOS 18',
        platform: 'iOS',
        locationLabel: 'TP. Hồ Chí Minh, VN',
        ipMasked: '115.79.•••.63',
        lastActiveAt: '29/10/2026 · 19:05',
        isTrustedBiometric: false,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 8,
      closeFriendsCount: 3,
      pendingFriendRequestsCount: 0,
      groupFunds: [
        {
          fundId: 'grp_arch_studio',
          fundName: 'Quỹ Studio Kiến trúc',
          fundTypeVi: 'Quỹ định kỳ (Fixed)',
          fundTypeEn: 'Fixed Cycle Fund',
          roleVi: 'Thành viên',
          roleEn: 'Member',
          obligationStateVi: 'Đã hoàn tất kỳ 10/2026',
          obligationStateEn: 'Oct 2026 cycle settled',
        },
      ],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_08',
    spendId: 'SW-MAI-3904',
    fullName: 'Bùi Ngọc Mai',
    emailMasked: 'mai.bui***@gmail.com',
    phoneMasked: '0966 ••• 904',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '10/09/2025',
    lastActiveAt: '03/11/2026 · 07:45',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: null,
    scopedProfile: {
      occupationVi: 'Dược sĩ',
      occupationEn: 'Pharmacist',
      activeWalletsCount: 2,
      linkedBanksCount: 1,
      securityNoteVi: 'Hoạt động bình thường.',
      securityNoteEn: 'Normal activity.',
    },
    sessions: [
      {
        id: 'ses_mai_01',
        deviceLabel: 'iPhone 15 · iOS 18.4',
        platform: 'iOS',
        locationLabel: 'Cần Thơ, VN',
        ipMasked: '113.173.•••.28',
        lastActiveAt: '03/11/2026 · 07:45',
        isTrustedBiometric: true,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 5,
      closeFriendsCount: 2,
      pendingFriendRequestsCount: 0,
      groupFunds: [],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_09',
    spendId: 'SW-DUC-1127',
    fullName: 'Đỗ Minh Đức',
    emailMasked: 'duc.do***@spendwise.vn',
    phoneMasked: '0912 ••• 127',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 2 · Sinh trắc học',
    kycTierEn: 'eKYC Tier 2 · Biometric',
    joinedAt: '28/03/2025',
    lastActiveAt: '03/11/2026 · 10:02',
    pinConfigured: true,
    biometricEnrolled: true,
    assignedSupportCase: {
      caseCode: 'SUP-2026-819',
      titleVi: 'Kiểm tra biên bản bàn giao quyền Chủ quỹ Gia đình Việt sau bầu cử',
      titleEn: 'Audit post-election ownership handoff for Family Fund',
      categoryVi: 'Quản trị Quỹ nhóm',
      categoryEn: 'Fund Governance',
      assignedOperator: 'Nguyễn Phương Thảo · Governance',
      openedAt: '02/11/2026 · 16:20',
    },
    scopedProfile: {
      occupationVi: 'Quản lý dự án',
      occupationEn: 'Project Manager',
      activeWalletsCount: 2,
      linkedBanksCount: 2,
      securityNoteVi: 'Đã xác thực chữ ký PIN khi nhận bàn giao quyền Chủ quỹ.',
      securityNoteEn: 'PIN signature verified for group fund ownership acceptance.',
    },
    sessions: [
      {
        id: 'ses_duc_01',
        deviceLabel: 'Galaxy Z Fold6 · Android 15',
        platform: 'Android',
        locationLabel: 'Hà Nội, VN',
        ipMasked: '27.68.•••.104',
        lastActiveAt: '03/11/2026 · 10:02',
        isTrustedBiometric: true,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 19,
      closeFriendsCount: 6,
      pendingFriendRequestsCount: 1,
      groupFunds: [
        {
          fundId: 'grp_family_vn',
          fundName: 'Quỹ Gia đình Việt',
          fundTypeVi: 'Quỹ định kỳ (Fixed)',
          fundTypeEn: 'Fixed Cycle Fund',
          roleVi: 'Đồng sáng lập · Thành viên',
          roleEn: 'Co-founder · Member',
          obligationStateVi: 'Đã hoàn tất kỳ 10/2026',
          obligationStateEn: 'Oct 2026 cycle settled',
        },
      ],
    },
    auditLog: [],
  },
  {
    id: 'usr_admin_10',
    spendId: 'SW-VY-9503',
    fullName: 'Trương Hạ Vy',
    emailMasked: 'vy.truong***@gmail.com',
    phoneMasked: '0938 ••• 503',
    accountStatus: 'active',
    kycTierVi: 'eKYC Cấp 1 · Cơ bản',
    kycTierEn: 'eKYC Tier 1 · Basic',
    joinedAt: '15/10/2026',
    lastActiveAt: '02/11/2026 · 18:12',
    pinConfigured: false,
    biometricEnrolled: false,
    assignedSupportCase: null,
    scopedProfile: {
      occupationVi: 'Sinh viên đại học',
      occupationEn: 'University Student',
      activeWalletsCount: 1,
      linkedBanksCount: 0,
      securityNoteVi: 'Tài khoản mới tạo, chưa thiết lập mã PIN giao dịch.',
      securityNoteEn: 'Newly registered account; transaction PIN not yet configured.',
    },
    sessions: [
      {
        id: 'ses_vy_01',
        deviceLabel: 'iPhone 13 · iOS 18.2',
        platform: 'iOS',
        locationLabel: 'TP. Hồ Chí Minh, VN',
        ipMasked: '115.74.•••.39',
        lastActiveAt: '02/11/2026 · 18:12',
        isTrustedBiometric: false,
        status: 'active',
      },
    ],
    relationships: {
      connectedFriendsCount: 3,
      closeFriendsCount: 1,
      pendingFriendRequestsCount: 0,
      groupFunds: [],
    },
    auditLog: [],
  },
];

// ============================================================================
// ADMIN A3 — TRANSACTIONS & LEDGER, SYNTHETIC WALLETS, AUDIT LOG & SYSTEM CONFIG
// ============================================================================

export type AdminLedgerTxType =
  | 'p2p_transfer'
  | 'bank_topup'
  | 'bank_withdrawal'
  | 'fund_contribution'
  | 'fund_disbursement'
  | 'goal_allocation';

export type AdminLedgerTxStatus =
  | 'settled_demo'
  | 'held_reservation'
  | 'pending_reconcile'
  | 'reversed_demo';

export interface AdminLedgerTransactionEntry {
  id: string;
  txCode: string;
  correlationId: string;
  idempotencyKeyMasked: string;
  createdAt: string;
  type: AdminLedgerTxType;
  typeLabelVi: string;
  typeLabelEn: string;
  status: AdminLedgerTxStatus;
  statusLabelVi: string;
  statusLabelEn: string;
  direction: 'credit' | 'debit';
  amountVnd: number;
  feeVnd: number;
  walletId: string;
  walletCode: string;
  ownerName: string;
  ownerSpendId: string;
  counterpartyLabelVi: string;
  counterpartyLabelEn: string;
  debitAccountCode: string;
  creditAccountCode: string;
  preBalanceVnd: number;
  postBalanceVnd: number;
  authMethodVi: string;
  authMethodEn: string;
  descriptionVi: string;
  descriptionEn: string;
  reconciliationNoteVi: string;
  reconciliationNoteEn: string;
}

export type AdminSyntheticWalletState = 'active' | 'frozen';

export interface AdminSyntheticWalletLedgerItem {
  id: string;
  walletCode: string;
  walletNameVi: string;
  walletNameEn: string;
  walletKindVi: string;
  walletKindEn: string;
  ownerName: string;
  ownerSpendId: string;
  state: AdminSyntheticWalletState;
  availableBalanceVnd: number;
  heldBalanceVnd: number;
  totalLedgerBalanceVnd: number;
  journalComputedBalanceVnd: number;
  varianceVnd: number;
  lastReconciledAt: string;
  statusReasonVi: string;
  statusReasonEn: string;
}

export interface AdminGlobalAuditEvent {
  id: string;
  referenceCode: string;
  timestamp: string;
  module: 'ledger' | 'users' | 'system' | 'support';
  moduleLabelVi: string;
  moduleLabelEn: string;
  actionTitleVi: string;
  actionTitleEn: string;
  targetEntity: string;
  detailVi: string;
  detailEn: string;
  reason: string;
  operator: string;
}

export interface AdminSystemConfigParameter {
  id: string;
  keyName: string;
  category: 'ledger_invariants' | 'velocity_risk' | 'integrations_security';
  categoryLabelVi: string;
  categoryLabelEn: string;
  labelVi: string;
  labelEn: string;
  valueDisplay: string;
  isRedactedSecret?: boolean;
  descriptionVi: string;
  descriptionEn: string;
  lastVerifiedAt: string;
}

export const ADMIN_SYNTHETIC_WALLETS_FIXTURES: AdminSyntheticWalletLedgerItem[] = [
  {
    id: 'wal_syn_01',
    walletCode: 'WAL-MINH-MAIN',
    walletNameVi: 'Ví Chi tiêu Chính (Nguyễn Nhật Minh)',
    walletNameEn: 'Primary Daily Wallet (Nguyen Nhat Minh)',
    walletKindVi: 'Ví cá nhân · Thanh toán',
    walletKindEn: 'Personal · Daily Spend',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    state: 'active',
    availableBalanceVnd: 44_970_000,
    heldBalanceVnd: 1_500_000,
    totalLedgerBalanceVnd: 46_470_000,
    journalComputedBalanceVnd: 46_470_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 10:15',
    statusReasonVi: 'Sổ cái khớp 100% với tổng bút toán Nợ/Có.',
    statusReasonEn: 'Ledger balance matches 100% of debit/credit journal entries.',
  },
  {
    id: 'wal_syn_02',
    walletCode: 'WAL-MINH-SAVE',
    walletNameVi: 'Ví Tiết kiệm Dự phòng (Nguyễn Nhật Minh)',
    walletNameEn: 'Emergency Savings Wallet (Nguyen Nhat Minh)',
    walletKindVi: 'Ví cá nhân · Tiết kiệm',
    walletKindEn: 'Personal · Savings',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    state: 'active',
    availableBalanceVnd: 68_000_000,
    heldBalanceVnd: 12_000_000,
    totalLedgerBalanceVnd: 80_000_000,
    journalComputedBalanceVnd: 80_000_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 10:15',
    statusReasonVi: 'Bao gồm 12.000.000 ₫ đang phân bổ mục tiêu tiết kiệm.',
    statusReasonEn: 'Includes 12,000,000 VND allocated to active savings goals.',
  },
  {
    id: 'wal_syn_03',
    walletCode: 'WAL-NGOC-MAIN',
    walletNameVi: 'Ví Cá nhân Chính (Trần Bảo Ngọc)',
    walletNameEn: 'Primary Personal Wallet (Tran Bao Ngoc)',
    walletKindVi: 'Ví cá nhân · Thanh toán',
    walletKindEn: 'Personal · Daily Spend',
    ownerName: 'Trần Bảo Ngọc',
    ownerSpendId: 'SW-NGOC-4410',
    state: 'active',
    availableBalanceVnd: 26_265_000,
    heldBalanceVnd: 0,
    totalLedgerBalanceVnd: 26_265_000,
    journalComputedBalanceVnd: 26_265_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 10:10',
    statusReasonVi: 'Đối soát tự động khớp hoàn toàn.',
    statusReasonEn: 'Automated reconciliation matched completely.',
  },
  {
    id: 'wal_syn_04',
    walletCode: 'WAL-KHANH-MAIN',
    walletNameVi: 'Ví Cá nhân Chính (Lê Quốc Khánh)',
    walletNameEn: 'Primary Personal Wallet (Le Quoc Khanh)',
    walletKindVi: 'Ví cá nhân · Thanh toán',
    walletKindEn: 'Personal · Daily Spend',
    ownerName: 'Lê Quốc Khánh',
    ownerSpendId: 'SW-KHANH-7302',
    state: 'frozen',
    availableBalanceVnd: 19_450_000,
    heldBalanceVnd: 0,
    totalLedgerBalanceVnd: 19_450_000,
    journalComputedBalanceVnd: 19_450_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 09:45',
    statusReasonVi: 'Đang tạm đóng băng chiều rút/chuyển theo hồ sơ hỗ trợ SUP-2026-809.',
    statusReasonEn: 'Outflow frozen pending PIN verification under support case SUP-2026-809.',
  },
  {
    id: 'wal_syn_05',
    walletCode: 'WAL-FUND-DALAT',
    walletNameVi: 'Tài khoản Sổ cái Quỹ Du lịch Đà Lạt',
    walletNameEn: 'Da Lat Trip Group Fund Ledger Account',
    walletKindVi: 'Quỹ nhóm · Tự do (Free)',
    walletKindEn: 'Group Fund · Free Pool',
    ownerName: 'Trần Bảo Ngọc (Chủ quỹ)',
    ownerSpendId: 'GRP-DALAT-02',
    state: 'active',
    availableBalanceVnd: 14_550_000,
    heldBalanceVnd: 2_450_000,
    totalLedgerBalanceVnd: 17_000_000,
    journalComputedBalanceVnd: 17_000_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 10:05',
    statusReasonVi: 'Có 2.450.000 ₫ đang giữ chỗ chờ hết thời gian 24 giờ giải ngân.',
    statusReasonEn: '2,450,000 VND reserved under active 24h disbursement hold window.',
  },
  {
    id: 'wal_syn_06',
    walletCode: 'WAL-TUNG-MAIN',
    walletNameVi: 'Ví Cá nhân Chính (Hoàng Sơn Tùng)',
    walletNameEn: 'Primary Personal Wallet (Hoang Son Tung)',
    walletKindVi: 'Ví cá nhân · Thanh toán',
    walletKindEn: 'Personal · Daily Spend',
    ownerName: 'Hoàng Sơn Tùng',
    ownerSpendId: 'SW-TUNG-8310',
    state: 'frozen',
    availableBalanceVnd: 31_800_000,
    heldBalanceVnd: 0,
    totalLedgerBalanceVnd: 31_800_000,
    journalComputedBalanceVnd: 31_800_000,
    varianceVnd: 0,
    lastReconciledAt: '03/11/2026 · 08:00',
    statusReasonVi: 'Đóng băng bảo vệ số dư do chủ tài khoản báo mất thiết bị (SUP-2026-815).',
    statusReasonEn: 'Frozen to protect balance following lost-device report (SUP-2026-815).',
  },
];

export const ADMIN_LEDGER_TRANSACTIONS_FIXTURES: AdminLedgerTransactionEntry[] = [
  {
    id: 'ltx_01',
    txCode: 'TX-20261103-901',
    correlationId: 'CORR-TOPUP-VCB-8841',
    idempotencyKeyMasked: 'IDEM-9921-••••-8841',
    createdAt: '03/11/2026 · 09:14',
    type: 'bank_topup',
    typeLabelVi: 'Nạp ví từ Ngân hàng',
    typeLabelEn: 'Bank Wallet Top-Up',
    status: 'pending_reconcile',
    statusLabelVi: 'Chờ khớp mã tham chiếu',
    statusLabelEn: 'Pending Reference Match',
    direction: 'credit',
    amountVnd: 15_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_01',
    walletCode: 'WAL-MINH-MAIN',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    counterpartyLabelVi: 'Vietcombank Napas 247 · Đuôi 9921',
    counterpartyLabelEn: 'Vietcombank Napas 247 · Ending 9921',
    debitAccountCode: 'GL-NOSTRO-VCB-247',
    creditAccountCode: 'LIAB-WAL-MINH-MAIN',
    preBalanceVnd: 44_970_000,
    postBalanceVnd: 59_970_000,
    authMethodVi: 'Webhook Ngân hàng + Đối soát viên',
    authMethodEn: 'Bank Webhook + Operator Match',
    descriptionVi: 'Nạp tiền vào ví chính từ tài khoản Vietcombank liên kết (thiếu hậu tố SWTOPUP).',
    descriptionEn: 'Top-up to primary wallet from linked Vietcombank account (missing SWTOPUP suffix).',
    reconciliationNoteVi: 'Đang chờ xác nhận khớp lệnh tại tác vụ TOPUP-8841 (chỉ mô phỏng DEMO).',
    reconciliationNoteEn: 'Awaiting manual reference match in task TOPUP-8841 (DEMO simulation only).',
  },
  {
    id: 'ltx_02',
    txCode: 'TX-20261103-887',
    correlationId: 'CORR-FUND-DL-4092',
    idempotencyKeyMasked: 'IDEM-4410-••••-4092',
    createdAt: '03/11/2026 · 08:35',
    type: 'fund_disbursement',
    typeLabelVi: 'Giải ngân Quỹ nhóm (24h)',
    typeLabelEn: 'Group Fund 24h Disbursement',
    status: 'held_reservation',
    statusLabelVi: 'Đang giữ chỗ 24 giờ',
    statusLabelEn: '24h Reservation Hold',
    direction: 'debit',
    amountVnd: 2_450_000,
    feeVnd: 0,
    walletId: 'wal_syn_05',
    walletCode: 'WAL-FUND-DALAT',
    ownerName: 'Quỹ Du lịch Đà Lạt',
    ownerSpendId: 'GRP-DALAT-02',
    counterpartyLabelVi: 'Đặt cọc Homestay Thông Reo · Trần Bảo Ngọc',
    counterpartyLabelEn: 'Thong Reo Homestay Deposit · Tran Bao Ngoc',
    debitAccountCode: 'LIAB-FUND-DALAT-AVAIL',
    creditAccountCode: 'LIAB-FUND-DALAT-HELD24H',
    preBalanceVnd: 17_000_000,
    postBalanceVnd: 14_550_000,
    authMethodVi: 'Mã PIN 6 số + Quyền chi tiêu quỹ',
    authMethodEn: '6-digit PIN + Fund spending permission',
    descriptionVi: 'Tạm giữ 2.450.000 ₫ trong cửa sổ 24 giờ trước khi giải ngân hoàn tất.',
    descriptionEn: 'Reserved 2,450,000 VND in 24-hour review window prior to final disbursement.',
    reconciliationNoteVi: 'Số tiền nằm trong tài khoản tạm giữ nội bộ, chưa phát sinh chuyển ra ngoài.',
    reconciliationNoteEn: 'Funds remain in internal escrow reservation account; not yet disbursed externally.',
  },
  {
    id: 'ltx_03',
    txCode: 'TX-20261102-764',
    correlationId: 'CORR-P2P-20261019-1015',
    idempotencyKeyMasked: 'IDEM-9921-••••-1015',
    createdAt: '02/11/2026 · 19:45',
    type: 'p2p_transfer',
    typeLabelVi: 'Chuyển tiền P2P nội bộ',
    typeLabelEn: 'Internal P2P Transfer',
    status: 'settled_demo',
    statusLabelVi: 'Hoàn tất sổ cái (DEMO)',
    statusLabelEn: 'Ledger Settled (DEMO)',
    direction: 'debit',
    amountVnd: 1_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_01',
    walletCode: 'WAL-MINH-MAIN',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    counterpartyLabelVi: 'Trần Bảo Ngọc (SW-NGOC-4410)',
    counterpartyLabelEn: 'Tran Bao Ngoc (SW-NGOC-4410)',
    debitAccountCode: 'LIAB-WAL-MINH-MAIN',
    creditAccountCode: 'LIAB-WAL-NGOC-MAIN',
    preBalanceVnd: 45_970_000,
    postBalanceVnd: 44_970_000,
    authMethodVi: 'Sinh trắc học Face ID + Mã PIN',
    authMethodEn: 'Face ID Biometric + PIN',
    descriptionVi: 'Chuyển khoản chia tiền ăn tối cuối tuần qua mã QR SpendID.',
    descriptionEn: 'Weekend dinner split transfer via SpendID QR.',
    reconciliationNoteVi: 'Bút toán kép Nợ/Có khớp tức thời cùng mã CORR-P2P-20261019-1015.',
    reconciliationNoteEn: 'Double-entry debit/credit matched atomically under CORR-P2P-20261019-1015.',
  },
  {
    id: 'ltx_04',
    txCode: 'TX-20261102-765',
    correlationId: 'CORR-P2P-20261019-1015',
    idempotencyKeyMasked: 'IDEM-4410-••••-1015',
    createdAt: '02/11/2026 · 19:45',
    type: 'p2p_transfer',
    typeLabelVi: 'Nhận tiền P2P nội bộ',
    typeLabelEn: 'Internal P2P Credit',
    status: 'settled_demo',
    statusLabelVi: 'Hoàn tất sổ cái (DEMO)',
    statusLabelEn: 'Ledger Settled (DEMO)',
    direction: 'credit',
    amountVnd: 1_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_03',
    walletCode: 'WAL-NGOC-MAIN',
    ownerName: 'Trần Bảo Ngọc',
    ownerSpendId: 'SW-NGOC-4410',
    counterpartyLabelVi: 'Nguyễn Nhật Minh (SW-MINH-9921)',
    counterpartyLabelEn: 'Nguyen Nhat Minh (SW-MINH-9921)',
    debitAccountCode: 'LIAB-WAL-MINH-MAIN',
    creditAccountCode: 'LIAB-WAL-NGOC-MAIN',
    preBalanceVnd: 25_265_000,
    postBalanceVnd: 26_265_000,
    authMethodVi: 'Đối ứng tự động P2P',
    authMethodEn: 'Automated P2P Counter-Entry',
    descriptionVi: 'Nhận tiền chuyển khoản nội bộ từ Nguyễn Nhật Minh.',
    descriptionEn: 'Received internal P2P transfer from Nguyen Nhat Minh.',
    reconciliationNoteVi: 'Khớp cặp 100% với bút toán ghi Nợ TX-20261102-764.',
    reconciliationNoteEn: '100% paired with debit entry TX-20261102-764.',
  },
  {
    id: 'ltx_05',
    txCode: 'TX-20261102-610',
    correlationId: 'CORR-FUND-CONTRIB-501',
    idempotencyKeyMasked: 'IDEM-9921-••••-0501',
    createdAt: '02/11/2026 · 14:20',
    type: 'fund_contribution',
    typeLabelVi: 'Đóng góp Quỹ nhóm Định kỳ',
    typeLabelEn: 'Fixed Group Fund Contribution',
    status: 'settled_demo',
    statusLabelVi: 'Hoàn tất sổ cái (DEMO)',
    statusLabelEn: 'Ledger Settled (DEMO)',
    direction: 'debit',
    amountVnd: 2_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_01',
    walletCode: 'WAL-MINH-MAIN',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    counterpartyLabelVi: 'Quỹ Gia đình Việt · Kỳ Tháng 11/2026',
    counterpartyLabelEn: 'Family Fund · Nov 2026 Cycle',
    debitAccountCode: 'LIAB-WAL-MINH-MAIN',
    creditAccountCode: 'LIAB-FUND-FAMILY-VN',
    preBalanceVnd: 47_970_000,
    postBalanceVnd: 45_970_000,
    authMethodVi: 'Mã PIN 6 số',
    authMethodEn: '6-digit PIN',
    descriptionVi: 'Chuyển tài sản đóng góp nghĩa vụ định kỳ vào Quỹ Gia đình Việt.',
    descriptionEn: 'Asset transfer for scheduled cycle contribution to Family Fund.',
    reconciliationNoteVi: 'Phân loại chuẩn: Chuyển tài sản vào quỹ, không tính vào chi tiêu tiêu dùng cá nhân.',
    reconciliationNoteEn: 'Classified as fund asset transfer; excluded from personal consumption expenses.',
  },
  {
    id: 'ltx_06',
    txCode: 'TX-20261101-540',
    correlationId: 'CORR-WDR-TCB-7730',
    idempotencyKeyMasked: 'IDEM-9921-••••-7730',
    createdAt: '01/11/2026 · 16:05',
    type: 'bank_withdrawal',
    typeLabelVi: 'Rút tiền về Ngân hàng',
    typeLabelEn: 'Withdrawal to Linked Bank',
    status: 'settled_demo',
    statusLabelVi: 'Hoàn tất sổ cái (DEMO)',
    statusLabelEn: 'Ledger Settled (DEMO)',
    direction: 'debit',
    amountVnd: 5_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_02',
    walletCode: 'WAL-MINH-SAVE',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    counterpartyLabelVi: 'Techcombank · Đuôi 8821',
    counterpartyLabelEn: 'Techcombank · Ending 8821',
    debitAccountCode: 'LIAB-WAL-MINH-SAVE',
    creditAccountCode: 'GL-NOSTRO-TCB-247',
    preBalanceVnd: 85_000_000,
    postBalanceVnd: 80_000_000,
    authMethodVi: 'Sinh trắc học + Mã PIN',
    authMethodEn: 'Biometric + PIN',
    descriptionVi: 'Rút tiền từ Ví Tiết kiệm về tài khoản Techcombank chính chủ.',
    descriptionEn: 'Withdrawal from Savings Wallet to verified Techcombank account.',
    reconciliationNoteVi: 'Bút toán mô phỏng DEMO — không phát sinh lệnh chuyển tiền ngân hàng thực.',
    reconciliationNoteEn: 'DEMO simulation entry — no real external bank settlement executed.',
  },
  {
    id: 'ltx_07',
    txCode: 'TX-20261101-412',
    correlationId: 'CORR-GOAL-ALLOC-309',
    idempotencyKeyMasked: 'IDEM-9921-••••-0309',
    createdAt: '01/11/2026 · 08:00',
    type: 'goal_allocation',
    typeLabelVi: 'Phân bổ Mục tiêu Tiết kiệm',
    typeLabelEn: 'Savings Goal Allocation',
    status: 'held_reservation',
    statusLabelVi: 'Đang giữ chỗ mục tiêu',
    statusLabelEn: 'Goal Reserved Balance',
    direction: 'debit',
    amountVnd: 3_000_000,
    feeVnd: 0,
    walletId: 'wal_syn_02',
    walletCode: 'WAL-MINH-SAVE',
    ownerName: 'Nguyễn Nhật Minh',
    ownerSpendId: 'SW-MINH-9921',
    counterpartyLabelVi: 'Mục tiêu: Quỹ Mua MacBook M4',
    counterpartyLabelEn: 'Goal: MacBook M4 Savings Target',
    debitAccountCode: 'LIAB-WAL-MINH-SAVE-AVAIL',
    creditAccountCode: 'LIAB-WAL-MINH-SAVE-HELD',
    preBalanceVnd: 71_000_000,
    postBalanceVnd: 68_000_000,
    authMethodVi: 'Quy tắc trích tự động đầu tháng',
    authMethodEn: 'Automated Monthly Goal Rule',
    descriptionVi: 'Khóa số dư khả dụng chuyển sang ngăn giữ chỗ mục tiêu tiết kiệm.',
    descriptionEn: 'Moved liquid balance into dedicated savings goal reservation bucket.',
    reconciliationNoteVi: 'Tổng số dư sổ cái Ví Tiết kiệm không đổi (80.000.000 ₫).',
    reconciliationNoteEn: 'Total savings wallet ledger balance remains unchanged (80,000,000 VND).',
  },
  {
    id: 'ltx_08',
    txCode: 'TX-20261031-319',
    correlationId: 'CORR-WDR-REV-118',
    idempotencyKeyMasked: 'IDEM-7302-••••-0118',
    createdAt: '31/10/2026 · 21:12',
    type: 'bank_withdrawal',
    typeLabelVi: 'Hoàn trả lệnh rút tiền lỗi kết nối',
    typeLabelEn: 'Reversed Failed Bank Withdrawal',
    status: 'reversed_demo',
    statusLabelVi: 'Đã hoàn trả sổ cái',
    statusLabelEn: 'Reversed to Wallet',
    direction: 'credit',
    amountVnd: 4_500_000,
    feeVnd: 0,
    walletId: 'wal_syn_04',
    walletCode: 'WAL-KHANH-MAIN',
    ownerName: 'Lê Quốc Khánh',
    ownerSpendId: 'SW-KHANH-7302',
    counterpartyLabelVi: 'Hoàn trả tự động từ cổng BIDV',
    counterpartyLabelEn: 'Automated Reversal from BIDV Gateway',
    debitAccountCode: 'GL-SUSPENSE-WDR-HOLD',
    creditAccountCode: 'LIAB-WAL-KHANH-MAIN',
    preBalanceVnd: 14_950_000,
    postBalanceVnd: 19_450_000,
    authMethodVi: 'Bộ đối soát tự động (Auto-Reversal)',
    authMethodEn: 'Automated Reconciliation Reversal',
    descriptionVi: 'Hoàn lại 4.500.000 ₫ vào ví khả dụng do cổng ngân hàng hết thời gian chờ.',
    descriptionEn: 'Restored 4,500,000 VND to liquid wallet after gateway timeout.',
    reconciliationNoteVi: 'Đã giải tỏa giữ chỗ và cộng trả nguyên vẹn số dư ví.',
    reconciliationNoteEn: 'Released hold reservation and restored wallet balance in full.',
  },
];

export const ADMIN_INITIAL_GLOBAL_AUDIT_FIXTURES: AdminGlobalAuditEvent[] = [
  {
    id: 'gaud_01',
    referenceCode: 'AUD-LED-20261103-0945',
    timestamp: '03/11/2026 · 09:45',
    module: 'ledger',
    moduleLabelVi: 'Sổ cái & Ví',
    moduleLabelEn: 'Ledger & Wallets',
    actionTitleVi: 'Đóng băng chiều rút/chuyển ví WAL-KHANH-MAIN',
    actionTitleEn: 'Froze outflow on wallet WAL-KHANH-MAIN',
    targetEntity: 'WAL-KHANH-MAIN · Lê Quốc Khánh (SW-KHANH-7302)',
    detailVi: 'Chuyển trạng thái ví từ Hoạt động sang Đang đóng băng (DEMO)',
    detailEn: 'Transitioned synthetic wallet state from Active to Frozen (DEMO)',
    reason: 'Bảo vệ số dư sau 3 lần nhập sai mã PIN giao dịch trên trình duyệt lạ (SUP-2026-809).',
    operator: 'Phạm Minh Quân · Risk Ops',
  },
  {
    id: 'gaud_02',
    referenceCode: 'AUD-USR-20261103-0915',
    timestamp: '03/11/2026 · 09:15',
    module: 'users',
    moduleLabelVi: 'Bảo mật Người dùng',
    moduleLabelEn: 'User Security',
    actionTitleVi: 'Mở quyền truy cập hồ sơ theo Case hỗ trợ SUP-2026-801',
    actionTitleEn: 'Scoped profile inspection enabled under SUP-2026-801',
    targetEntity: 'Nguyễn Nhật Minh (SW-MINH-9921)',
    detailVi: 'Tra cứu phiên đăng nhập và liên kết ngân hàng phục vụ đối soát nạp ví',
    detailEn: 'Inspected device sessions and bank links for top-up reconciliation',
    reason: 'Khách hàng gửi biên lai nạp 15.000.000 ₫ thiếu hậu tố mã tham chiếu.',
    operator: 'Trần Hoàng Nam · Ca 1',
  },
  {
    id: 'gaud_03',
    referenceCode: 'AUD-SYS-20261103-0800',
    timestamp: '03/11/2026 · 08:00',
    module: 'system',
    moduleLabelVi: 'Cấu hình Hệ thống',
    moduleLabelEn: 'System Config',
    actionTitleVi: 'Xác nhận định kỳ quy tắc bất biến Sổ cái kép (Double-Entry)',
    actionTitleEn: 'Verified Double-Entry Ledger Invariant Ruleset',
    targetEntity: 'LEDGER_INVARIANT_V2 · Ca sáng',
    detailVi: 'Kiểm tra chéo 6 ví mẫu: Tổng chênh lệch đối soát = 0 ₫',
    detailEn: 'Cross-checked 6 synthetic wallets: Net reconciliation variance = 0 VND',
    reason: 'Kiểm toán tự động đầu ca trực vận hành ngày 03/11/2026.',
    operator: 'Hệ thống Đối soát Nội bộ',
  },
];

export const ADMIN_SYSTEM_CONFIG_FIXTURES: AdminSystemConfigParameter[] = [
  {
    id: 'cfg_01',
    keyName: 'ledger.double_entry.enforce_zero_variance',
    category: 'ledger_invariants',
    categoryLabelVi: 'Bất biến Sổ cái & Đối soát',
    categoryLabelEn: 'Ledger & Reconciliation Invariants',
    labelVi: 'Bắt buộc cân bằng bút toán Nợ / Có (Zero-Variance)',
    labelEn: 'Enforce Atomic Debit / Credit Balance',
    valueDisplay: 'ENABLED (Strict Atomic)',
    descriptionVi:
      'Mọi giao dịch P2P, Nạp/Rút và Quỹ nhóm phải ghi nhận cặp bút toán Nợ/Có cân bằng tuyệt đối theo cùng mã correlationId.',
    descriptionEn:
      'All P2P, banking, and group fund mutations must record balanced debit/credit pairs under a shared correlationId.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_02',
    keyName: 'ledger.idempotency.retention_window_hours',
    category: 'ledger_invariants',
    categoryLabelVi: 'Bất biến Sổ cái & Đối soát',
    categoryLabelEn: 'Ledger & Reconciliation Invariants',
    labelVi: 'Thời gian lưu khóa chống trùng lệnh (Idempotency TTL)',
    labelEn: 'Idempotency Key Deduplication Window',
    valueDisplay: '24 giờ (24h)',
    descriptionVi:
      'Ngăn chặn trừ tiền kép khi thiết bị người dùng gửi lại yêu cầu chuyển tiền hoặc giải ngân quỹ do mạng chập chờn.',
    descriptionEn:
      'Prevents duplicate debits when client devices retry transfer or fund disbursement requests.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_03',
    keyName: 'funds.free_pool.disbursement_hold_window_hours',
    category: 'ledger_invariants',
    categoryLabelVi: 'Bất biến Sổ cái & Đối soát',
    categoryLabelEn: 'Ledger & Reconciliation Invariants',
    labelVi: 'Thời gian tạm giữ giải ngân Quỹ tự do (24h Hold)',
    labelEn: 'Free Fund 24-Hour Disbursement Hold Window',
    valueDisplay: '24 giờ · Cho phép khiếu nại chứng từ',
    descriptionVi:
      'Các khoản chi theo quyền chi tiêu tại Quỹ tự do được giữ chỗ 24 giờ để các thành viên minh bạch kiểm tra hóa đơn.',
    descriptionEn:
      'Authorized spends in Free Funds are reserved for 24 hours allowing members to inspect receipts.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_04',
    keyName: 'risk.velocity.single_p2p_limit_vnd',
    category: 'velocity_risk',
    categoryLabelVi: 'Hạn mức & Kiểm soát Rủi ro',
    categoryLabelEn: 'Velocity & Risk Thresholds',
    labelVi: 'Hạn mức tối đa 1 lệnh chuyển P2P nội bộ',
    labelEn: 'Maximum Single Internal P2P Transfer Limit',
    valueDisplay: '50.000.000 ₫ / lệnh',
    descriptionVi:
      'Yêu cầu xác thực mã PIN 6 số hoặc sinh trắc học trên thiết bị tin cậy đối với mọi giao dịch chuyển tiền.',
    descriptionEn:
      'Requires 6-digit PIN or trusted-device biometric verification for every transfer.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_05',
    keyName: 'risk.auth.pin_lockout_policy',
    category: 'velocity_risk',
    categoryLabelVi: 'Hạn mức & Kiểm soát Rủi ro',
    categoryLabelEn: 'Velocity & Risk Thresholds',
    labelVi: 'Chính sách khóa bảo vệ khi nhập sai Mã PIN',
    labelEn: 'Failed Transaction PIN Lockout Policy',
    valueDisplay: 'Tối đa 3 lần sai → Khóa tạm 15 phút',
    descriptionVi:
      'Tự động tạm khóa chiều rút/chuyển và phát cảnh báo tới trung tâm vận hành nếu vượt ngưỡng.',
    descriptionEn:
      'Automatically restricts outflow and emits an operational alert when threshold is exceeded.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_06',
    keyName: 'integration.napas247.webhook_signing_secret',
    category: 'integrations_security',
    categoryLabelVi: 'Tích hợp & Bảo mật Khóa',
    categoryLabelEn: 'Integrations & Secret Hygiene',
    labelVi: 'Khóa ký xác thực Webhook Ngân hàng (Napas 247)',
    labelEn: 'Banking Webhook HMAC Signing Secret',
    valueDisplay: '[REDACTED_SECRET · Không hiển thị trên trình duyệt]',
    isRedactedSecret: true,
    descriptionVi:
      'Khóa bí mật chỉ lưu tại kho khóa máy chủ (KMS) ở môi trường thực; bản trình diễn DEMO chạy 100% dữ liệu giả lập cục bộ.',
    descriptionEn:
      'Signing secrets reside strictly in server KMS in production; this DEMO runs 100% on synthetic local fixtures.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
  {
    id: 'cfg_07',
    keyName: 'integration.otp_dispatcher.provider_credentials',
    category: 'integrations_security',
    categoryLabelVi: 'Tích hợp & Bảo mật Khóa',
    categoryLabelEn: 'Integrations & Secret Hygiene',
    labelVi: 'Thông tin xác thực Cổng gửi OTP & Thông báo đẩy',
    labelEn: 'OTP & Push Notification Provider Credentials',
    valueDisplay: '[REDACTED_SECRET · Zero-Credential Policy]',
    isRedactedSecret: true,
    descriptionVi:
      'Tuyệt đối không lộ khóa API, mã OTP hoặc mật khẩu khôi phục trên giao diện Web Admin.',
    descriptionEn:
      'API keys, OTP codes, and recovery tokens are never exposed in the Web Admin UI.',
    lastVerifiedAt: '03/11/2026 · 08:00',
  },
];

// ============================================================================
// ADMIN A4 — SUPPORT & COMPLAINTS CASE QUEUE, SCOPED FUND DETAILS & REFUND WORKFLOW
// ============================================================================

export const CURRENT_ADMIN_OPERATOR = 'Trần Hoàng Nam · Ca 1';

export type AdminSupportCaseStatus =
  | 'open'
  | 'in_progress'
  | 'awaiting_user_info'
  | 'resolved'
  | 'closed';

export type AdminSupportCaseCategory =
  | 'fund_dispute'
  | 'bank_topup'
  | 'security_pin'
  | 'p2p_refund'
  | 'device_loss';

export type AdminSupportCasePriority = 'critical' | 'high' | 'medium';

export type AdminRefundDecisionState =
  | 'none'
  | 'pending_approval'
  | 'approved_demo'
  | 'rejected_demo';

export type AdminRefundSettlementStatus =
  | 'not_applicable'
  | 'unsettled_pending_decision'
  | 'approved_awaiting_ledger_batch'
  | 'simulated_ledger_credited_no_bank'
  | 'settlement_cancelled';

export interface AdminSupportCaseMessage {
  id: string;
  senderRole: 'user' | 'operator' | 'system';
  senderName: string;
  timestamp: string;
  bodyVi: string;
  bodyEn: string;
  isInfoRequest?: boolean;
}

export interface AdminSupportScopedUserContext {
  userId: string;
  fullName: string;
  spendId: string;
  emailMasked: string;
  phoneMasked: string;
  kycTierVi: string;
  kycTierEn: string;
  walletCode: string;
  walletBalanceVnd: number;
  recentDeviceSummaryVi: string;
  recentDeviceSummaryEn: string;
  securityNoteVi: string;
  securityNoteEn: string;
}

export interface AdminSupportScopedFundContext {
  fundCode: string;
  fundNameVi: string;
  fundNameEn: string;
  fundModeVi: string;
  fundModeEn: string;
  ownerName: string;
  memberCount: number;
  fundAvailableBalanceVnd: number;
  fundHeld24hBalanceVnd: number;
  disputedTxCode: string;
  disputedAmountVnd: number;
  disputedItemSummaryVi: string;
  disputedItemSummaryEn: string;
  receiptVerificationVi: string;
  receiptVerificationEn: string;
  memberStandingSummaryVi: string;
  memberStandingSummaryEn: string;
}

export interface AdminSupportRefundClaim {
  claimCode: string;
  requestedAmountVnd: number;
  targetWalletCode: string;
  sourceTxCode: string;
  claimReasonVi: string;
  claimReasonEn: string;
  decisionState: AdminRefundDecisionState;
  settlementStatus: AdminRefundSettlementStatus;
  decidedByOperator?: string;
  decidedAt?: string;
  decisionReason?: string;
}

export interface AdminSupportCaseEntry {
  id: string;
  caseCode: string;
  createdAt: string;
  updatedAt: string;
  status: AdminSupportCaseStatus;
  priority: AdminSupportCasePriority;
  category: AdminSupportCaseCategory;
  categoryLabelVi: string;
  categoryLabelEn: string;
  subjectVi: string;
  subjectEn: string;
  summaryVi: string;
  summaryEn: string;
  assignedOperator: string | null;
  scopedUser: AdminSupportScopedUserContext;
  scopedFund: AdminSupportScopedFundContext | null;
  refundClaim: AdminSupportRefundClaim | null;
  messages: AdminSupportCaseMessage[];
  resolutionSummaryVi?: string;
  resolutionSummaryEn?: string;
}

export interface AdminSupportReplyTemplate {
  id: string;
  labelVi: string;
  labelEn: string;
  bodyVi: string;
  bodyEn: string;
}

export const ADMIN_SUPPORT_REPLY_TEMPLATES: AdminSupportReplyTemplate[] = [
  {
    id: 'tpl_fund_24h_hold',
    labelVi: 'Mẫu: Giải thích giữ chỗ 24h & đối chiếu quỹ nhóm',
    labelEn: 'Template: 24h Fund Hold & Receipt Review',
    bodyVi:
      'SpendWise đã kiểm tra bút toán giữ chỗ 24 giờ của Quỹ nhóm và đối chiếu hóa đơn đính kèm. Khoản chênh lệch sẽ được xử lý theo quy trình hoàn trả nội bộ trên sổ cái DEMO.',
    bodyEn:
      'SpendWise has inspected the 24-hour Group Fund reservation hold and verified the attached receipt. The variance will be processed under the synthetic ledger refund workflow.',
  },
  {
    id: 'tpl_topup_reconciled',
    labelVi: 'Mẫu: Xác nhận khớp biên lai nạp ví ngân hàng',
    labelEn: 'Template: Bank Top-Up Reference Matched',
    bodyVi:
      'Bộ phận Vận hành đã đối chiếu thành công mã tham chiếu Napas 247 với sao kê ngân hàng của bạn. Yêu cầu ghi Có vào ví đã được phê duyệt trên trạng thái DEMO.',
    bodyEn:
      'Operations has matched your Napas 247 reference code against the bank statement. Your wallet credit approval has been recorded in DEMO state.',
  },
  {
    id: 'tpl_request_receipt_evidence',
    labelVi: 'Mẫu: Yêu cầu bổ sung hóa đơn / chứng từ gốc',
    labelEn: 'Template: Request Itemized Receipt / Statement',
    bodyVi:
      'Vui lòng bổ sung ảnh chụp hóa đơn chi tiết hoặc biên lai chuyển khoản rõ mã tham chiếu trong vòng 24 giờ để bộ phận thẩm định hoàn tất phê duyệt.',
    bodyEn:
      'Please provide a clear photo of the itemized receipt or transfer slip showing the reference code within 24 hours so we can complete review.',
  },
  {
    id: 'tpl_security_freeze_protection',
    labelVi: 'Mẫu: Xác nhận bảo vệ ví & hướng dẫn xác thực',
    labelEn: 'Template: Wallet Protection & Verification Steps',
    bodyVi:
      'Để bảo vệ an toàn số dư, hệ thống đã tạm khóa chiều rút/chuyển và thu hồi các phiên thiết bị lạ. Tuyệt đối không cung cấp mã PIN hay OTP cho bất kỳ ai.',
    bodyEn:
      'To protect your balance, outflow has been temporarily restricted and untrusted sessions revoked. Never share your PIN or OTP with anyone.',
  },
];

export const ADMIN_SUPPORT_CASES_FIXTURES: AdminSupportCaseEntry[] = [
  {
    id: 'scase_01',
    caseCode: 'SUP-2026-804',
    createdAt: '03/11/2026 · 08:40',
    updatedAt: '03/11/2026 · 09:50',
    status: 'in_progress',
    priority: 'high',
    category: 'fund_dispute',
    categoryLabelVi: 'Khiếu nại Quỹ nhóm',
    categoryLabelEn: 'Group Fund Dispute',
    subjectVi:
      'Khiếu nại chênh lệch 350.000 ₫ hóa đơn đặt cọc Homestay tại Quỹ Du lịch Đà Lạt',
    subjectEn:
      'Disputed 350,000 VND homestay deposit variance in Da Lat Trip Group Fund',
    summaryVi:
      'Thành viên quỹ phản ánh khoản giải ngân 2.450.000 ₫ (đang giữ chỗ 24h) bao gồm 350.000 ₫ phụ thu cá nhân ngoài phạm vi thống nhất của nhóm.',
    summaryEn:
      'Fund member reported that the 2,450,000 VND disbursement (in 24h hold) includes 350,000 VND of personal add-on fees outside group agreement.',
    assignedOperator: CURRENT_ADMIN_OPERATOR,
    scopedUser: {
      userId: 'usr_admin_02',
      fullName: 'Trần Bảo Ngọc',
      spendId: 'SW-NGOC-4410',
      emailMasked: 'ngoc.tran•••@gmail.com',
      phoneMasked: '091•••4410',
      kycTierVi: 'eKYC Cấp 2 (Đã xác thực CCCD gắn chip)',
      kycTierEn: 'eKYC Tier 2 (Chip ID Verified)',
      walletCode: 'WAL-NGOC-MAIN',
      walletBalanceVnd: 26_265_000,
      recentDeviceSummaryVi: 'iPhone 15 Pro · iOS 18.1 (Tin cậy · TP.HCM)',
      recentDeviceSummaryEn: 'iPhone 15 Pro · iOS 18.1 (Trusted · HCMC)',
      securityNoteVi: 'Không phát hiện đăng nhập lạ; lịch sử chi tiêu quỹ minh bạch.',
      securityNoteEn: 'No anomalous logins detected; clean fund spending history.',
    },
    scopedFund: {
      fundCode: 'GRP-DALAT-02',
      fundNameVi: 'Quỹ Du lịch Đà Lạt',
      fundNameEn: 'Da Lat Trip Group Fund',
      fundModeVi: 'Quỹ tự do (Free) · Hạn mức quyền chi 5.000.000 ₫/lần',
      fundModeEn: 'Free Pool Fund · 5,000,000 VND spending permission',
      ownerName: 'Trần Bảo Ngọc (Chủ quỹ)',
      memberCount: 5,
      fundAvailableBalanceVnd: 14_550_000,
      fundHeld24hBalanceVnd: 2_450_000,
      disputedTxCode: 'TX-20261103-887',
      disputedAmountVnd: 2_450_000,
      disputedItemSummaryVi:
        'Đặt cọc Homestay Thông Reo 3 ngày 2 đêm (2.100.000 ₫ tiền phòng chung + 350.000 ₫ dịch vụ nâng cấp phòng đơn).',
      disputedItemSummaryEn:
        'Thong Reo Homestay 3D2N deposit (2,100,000 VND shared rooms + 350,000 VND single-room upgrade fee).',
      receiptVerificationVi:
        'Đã đính kèm biên lai gốc của Homestay; chủ quỹ đồng ý hoàn lại 350.000 ₫ phần nâng cấp cá nhân về quỹ chung.',
      receiptVerificationEn:
        'Original homestay receipt attached; fund owner agreed to refund 350,000 VND personal upgrade portion back to the shared pool.',
      memberStandingSummaryVi:
        '5/5 thành viên đã đóng góp đầy đủ (tổng 17.000.000 ₫ trước khi giữ chỗ).',
      memberStandingSummaryEn:
        '5/5 members contributed in full (17,000,000 VND total prior to hold).',
    },
    refundClaim: {
      claimCode: 'CLM-REF-2026-401',
      requestedAmountVnd: 350_000,
      targetWalletCode: 'WAL-FUND-DALAT',
      sourceTxCode: 'TX-20261103-887',
      claimReasonVi:
        'Hoàn trả 350.000 ₫ phí phụ thu phòng đơn cá nhân về lại số dư khả dụng của Quỹ Du lịch Đà Lạt.',
      claimReasonEn:
        'Refund 350,000 VND personal room upgrade fee back to Da Lat Trip Group Fund available balance.',
      decisionState: 'pending_approval',
      settlementStatus: 'unsettled_pending_decision',
    },
    messages: [
      {
        id: 'msg_804_1',
        senderRole: 'user',
        senderName: 'Trần Bảo Ngọc (Chủ quỹ GRP-DALAT-02)',
        timestamp: '03/11/2026 · 08:40',
        bodyVi:
          'Mình vừa thanh toán cọc Homestay 2.450.000 ₫ từ quỹ nhóm, trong đó có 350.000 ₫ nâng cấp phòng riêng của mình. Nhờ bộ phận hỗ trợ điều chỉnh hoàn lại 350.000 ₫ về quỹ chung giúp nhóm.',
        bodyEn:
          'I just paid the 2,450,000 VND homestay deposit from our group fund, which included a 350,000 VND upgrade for my room. Please help approve returning 350,000 VND to the group fund.',
      },
      {
        id: 'msg_804_2',
        senderRole: 'operator',
        senderName: CURRENT_ADMIN_OPERATOR,
        timestamp: '03/11/2026 · 09:50',
        bodyVi:
          'Đã tiếp nhận hồ sơ SUP-2026-804 và kiểm tra hóa đơn Homestay Thông Reo trong cửa sổ giữ chỗ 24 giờ. Đang tiến hành bước phê duyệt hoàn trả 350.000 ₫ trên sổ cái DEMO.',
        bodyEn:
          'Assigned case SUP-2026-804 and verified the Thong Reo Homestay invoice within the 24h hold window. Proceeding with the 350,000 VND DEMO refund approval.',
      },
    ],
  },
  {
    id: 'scase_02',
    caseCode: 'SUP-2026-801',
    createdAt: '03/11/2026 · 09:15',
    updatedAt: '03/11/2026 · 10:05',
    status: 'in_progress',
    priority: 'high',
    category: 'bank_topup',
    categoryLabelVi: 'Đối soát Nạp/Rút Ngân hàng',
    categoryLabelEn: 'Bank Top-Up & Withdrawal',
    subjectVi:
      'Nạp ví 15.000.000 ₫ qua Vietcombank Napas 247 thiếu hậu tố mã tham chiếu SWTOPUP',
    subjectEn:
      '15,000,000 VND Vietcombank Napas 247 top-up missing SWTOPUP reference suffix',
    summaryVi:
      'Khách hàng chuyển khoản nạp ví chính để chuẩn bị đóng Quỹ Gia đình Việt nhưng ghi thiếu hậu tố tự động. Đã đối chiếu khớp sao kê ngân hàng.',
    summaryEn:
      'User transferred 15,000,000 VND to primary wallet to prepare for Family Fund contribution but omitted the auto-match suffix. Bank statement verified.',
    assignedOperator: CURRENT_ADMIN_OPERATOR,
    scopedUser: {
      userId: 'usr_admin_01',
      fullName: 'Nguyễn Nhật Minh',
      spendId: 'SW-MINH-9921',
      emailMasked: 'minh.nguyen•••@spendwise.vn',
      phoneMasked: '090•••9921',
      kycTierVi: 'eKYC Cấp 2 (Đã xác thực sinh trắc học)',
      kycTierEn: 'eKYC Tier 2 (Biometric Verified)',
      walletCode: 'WAL-MINH-MAIN',
      walletBalanceVnd: 44_970_000,
      recentDeviceSummaryVi: 'iPhone 16 Pro Max · iOS 18.2 (Tin cậy · TP.HCM)',
      recentDeviceSummaryEn: 'iPhone 16 Pro Max · iOS 18.2 (Trusted · HCMC)',
      securityNoteVi: 'Tài khoản chính chủ hoạt động ổn định, khớp tên chủ thẻ Vietcombank.',
      securityNoteEn: 'Verified account in good standing; matches Vietcombank cardholder name.',
    },
    scopedFund: {
      fundCode: 'GRP-FAMILY-01',
      fundNameVi: 'Quỹ Gia đình Việt',
      fundNameEn: 'Family Fund Vietnam',
      fundModeVi: 'Quỹ định kỳ (Fixed) · Nghĩa vụ 2.000.000 ₫/tháng',
      fundModeEn: 'Fixed Cycle Fund · 2,000,000 VND/month obligation',
      ownerName: 'Nguyễn Nhật Minh (Chủ quỹ)',
      memberCount: 4,
      fundAvailableBalanceVnd: 32_000_000,
      fundHeld24hBalanceVnd: 0,
      disputedTxCode: 'TX-20261103-901',
      disputedAmountVnd: 15_000_000,
      disputedItemSummaryVi:
        'Khoản nạp 15.000.000 ₫ dùng để bổ sung số dư ví chính và duy trì nghĩa vụ đóng góp kỳ Tháng 11/2026.',
      disputedItemSummaryEn:
        '15,000,000 VND top-up intended to replenish primary wallet and maintain Nov 2026 cycle standing.',
      receiptVerificationVi:
        'Biên lai Vietcombank ủy nhiệm chi khớp 100% số tiền và thời điểm 09:14 ngày 03/11/2026.',
      receiptVerificationEn:
        'Vietcombank payment receipt matches 100% on amount and timestamp (09:14 on 03/11/2026).',
      memberStandingSummaryVi:
        'Nguyễn Nhật Minh đã hoàn thành kỳ đóng góp tháng 11 (TX-20261102-610).',
      memberStandingSummaryEn:
        'Nguyen Nhat Minh completed Nov cycle contribution (TX-20261102-610).',
    },
    refundClaim: {
      claimCode: 'CLM-REF-2026-402',
      requestedAmountVnd: 15_000_000,
      targetWalletCode: 'WAL-MINH-MAIN',
      sourceTxCode: 'TX-20261103-901',
      claimReasonVi:
        'Ghi Có bổ sung 15.000.000 ₫ vào ví WAL-MINH-MAIN sau khi khớp thủ công mã tham chiếu CORR-TOPUP-VCB-8841.',
      claimReasonEn:
        'Credit 15,000,000 VND to WAL-MINH-MAIN after manual match of reference CORR-TOPUP-VCB-8841.',
      decisionState: 'approved_demo',
      settlementStatus: 'approved_awaiting_ledger_batch',
      decidedByOperator: CURRENT_ADMIN_OPERATOR,
      decidedAt: '03/11/2026 · 10:05',
      decisionReason:
        'Đã đối chiếu khớp ủy nhiệm chi Vietcombank đuôi 9921 với bút toán chờ CORR-TOPUP-VCB-8841.',
    },
    messages: [
      {
        id: 'msg_801_1',
        senderRole: 'user',
        senderName: 'Nguyễn Nhật Minh (SW-MINH-9921)',
        timestamp: '03/11/2026 · 09:15',
        bodyVi:
          'Mình vừa chuyển 15.000.000 ₫ từ Vietcombank vào ví nhưng quên ghi hậu tố SWTOPUP. Mình gửi kèm ảnh chụp biên lai giao dịch thành công.',
        bodyEn:
          'I transferred 15,000,000 VND from Vietcombank to my wallet but forgot the SWTOPUP suffix. Attached is the successful transfer receipt.',
      },
      {
        id: 'msg_801_2',
        senderRole: 'operator',
        senderName: CURRENT_ADMIN_OPERATOR,
        timestamp: '03/11/2026 · 10:05',
        bodyVi:
          'Đã phê duyệt yêu cầu ghi Có hoàn trả 15.000.000 ₫ (DEMO). Hiện lệnh đang ở trạng thái chờ chạy lô hạch toán sổ cái nội bộ.',
        bodyEn:
          'Approved the 15,000,000 VND credit request (DEMO). The entry is currently awaiting simulated internal ledger batch settlement.',
      },
    ],
  },
  {
    id: 'scase_03',
    caseCode: 'SUP-2026-818',
    createdAt: '03/11/2026 · 09:30',
    updatedAt: '03/11/2026 · 09:30',
    status: 'open',
    priority: 'medium',
    category: 'fund_dispute',
    categoryLabelVi: 'Khiếu nại Quỹ nhóm',
    categoryLabelEn: 'Group Fund Dispute',
    subjectVi:
      'Yêu cầu hoàn trả 1.000.000 ₫ do đóng góp trùng 2 lần kỳ Tháng 11 tại Quỹ Team Sản phẩm Q4',
    subjectEn:
      'Request 1,000,000 VND refund for duplicate Nov cycle contribution in Q4 Product Team Fund',
    summaryVi:
      'Hồ sơ mới chưa gán người xử lý: Thành viên bấm đóng góp 2 lần liên tiếp cho cùng kỳ tháng 11/2026 và đề nghị hoàn lại 1 kỳ.',
    summaryEn:
      'Unassigned new case: Member submitted two consecutive contributions for the Nov 2026 cycle and requested a refund of one duplicate entry.',
    assignedOperator: null,
    scopedUser: {
      userId: 'usr_admin_05',
      fullName: 'Phạm Gia Huy',
      spendId: 'SW-HUY-5120',
      emailMasked: 'huy.pham•••@company.vn',
      phoneMasked: '098•••5120',
      kycTierVi: 'eKYC Cấp 2 (Đã xác thực CCCD)',
      kycTierEn: 'eKYC Tier 2 (ID Verified)',
      walletCode: 'WAL-HUY-MAIN',
      walletBalanceVnd: 11_400_000,
      recentDeviceSummaryVi: 'Samsung Galaxy S24 Ultra · Android 15 (Tin cậy)',
      recentDeviceSummaryEn: 'Samsung Galaxy S24 Ultra · Android 15 (Trusted)',
      securityNoteVi: 'Xác thực PIN hợp lệ cho cả 2 giao dịch đóng góp.',
      securityNoteEn: 'Valid PIN authentication recorded for both contribution transactions.',
    },
    scopedFund: {
      fundCode: 'GRP-PRODUCT-03',
      fundNameVi: 'Quỹ Team Sản phẩm Q4',
      fundNameEn: 'Q4 Product Team Fund',
      fundModeVi: 'Quỹ định kỳ (Fixed) · Nghĩa vụ 1.000.000 ₫/tháng',
      fundModeEn: 'Fixed Cycle Fund · 1,000,000 VND/month obligation',
      ownerName: 'Nguyễn Nhật Minh (Chủ quỹ)',
      memberCount: 8,
      fundAvailableBalanceVnd: 9_000_000,
      fundHeld24hBalanceVnd: 0,
      disputedTxCode: 'TX-20261101-298',
      disputedAmountVnd: 1_000_000,
      disputedItemSummaryVi:
        'Ghi nhận 2 bút toán đóng góp 1.000.000 ₫ cách nhau 4 phút của thành viên Phạm Gia Huy trong kỳ Tháng 11/2026.',
      disputedItemSummaryEn:
        'Recorded two 1,000,000 VND contribution entries 4 minutes apart by member Pham Gia Huy in Nov 2026 cycle.',
      receiptVerificationVi:
        'Sổ cái Quỹ Team Sản phẩm Q4 đang dư +1.000.000 ₫ so với định mức kỳ của 8 thành viên.',
      receiptVerificationEn:
        'Q4 Product Team Fund ledger shows +1,000,000 VND surplus above the 8-member cycle quota.',
      memberStandingSummaryVi:
        'Phạm Gia Huy đã đóng 2.000.000 ₫ / định mức 1.000.000 ₫ (Dư 1 kỳ).',
      memberStandingSummaryEn:
        'Pham Gia Huy paid 2,000,000 VND / 1,000,000 VND quota (1 cycle surplus).',
    },
    refundClaim: {
      claimCode: 'CLM-REF-2026-403',
      requestedAmountVnd: 1_000_000,
      targetWalletCode: 'WAL-HUY-MAIN',
      sourceTxCode: 'TX-20261101-298',
      claimReasonVi:
        'Hoàn trả 1.000.000 ₫ khoản đóng góp trùng kỳ Tháng 11 từ Quỹ Team Sản phẩm Q4 về ví cá nhân WAL-HUY-MAIN.',
      claimReasonEn:
        'Refund 1,000,000 VND duplicate Nov cycle contribution from Q4 Product Team Fund to personal wallet WAL-HUY-MAIN.',
      decisionState: 'pending_approval',
      settlementStatus: 'unsettled_pending_decision',
    },
    messages: [
      {
        id: 'msg_818_1',
        senderRole: 'user',
        senderName: 'Phạm Gia Huy (SW-HUY-5120)',
        timestamp: '03/11/2026 · 09:30',
        bodyVi:
          'Do mạng Wi-Fi bị lag nên mình bấm đóng quỹ Team Sản phẩm Q4 tới 2 lần (mỗi lần 1.000.000 ₫). Nhờ Admin kiểm tra và hoàn lại 1.000.000 ₫ về ví giúp mình.',
        bodyEn:
          'Due to Wi-Fi lag I submitted my Q4 Product Team Fund contribution twice (1,000,000 VND each). Please verify and refund 1,000,000 VND to my wallet.',
      },
    ],
  },
  {
    id: 'scase_04',
    caseCode: 'SUP-2026-809',
    createdAt: '03/11/2026 · 09:42',
    updatedAt: '03/11/2026 · 09:48',
    status: 'awaiting_user_info',
    priority: 'critical',
    category: 'security_pin',
    categoryLabelVi: 'Bảo mật & Mã PIN',
    categoryLabelEn: 'PIN & Account Security',
    subjectVi:
      'Khóa bảo vệ sau 3 lần nhập sai mã PIN giao dịch trên trình duyệt Chrome lạ',
    subjectEn:
      'Protective lockout after 3 failed transaction PIN attempts on unrecognized Chrome browser',
    summaryVi:
      'Ví WAL-KHANH-MAIN đang tạm đóng băng chiều rút/chuyển. Đang chờ khách hàng xác nhận ảnh CCCD để cấp lại quyền giao dịch.',
    summaryEn:
      'Wallet WAL-KHANH-MAIN outflow is frozen. Awaiting user eKYC confirmation before restoring transaction privileges.',
    assignedOperator: 'Phạm Minh Quân · Risk Ops',
    scopedUser: {
      userId: 'usr_admin_03',
      fullName: 'Lê Quốc Khánh',
      spendId: 'SW-KHANH-7302',
      emailMasked: 'khanh.le•••@outlook.com',
      phoneMasked: '093•••7302',
      kycTierVi: 'eKYC Cấp 2 (Đang yêu cầu xác thực lại)',
      kycTierEn: 'eKYC Tier 2 (Re-verification requested)',
      walletCode: 'WAL-KHANH-MAIN',
      walletBalanceVnd: 19_450_000,
      recentDeviceSummaryVi: 'Chrome 130 · Windows 11 (IP lạ · Cảnh báo PIN)',
      recentDeviceSummaryEn: 'Chrome 130 · Windows 11 (Unrecognized IP · PIN Alert)',
      securityNoteVi: 'Ví đang ở trạng thái Đóng băng (Frozen) theo lệnh AUD-LED-20261103-0945.',
      securityNoteEn: 'Wallet is currently Frozen under audit order AUD-LED-20261103-0945.',
    },
    scopedFund: null,
    refundClaim: null,
    messages: [
      {
        id: 'msg_809_1',
        senderRole: 'system',
        senderName: 'Hệ thống Giám sát Rủi ro',
        timestamp: '03/11/2026 · 09:42',
        bodyVi:
          'Tự động tạo hồ sơ SUP-2026-809 do phát hiện 3 lần nhập sai mã PIN liên tiếp trên thiết bị không tin cậy.',
        bodyEn:
          'Auto-created case SUP-2026-809 after detecting 3 consecutive failed PIN attempts on an untrusted device.',
      },
      {
        id: 'msg_809_2',
        senderRole: 'operator',
        senderName: 'Phạm Minh Quân · Risk Ops',
        timestamp: '03/11/2026 · 09:48',
        bodyVi:
          '[YÊU CẦU BỔ SUNG THÔNG TIN] Vui lòng xác nhận phiên đăng nhập trên ứng dụng di động chính chủ để hoàn tất mở khóa.',
        bodyEn:
          '[INFORMATION REQUEST] Please confirm your login session from your primary trusted mobile device to complete unlocking.',
        isInfoRequest: true,
      },
    ],
  },
  {
    id: 'scase_05',
    caseCode: 'SUP-2026-822',
    createdAt: '03/11/2026 · 09:55',
    updatedAt: '03/11/2026 · 10:00',
    status: 'open',
    priority: 'high',
    category: 'p2p_refund',
    categoryLabelVi: 'Hoàn tiền & Chuyển khoản P2P',
    categoryLabelEn: 'P2P Transfer & Refund',
    subjectVi:
      'Đề nghị thu hồi & hoàn tiền 1.250.000 ₫ chuyển nhầm tới tài khoản đang tạm khóa nhận',
    subjectEn:
      'Request recall & 1,250,000 VND refund for P2P transfer sent to suspended recipient',
    summaryVi:
      'Khách hàng chuyển nhầm SpendID sang tài khoản đang bị tạm treo chờ xác minh. Khoản tiền đang nằm ở tài khoản treo trung gian.',
    summaryEn:
      'User accidentally entered a SpendID belonging to a suspended account. Funds are currently held in an internal suspense account.',
    assignedOperator: CURRENT_ADMIN_OPERATOR,
    scopedUser: {
      userId: 'usr_admin_04',
      fullName: 'Vũ Phương Thảo',
      spendId: 'SW-THAO-6198',
      emailMasked: 'thao.vu•••@gmail.com',
      phoneMasked: '097•••6198',
      kycTierVi: 'eKYC Cấp 2 (Đã xác thực CCCD)',
      kycTierEn: 'eKYC Tier 2 (ID Verified)',
      walletCode: 'WAL-THAO-MAIN',
      walletBalanceVnd: 18_900_000,
      recentDeviceSummaryVi: 'iPhone 15 · iOS 18.1 (Tin cậy · Hà Nội)',
      recentDeviceSummaryEn: 'iPhone 15 · iOS 18.1 (Trusted · Hanoi)',
      securityNoteVi: 'Giao dịch xác thực Face ID chính chủ; tài khoản đích chưa rút tiền.',
      securityNoteEn: 'Authenticated via trusted Face ID; recipient account has not withdrawn funds.',
    },
    scopedFund: null,
    refundClaim: {
      claimCode: 'CLM-REF-2026-405',
      requestedAmountVnd: 1_250_000,
      targetWalletCode: 'WAL-THAO-MAIN',
      sourceTxCode: 'TX-20261103-845',
      claimReasonVi:
        'Hoàn trả 1.250.000 ₫ từ tài khoản treo trung gian (GL-SUSPENSE-P2P) về ví WAL-THAO-MAIN.',
      claimReasonEn:
        'Refund 1,250,000 VND from internal P2P suspense account (GL-SUSPENSE-P2P) back to WAL-THAO-MAIN.',
      decisionState: 'pending_approval',
      settlementStatus: 'unsettled_pending_decision',
    },
    messages: [
      {
        id: 'msg_822_1',
        senderRole: 'user',
        senderName: 'Vũ Phương Thảo (SW-THAO-6198)',
        timestamp: '03/11/2026 · 09:55',
        bodyVi:
          'Mình gõ nhầm 1 chữ số mã SpendID nên chuyển 1.250.000 ₫ sang tài khoản đang tạm khóa nhận. Nhờ hỗ trợ duyệt hoàn lại về ví WAL-THAO-MAIN giúp mình.',
        bodyEn:
          'I mistyped one digit of the SpendID and sent 1,250,000 VND to a suspended recipient. Please approve refunding it back to WAL-THAO-MAIN.',
      },
    ],
  },
  {
    id: 'scase_06',
    caseCode: 'SUP-2026-815',
    createdAt: '03/11/2026 · 07:50',
    updatedAt: '03/11/2026 · 08:15',
    status: 'resolved',
    priority: 'critical',
    category: 'device_loss',
    categoryLabelVi: 'Khẩn cấp · Mất thiết bị',
    categoryLabelEn: 'Emergency · Lost Device',
    subjectVi:
      'Báo mất điện thoại iPhone 14 Pro — Yêu cầu đóng băng ví & thu hồi toàn bộ phiên',
    subjectEn:
      'Reported lost iPhone 14 Pro — Requested emergency wallet freeze & session revocation',
    summaryVi:
      'Đã đóng băng ví WAL-TUNG-MAIN (31.800.000 ₫) và thu hồi toàn bộ phiên đăng nhập cũ theo yêu cầu bảo vệ khẩn cấp của chủ tài khoản.',
    summaryEn:
      'Froze wallet WAL-TUNG-MAIN (31,800,000 VND) and revoked all active sessions per owner emergency protection request.',
    assignedOperator: CURRENT_ADMIN_OPERATOR,
    scopedUser: {
      userId: 'usr_admin_06',
      fullName: 'Hoàng Sơn Tùng',
      spendId: 'SW-TUNG-8310',
      emailMasked: 'tung.hoang•••@gmail.com',
      phoneMasked: '094•••8310',
      kycTierVi: 'eKYC Cấp 2 (Đã xác thực CCCD)',
      kycTierEn: 'eKYC Tier 2 (ID Verified)',
      walletCode: 'WAL-TUNG-MAIN',
      walletBalanceVnd: 31_800_000,
      recentDeviceSummaryVi: 'Đã thu hồi toàn bộ phiên thiết bị cũ lúc 08:00',
      recentDeviceSummaryEn: 'All prior device sessions revoked at 08:00',
      securityNoteVi: 'Số dư 31.800.000 ₫ được bảo toàn 100% ở trạng thái đóng băng.',
      securityNoteEn: '31,800,000 VND balance 100% preserved under wallet freeze.',
    },
    scopedFund: null,
    refundClaim: null,
    resolutionSummaryVi:
      'Đã đóng băng ví mẫu WAL-TUNG-MAIN và thu hồi phiên đăng nhập; hướng dẫn khách hàng kích hoạt lại trên thiết bị mới qua eKYC.',
    resolutionSummaryEn:
      'Froze synthetic wallet WAL-TUNG-MAIN and revoked device sessions; guided user to re-activate on new device via eKYC.',
    messages: [
      {
        id: 'msg_815_1',
        senderRole: 'user',
        senderName: 'Hoàng Sơn Tùng (SW-TUNG-8310)',
        timestamp: '03/11/2026 · 07:50',
        bodyVi:
          'Mình vừa bị rơi mất điện thoại iPhone 14 Pro. Nhờ tổng đài khóa khẩn cấp ví SpendWise và đăng xuất mọi thiết bị giúp mình!',
        bodyEn:
          'I just lost my iPhone 14 Pro. Please urgently freeze my SpendWise wallet and sign out all devices!',
      },
      {
        id: 'msg_815_2',
        senderRole: 'operator',
        senderName: CURRENT_ADMIN_OPERATOR,
        timestamp: '03/11/2026 · 08:15',
        bodyVi:
          'Đã đóng băng ví WAL-TUNG-MAIN và thu hồi toàn bộ phiên đăng nhập. Số dư 31.800.000 ₫ của bạn hoàn toàn an toàn.',
        bodyEn:
          'Frozen wallet WAL-TUNG-MAIN and revoked all active sessions. Your 31,800,000 VND balance is completely safe.',
      },
    ],
  },
];

// ============================================================================
// ADMIN A5 — BROADCAST AUTHORING, AI OPERATIONS MONITORING & BANK GATEWAYS (DEMO)
// ============================================================================

export type AdminBroadcastAudienceId =
  | 'all_active_users'
  | 'group_fund_owners'
  | 'bank_linked_wallets'
  | 'security_watch_tier';

export type AdminBroadcastStatus =
  | 'draft'
  | 'scheduled_demo'
  | 'published_demo'
  | 'cancelled_demo';

export type AdminBroadcastCategory =
  | 'system_maintenance'
  | 'security_advisory'
  | 'fund_cycle_reminder'
  | 'feature_update';

export interface AdminBroadcastAudienceOption {
  id: AdminBroadcastAudienceId;
  labelVi: string;
  labelEn: string;
  descriptionVi: string;
  descriptionEn: string;
  estimatedRecipients: number;
}

export interface AdminBroadcastEntry {
  id: string;
  broadcastCode: string;
  titleVi: string;
  titleEn: string;
  bodyVi: string;
  bodyEn: string;
  category: AdminBroadcastCategory;
  categoryLabelVi: string;
  categoryLabelEn: string;
  audienceId: AdminBroadcastAudienceId;
  audienceLabelVi: string;
  audienceLabelEn: string;
  estimatedRecipients: number;
  channelVi: string;
  channelEn: string;
  status: AdminBroadcastStatus;
  createdAt: string;
  publishedOrUpdatedAt: string;
  authorOperator: string;
  deliveryNoticeVi: string;
  deliveryNoticeEn: string;
}

export const ADMIN_BROADCAST_AUDIENCE_OPTIONS: AdminBroadcastAudienceOption[] = [
  {
    id: 'all_active_users',
    labelVi: 'Toàn bộ người dùng đang hoạt động',
    labelEn: 'All Active Users',
    descriptionVi:
      'Hiển thị biểu ngữ trong ứng dụng cho toàn bộ tài khoản đang hoạt động (chỉ mô phỏng DEMO).',
    descriptionEn:
      'Displays in-app banner to all active user accounts (DEMO simulation only).',
    estimatedRecipients: 18_420,
  },
  {
    id: 'group_fund_owners',
    labelVi: 'Chủ quỹ & Thành viên Quỹ nhóm',
    labelEn: 'Group Fund Owners & Members',
    descriptionVi:
      'Nhắm tới người dùng tham gia Quỹ định kỳ (Fixed) và Quỹ tự do (Free).',
    descriptionEn:
      'Targets users participating in Fixed Cycle and Free Pool Group Funds.',
    estimatedRecipients: 3_140,
  },
  {
    id: 'bank_linked_wallets',
    labelVi: 'Người dùng có Ví liên kết Ngân hàng',
    labelEn: 'Users with Linked Bank Wallets',
    descriptionVi:
      'Phù hợp cho thông báo bảo trì cổng Napas 247 hoặc đối soát nạp/rút ngân hàng.',
    descriptionEn:
      'Ideal for Napas 247 gateway maintenance or bank top-up/withdrawal notices.',
    estimatedRecipients: 14_890,
  },
  {
    id: 'security_watch_tier',
    labelVi: 'Tài khoản cần lưu ý Bảo mật PIN / Thiết bị mới',
    labelEn: 'Security & New-Device Watchlist',
    descriptionVi:
      'Nhắc nhở bảo mật Zero-Credential, cảnh báo lừa đảo OTP/PIN cho thiết bị mới đăng nhập.',
    descriptionEn:
      'Zero-Credential security reminders and phishing advisories for recent device logins.',
    estimatedRecipients: 1_280,
  },
];

export interface AdminBroadcastTemplate {
  id: string;
  labelVi: string;
  labelEn: string;
  category: AdminBroadcastCategory;
  audienceId: AdminBroadcastAudienceId;
  titleVi: string;
  titleEn: string;
  bodyVi: string;
  bodyEn: string;
}

export const ADMIN_BROADCAST_TEMPLATES: AdminBroadcastTemplate[] = [
  {
    id: 'btpl_napas_maint',
    labelVi: 'Mẫu: Bảo trì định kỳ cổng Napas 247 ban đêm',
    labelEn: 'Template: Scheduled Overnight Napas 247 Maintenance',
    category: 'system_maintenance',
    audienceId: 'bank_linked_wallets',
    titleVi: 'Thông báo bảo trì định kỳ cổng liên kết ngân hàng Napas 247 (01:00 – 02:30)',
    titleEn: 'Scheduled Napas 247 Bank Gateway Maintenance (01:00 – 02:30)',
    bodyVi:
      'Cổng nạp/rút liên ngân hàng sẽ bảo trì nâng cấp định kỳ từ 01:00 đến 02:30 sáng mai. Chuyển tiền P2P nội bộ và đóng góp Quỹ nhóm vẫn hoạt động bình thường 100%.',
    bodyEn:
      'Interbank top-up and withdrawal rails will undergo scheduled maintenance from 01:00 to 02:30 AM. Internal P2P transfers and Group Fund contributions remain 100% available.',
  },
  {
    id: 'btpl_fund_cycle',
    labelVi: 'Mẫu: Nhắc lịch đóng góp Quỹ nhóm Định kỳ tháng 11',
    labelEn: 'Template: Nov Fixed Group Fund Cycle Reminder',
    category: 'fund_cycle_reminder',
    audienceId: 'group_fund_owners',
    titleVi: 'Nhắc lịch hoàn tất nghĩa vụ đóng góp Quỹ nhóm Định kỳ Tháng 11/2026',
    titleEn: 'Reminder: Complete Nov 2026 Fixed Group Fund Cycle Contributions',
    bodyVi:
      'Kỳ đóng góp tháng 11/2026 của các Quỹ định kỳ sẽ chốt sổ vào 23:59 ngày 05/11. Vui lòng kiểm tra số dư ví khả dụng để duy trì trạng thái đúng hạn.',
    bodyEn:
      'The November 2026 Fixed Group Fund contribution window closes at 23:59 on Nov 5. Please check your liquid wallet balance to stay on-time.',
  },
  {
    id: 'btpl_security_pin',
    labelVi: 'Mẫu: Cảnh báo bảo mật Mã PIN & OTP',
    labelEn: 'Template: PIN & OTP Anti-Phishing Advisory',
    category: 'security_advisory',
    audienceId: 'security_watch_tier',
    titleVi: 'Cảnh báo an toàn: SpendWise không bao giờ yêu cầu cung cấp mã PIN hay OTP',
    titleEn: 'Security Advisory: SpendWise Never Asks for Your PIN or OTP',
    bodyVi:
      'Nhân viên vận hành SpendWise tuyệt đối không bao giờ hỏi mật khẩu, mã PIN 6 số hoặc mã OTP của bạn. Hãy khóa ví ngay trong mục Bảo mật nếu phát hiện thiết bị lạ.',
    bodyEn:
      'SpendWise operators will never ask for your password, 6-digit PIN, or OTP code. Freeze your wallet immediately in Security Settings if you notice an unfamiliar device.',
  },
];

export const ADMIN_BROADCAST_FIXTURES: AdminBroadcastEntry[] = [
  {
    id: 'bc_01',
    broadcastCode: 'BRC-20261103-101',
    titleVi: 'Thông báo nâng cấp đối soát tự động cổng BIDV & Vietcombank Napas 247',
    titleEn: 'Automated Reconciliation Upgrade for BIDV & Vietcombank Napas 247',
    bodyVi:
      'Hệ thống đang tối ưu hóa bộ đọc webhook Napas 247. Các giao dịch nạp ví thiếu hậu tố tham chiếu sẽ được đối soát viên xử lý trong vòng 15 phút (Trạng thái DEMO).',
    bodyEn:
      'Optimizing Napas 247 webhook matching. Top-up transfers missing reference suffixes are reconciled within 15 minutes (DEMO state).',
    category: 'system_maintenance',
    categoryLabelVi: 'Bảo trì & Vận hành',
    categoryLabelEn: 'Maintenance & Ops',
    audienceId: 'bank_linked_wallets',
    audienceLabelVi: 'Người dùng có Ví liên kết Ngân hàng',
    audienceLabelEn: 'Users with Linked Bank Wallets',
    estimatedRecipients: 14_890,
    channelVi: 'Biểu ngữ In-App + Hộp thư Thông báo (DEMO)',
    channelEn: 'In-App Banner + Notification Inbox (DEMO)',
    status: 'published_demo',
    createdAt: '03/11/2026 · 08:30',
    publishedOrUpdatedAt: '03/11/2026 · 08:35',
    authorOperator: CURRENT_ADMIN_OPERATOR,
    deliveryNoticeVi:
      'Đã kích hoạt hiển thị DEMO cục bộ — Không gửi thông báo đẩy (Push/SMS/Email) ra bên ngoài.',
    deliveryNoticeEn:
      'Activated in local DEMO state — Zero external Push, SMS, or Email messages sent.',
  },
  {
    id: 'bc_02',
    broadcastCode: 'BRC-20261103-102',
    titleVi: 'Nhắc lịch đóng góp Quỹ nhóm Định kỳ kỳ Tháng 11/2026',
    titleEn: 'November 2026 Fixed Group Fund Contribution Reminder',
    bodyVi:
      'Các thành viên Quỹ nhóm Định kỳ vui lòng hoàn tất đóng góp trước ngày 05/11/2026 để bộ đánh giá chu kỳ ghi nhận đúng hạn.',
    bodyEn:
      'Fixed Group Fund members please complete cycle contributions before Nov 5, 2026 for on-time standing.',
    category: 'fund_cycle_reminder',
    categoryLabelVi: 'Nhắc lịch Quỹ nhóm',
    categoryLabelEn: 'Group Fund Reminder',
    audienceId: 'group_fund_owners',
    audienceLabelVi: 'Chủ quỹ & Thành viên Quỹ nhóm',
    audienceLabelEn: 'Group Fund Owners & Members',
    estimatedRecipients: 3_140,
    channelVi: 'Hộp thư Thông báo In-App (DEMO)',
    channelEn: 'In-App Notification Inbox (DEMO)',
    status: 'scheduled_demo',
    createdAt: '03/11/2026 · 09:20',
    publishedOrUpdatedAt: '03/11/2026 · 09:20',
    authorOperator: CURRENT_ADMIN_OPERATOR,
    deliveryNoticeVi:
      'Đang lên lịch mô phỏng DEMO — Không gọi dịch vụ gửi tin bên thứ ba.',
    deliveryNoticeEn:
      'Scheduled in synthetic DEMO state — No third-party messaging provider calls.',
  },
  {
    id: 'bc_03',
    broadcastCode: 'BRC-20261103-103',
    titleVi: 'Cảnh báo phòng chống giả mạo yêu cầu cung cấp mã PIN giao dịch',
    titleEn: 'Security Alert: Beware of Phishing Requests for Transaction PINs',
    bodyVi:
      'SpendWise áp dụng chính sách Zero-Credential: Quản trị viên không bao giờ yêu cầu mã PIN hay OTP của bạn dưới mọi hình thức.',
    bodyEn:
      'SpendWise enforces a strict Zero-Credential policy: Administrators never request your PIN or OTP under any circumstances.',
    category: 'security_advisory',
    categoryLabelVi: 'Cảnh báo Bảo mật',
    categoryLabelEn: 'Security Advisory',
    audienceId: 'all_active_users',
    audienceLabelVi: 'Toàn bộ người dùng đang hoạt động',
    audienceLabelEn: 'All Active Users',
    estimatedRecipients: 18_420,
    channelVi: 'Biểu ngữ Khẩn cấp In-App (DEMO)',
    channelEn: 'Emergency In-App Banner (DEMO)',
    status: 'draft',
    createdAt: '03/11/2026 · 10:00',
    publishedOrUpdatedAt: '03/11/2026 · 10:00',
    authorOperator: CURRENT_ADMIN_OPERATOR,
    deliveryNoticeVi:
      'Bản nháp nội bộ DEMO — Chưa kích hoạt hiển thị.',
    deliveryNoticeEn:
      'Internal DEMO draft — Not yet published.',
  },
];

export type AdminAiPipelineState =
  | 'healthy_demo'
  | 'degraded_latency_demo'
  | 'guardrail_blocked_demo'
  | 'paused_demo';

export type AdminAiPipelineDomain =
  | 'tx_categorization'
  | 'budget_forecasting'
  | 'fund_cashflow_summary'
  | 'anomaly_guardrail';

export interface AdminAiPipelineEntry {
  id: string;
  pipelineCode: string;
  nameVi: string;
  nameEn: string;
  domain: AdminAiPipelineDomain;
  domainLabelVi: string;
  domainLabelEn: string;
  modelAliasDisplay: string;
  state: AdminAiPipelineState;
  syntheticRequests24h: number;
  p95LatencyMs: number;
  guardrailPassRatePct: number;
  piiMaskingStatusVi: string;
  piiMaskingStatusEn: string;
  lastSampleOutputVi: string;
  lastSampleOutputEn: string;
  operatorNoteVi: string;
  operatorNoteEn: string;
  lastEvaluatedAt: string;
}

export const ADMIN_AI_OPERATIONS_FIXTURES: AdminAiPipelineEntry[] = [
  {
    id: 'ai_pipe_01',
    pipelineCode: 'AI-PIPE-CAT-01',
    nameVi: 'Bộ phân loại Danh mục Chi tiêu & Tách dòng tiền Quỹ nhóm',
    nameEn: 'Expense Category Classifier & Group Fund Flow Separator',
    domain: 'tx_categorization',
    domainLabelVi: 'Phân loại Giao dịch',
    domainLabelEn: 'Transaction Categorization',
    modelAliasDisplay: 'spendwise-categorizer-rules-v2 (Synthetic DEMO)',
    state: 'healthy_demo',
    syntheticRequests24h: 4_820,
    p95LatencyMs: 185,
    guardrailPassRatePct: 99.8,
    piiMaskingStatusVi: 'Đã ẩn danh 100% số tài khoản & tên đối tác trước khi phân tích',
    piiMaskingStatusEn: '100% account numbers & counterparty PII redacted prior to evaluation',
    lastSampleOutputVi:
      'Phân loại mẫu: "Chuyển khoản đóng Quỹ Gia đình Việt" → Nhãn: Góp quỹ · Chuyển tài sản (Tách khỏi chi tiêu cá nhân).',
    lastSampleOutputEn:
      'Sample output: "Family Fund contribution" → Label: Fund Asset Transfer (Excluded from personal consumption).',
    operatorNoteVi:
      'Chạy 100% bằng bộ quy tắc giả lập cục bộ — Không gọi API mô hình AI bên ngoài.',
    operatorNoteEn:
      'Runs 100% on local synthetic rules — Zero external AI model API calls.',
    lastEvaluatedAt: '03/11/2026 · 10:15',
  },
  {
    id: 'ai_pipe_02',
    pipelineCode: 'AI-PIPE-BDG-02',
    nameVi: 'Bộ dự báo Nhịp độ Ngân sách & Cảnh báo Vượt hạn mức tháng',
    nameEn: 'Monthly Budget Pacing & Overrun Forecaster',
    domain: 'budget_forecasting',
    domainLabelVi: 'Dự báo Ngân sách',
    domainLabelEn: 'Budget Forecasting',
    modelAliasDisplay: 'spendwise-budget-pacing-v1 (Synthetic DEMO)',
    state: 'healthy_demo',
    syntheticRequests24h: 2_310,
    p95LatencyMs: 240,
    guardrailPassRatePct: 99.5,
    piiMaskingStatusVi: 'Chỉ xử lý số tổng hợp theo danh mục, không đọc chi tiết nhạy cảm',
    piiMaskingStatusEn: 'Processes category aggregates only; zero sensitive PII access',
    lastSampleOutputVi:
      'Dự báo mẫu: Danh mục "Ăn uống" đạt 78% hạn mức tháng vào ngày 22 — Khuyến nghị giữ nhịp chi 180.000 ₫/ngày.',
    lastSampleOutputEn:
      'Sample output: "Dining" reached 78% of monthly cap by day 22 — Recommended daily pace: 180,000 VND/day.',
    operatorNoteVi:
      'Trạng thái DEMO ổn định — Không phát sinh kết nối máy chủ LLM.',
    operatorNoteEn:
      'Stable DEMO state — Zero LLM provider network requests.',
    lastEvaluatedAt: '03/11/2026 · 10:12',
  },
  {
    id: 'ai_pipe_03',
    pipelineCode: 'AI-PIPE-FND-03',
    nameVi: 'Trợ lý Tóm tắt Minh bạch Quỹ nhóm & Kiểm tra Giữ chỗ 24h',
    nameEn: 'Group Fund Transparency Summarizer & 24h Hold Inspector',
    domain: 'fund_cashflow_summary',
    domainLabelVi: 'Tóm tắt Quỹ nhóm',
    domainLabelEn: 'Group Fund Summary',
    modelAliasDisplay: 'spendwise-fund-digest-v2 (Synthetic DEMO)',
    state: 'degraded_latency_demo',
    syntheticRequests24h: 1_190,
    p95LatencyMs: 690,
    guardrailPassRatePct: 98.9,
    piiMaskingStatusVi: 'Chỉ hiển thị tóm tắt trong phạm vi thành viên cùng quỹ',
    piiMaskingStatusEn: 'Summaries scoped strictly to verified fund members',
    lastSampleOutputVi:
      'Cảnh báo độ trễ mẫu: Thời gian tổng hợp hóa đơn Quỹ Du lịch Đà Lạt tăng lên 690ms do hàng đợi đối chiếu chứng từ.',
    lastSampleOutputEn:
      'Simulated latency notice: Da Lat Trip Fund receipt digest took 690ms due to queued invoice checks.',
    operatorNoteVi:
      'Độ trễ mô phỏng phục vụ kiểm thử cảnh báo vận hành AI (Không gọi nhà cung cấp AI thực).',
    operatorNoteEn:
      'Simulated latency state for AI ops monitoring verification (Zero real AI provider calls).',
    lastEvaluatedAt: '03/11/2026 · 10:08',
  },
  {
    id: 'ai_pipe_04',
    pipelineCode: 'AI-PIPE-GRD-04',
    nameVi: 'Bộ chắn An toàn (Guardrail) Chống lộ Thông tin Xác thực & Tư vấn Đầu tư rủi ro',
    nameEn: 'Zero-Credential & Unlicensed Investment Advice Guardrail Filter',
    domain: 'anomaly_guardrail',
    domainLabelVi: 'Kiểm soát An toàn AI',
    domainLabelEn: 'Safety & Guardrails',
    modelAliasDisplay: 'spendwise-policy-guard-v3 (Synthetic DEMO)',
    state: 'guardrail_blocked_demo',
    syntheticRequests24h: 3_640,
    p95LatencyMs: 95,
    guardrailPassRatePct: 99.2,
    piiMaskingStatusVi: 'Chặn tự động 100% câu hỏi chứa mã PIN, OTP hoặc số thẻ đầy đủ',
    piiMaskingStatusEn: 'Automatically blocks 100% of prompts containing PINs, OTPs, or full PANs',
    lastSampleOutputVi:
      'Sự kiện chặn mẫu: Đã từ chối 3 yêu cầu hỏi gợi ý mã PIN và 5 câu hỏi đầu cơ tiền mã hóa ngoài phạm vi quản lý chi tiêu.',
    lastSampleOutputEn:
      'Blocked sample: Intercepted 3 PIN-related prompts and 5 speculative crypto prompts outside personal finance scope.',
    operatorNoteVi:
      'Bộ lọc chính sách nội bộ hoạt động đúng thiết kế (Chế độ dữ liệu mẫu DEMO).',
    operatorNoteEn:
      'Internal policy filter operating as designed (Synthetic DEMO fixture mode).',
    lastEvaluatedAt: '03/11/2026 · 10:18',
  },
];

export type AdminBankGatewayState =
  | 'operational_demo'
  | 'degraded_latency_demo'
  | 'maintenance_demo';

export type AdminBankRailType =
  | 'napas_247'
  | 'direct_link'
  | 'vietqr_webhook';

export interface AdminBankGatewayEntry {
  id: string;
  gatewayCode: string;
  bankName: string;
  shortName: string;
  railType: AdminBankRailType;
  railLabelVi: string;
  railLabelEn: string;
  state: AdminBankGatewayState;
  syntheticTxVolume24h: number;
  webhookSuccessRatePct: number;
  p95RoundtripMs: number;
  pendingReconcileCount: number;
  statusSummaryVi: string;
  statusSummaryEn: string;
  lastHeartbeatAt: string;
}

export const ADMIN_BANK_GATEWAYS_FIXTURES: AdminBankGatewayEntry[] = [
  {
    id: 'bgw_01',
    gatewayCode: 'GW-VCB-NAPAS247',
    bankName: 'Vietcombank · Ngân hàng TMCP Ngoại thương Việt Nam',
    shortName: 'Vietcombank',
    railType: 'napas_247',
    railLabelVi: 'Chuyển nhanh Napas 247 & Webhook',
    railLabelEn: 'Napas 247 Fast Transfer & Webhook',
    state: 'operational_demo',
    syntheticTxVolume24h: 1_840,
    webhookSuccessRatePct: 99.85,
    p95RoundtripMs: 320,
    pendingReconcileCount: 1,
    statusSummaryVi:
      'Hoạt động ổn định (DEMO) — Có 1 giao dịch nạp thiếu hậu tố SWTOPUP đang chờ đối soát thủ công (TX-20261103-901).',
    statusSummaryEn:
      'Operational (DEMO) — 1 top-up missing SWTOPUP suffix awaiting manual match (TX-20261103-901).',
    lastHeartbeatAt: '03/11/2026 · 10:20',
  },
  {
    id: 'bgw_02',
    gatewayCode: 'GW-BIDV-DIRECT',
    bankName: 'BIDV · Ngân hàng TMCP Đầu tư và Phát triển Việt Nam',
    shortName: 'BIDV',
    railType: 'direct_link',
    railLabelVi: 'Liên kết Ví trực tiếp (Direct Link)',
    railLabelEn: 'Direct E-Wallet Account Link',
    state: 'degraded_latency_demo',
    syntheticTxVolume24h: 920,
    webhookSuccessRatePct: 96.4,
    p95RoundtripMs: 2_840,
    pendingReconcileCount: 3,
    statusSummaryVi:
      'Độ trễ phản hồi cao (2.84s — Mô phỏng DEMO) trùng khớp với cảnh báo ALT-901; bộ tự động hoàn trả (Auto-Reversal) đang kích hoạt.',
    statusSummaryEn:
      'Elevated callback latency (2.84s — DEMO simulation) matching alert ALT-901; automated reversal safeguard active.',
    lastHeartbeatAt: '03/11/2026 · 10:19',
  },
  {
    id: 'bgw_03',
    gatewayCode: 'GW-TCB-DIRECT',
    bankName: 'Techcombank · Ngân hàng TMCP Kỹ thương Việt Nam',
    shortName: 'Techcombank',
    railType: 'direct_link',
    railLabelVi: 'Liên kết Ví trực tiếp (Direct Link)',
    railLabelEn: 'Direct E-Wallet Account Link',
    state: 'operational_demo',
    syntheticTxVolume24h: 1_510,
    webhookSuccessRatePct: 99.92,
    p95RoundtripMs: 275,
    pendingReconcileCount: 0,
    statusSummaryVi:
      'Hoạt động ổn định (DEMO) — 100% lệnh nạp/rút mẫu khớp tức thời, không có giao dịch treo.',
    statusSummaryEn:
      'Operational (DEMO) — 100% of synthetic top-up/withdrawal entries matched instantaneously.',
    lastHeartbeatAt: '03/11/2026 · 10:20',
  },
  {
    id: 'bgw_04',
    gatewayCode: 'GW-MBB-VIETQR',
    bankName: 'MB Bank · Ngân hàng TMCP Quân Đội',
    shortName: 'MB Bank',
    railType: 'vietqr_webhook',
    railLabelVi: 'Quét mã VietQR Động & Báo Có tự động',
    railLabelEn: 'Dynamic VietQR & Credit Webhook',
    state: 'operational_demo',
    syntheticTxVolume24h: 1_260,
    webhookSuccessRatePct: 99.78,
    p95RoundtripMs: 390,
    pendingReconcileCount: 0,
    statusSummaryVi:
      'Hoạt động ổn định (DEMO) — Xác thực chữ ký webhook mẫu hợp lệ (Khóa ký được ẩn an toàn [REDACTED_SECRET]).',
    statusSummaryEn:
      'Operational (DEMO) — Synthetic webhook signature checks passing (signing key strictly [REDACTED_SECRET]).',
    lastHeartbeatAt: '03/11/2026 · 10:20',
  },
  {
    id: 'bgw_05',
    gatewayCode: 'GW-ACB-NAPAS247',
    bankName: 'ACB · Ngân hàng TMCP Á Châu',
    shortName: 'ACB',
    railType: 'napas_247',
    railLabelVi: 'Chuyển nhanh Napas 247 & Webhook',
    railLabelEn: 'Napas 247 Fast Transfer & Webhook',
    state: 'operational_demo',
    syntheticTxVolume24h: 740,
    webhookSuccessRatePct: 99.88,
    p95RoundtripMs: 310,
    pendingReconcileCount: 0,
    statusSummaryVi:
      'Hoạt động ổn định (DEMO) — Không phát sinh chênh lệch đối soát trong ca trực.',
    statusSummaryEn:
      'Operational (DEMO) — Zero reconciliation variance recorded in current shift.',
    lastHeartbeatAt: '03/11/2026 · 10:20',
  },
  {
    id: 'bgw_06',
    gatewayCode: 'GW-VPB-VIETQR',
    bankName: 'VPBank · Ngân hàng TMCP Việt Nam Thịnh Vượng',
    shortName: 'VPBank',
    railType: 'vietqr_webhook',
    railLabelVi: 'Quét mã VietQR Động & Báo Có tự động',
    railLabelEn: 'Dynamic VietQR & Credit Webhook',
    state: 'maintenance_demo',
    syntheticTxVolume24h: 315,
    webhookSuccessRatePct: 99.5,
    p95RoundtripMs: 450,
    pendingReconcileCount: 0,
    statusSummaryVi:
      'Đang ở chế độ Bảo trì định kỳ (DEMO) — Tạm ngưng nhận lệnh nạp mới qua VietQR VPBank để kiểm thử điều hướng dự phòng.',
    statusSummaryEn:
      'In Scheduled Maintenance Mode (DEMO) — New VietQR top-ups paused to test fallback routing.',
    lastHeartbeatAt: '03/11/2026 · 09:30',
  },
];




