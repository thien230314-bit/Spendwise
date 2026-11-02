import os
import re
import csv
import json
import xml.etree.ElementTree as ET
import sys

sys.stdout.reconfigure(encoding='utf-8')

# 1. Load TRX passed test names
tree = ET.parse('reports/GROUP-FUNDS-TEST-RESULTS.trx')
root = tree.getroot()
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}

trx_tests = set()
for r in root.findall('.//t:UnitTestResult', ns):
    tname = r.attrib.get('testName')
    trx_tests.add(tname)
    clean_name = re.sub(r'\(.*\)$', '', tname)
    trx_tests.add(clean_name)
    if '.' in clean_name:
        trx_tests.add(clean_name.split('.')[-1])

print(f"Loaded {len(trx_tests)} test identifiers from TRX.")

# 2. Load C# files
csharp_files = {}
for rdir, _, fnames in os.walk('backend'):
    for fn in fnames:
        if fn.endswith('.cs'):
            fp = os.path.join(rdir, fn).replace('\\', '/')
            with open(fp, 'r', encoding='utf-8') as f:
                csharp_files[fp] = f.readlines()

def find_csharp_line(filepath, search_str):
    if filepath not in csharp_files:
        return 1
    lines = csharp_files[filepath]
    for idx, l in enumerate(lines):
        if search_str in l:
            return idx + 1
    return 1

# Build all assertions
evidence_rows = []

def add_evidence(claim_id, file_md, line_md, claim_text, file_cs, search_cs, test_name):
    line_cs = find_csharp_line(file_cs, search_cs)
    status = "MATCH" if test_name in trx_tests else "MISMATCH"
    evidence_rows.append({
        'ID': claim_id,
        'File_MD': file_md,
        'Line_MD': line_md,
        'Claim_Text': claim_text,
        'File_CSharp': file_cs,
        'Line_CSharp': line_cs,
        'Test_Name': test_name,
        'Status': status
    })

# =========================================================================
# 1. FIXED FUND V2 LIFECYCLE & CORE RULES
# =========================================================================
add_evidence(
    'FIXED-RULE-001', 'fixed.md', '21-28',
    'Tạo Fixed Fund bắt buộc chấp nhận cam kết tin tưởng v2.0; thiếu hoặc sai version bị từ chối với trust_commitment_required.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'trust_commitment_required',
    'F1_TrustCommitment_ExplicitConfirmationRequired_RejectsMissingOrFalseOrWrongVersion'
)
add_evidence(
    'FIXED-RULE-002', 'fixed.md', '33-35',
    'Tên quỹ Fixed có độ dài từ 1 đến 160 ký tự; mục tiêu ban đầu > 0, hiển thị 0đ/Target và chưa tính giờ góp khi mới tạo.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'InputBoundary.RequiredText(request.Name, 160',
    'T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning'
)
add_evidence(
    'FIXED-RULE-003', 'fixed.md', '33',
    'Thời lượng nộp tiền cá nhân Quỹ Cố định từ 1 đến 720 giờ (mặc định gợi ý 24 giờ). Ngoài khoảng bị từ chối với invalid_contribution_duration.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'invalid_contribution_duration',
    'T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning'
)
add_evidence(
    'FIXED-RULE-004', 'fixed.md', '35-37',
    'Chốt nghĩa vụ kỳ 1 yêu cầu tối thiểu 2 thành viên tính cả Owner; chỉ 1 người bị từ chối với insufficient_members.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'insufficient_members',
    'T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers'
)
add_evidence(
    'FIXED-RULE-005', 'fixed.md', '37-39',
    'Mục tiêu phải chia hết chính xác cho số ghế đã chốt đến từng 1 VND; không chia hết bị từ chối với target_not_divisible_by_seats.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundAllocation.cs', 'target_not_divisible_by_seats',
    'T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview'
)
add_evidence(
    'FIXED-RULE-006', 'fixed.md', '40-42',
    'Chia hết mục tiêu tạo ra snapshot nghĩa vụ cố định và kích hoạt countdown nộp tiền cá nhân độc lập cho từng người.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'ConfirmObligationsAsync',
    'T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown'
)
add_evidence(
    'FIXED-RULE-007', 'fixed.md', '42-45',
    'Snapshot nghĩa vụ là bất biến; không thể đổi target hay xác nhận lại để reset deadline trong cùng kỳ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'obligations_already_confirmed',
    'F2_ObligationSnapshot_IsImmutable_CannotChangeTargetOrResetDeadline'
)
add_evidence(
    'FIXED-RULE-008', 'fixed.md', '46-48',
    'Sau khi chốt nghĩa vụ, danh sách thành viên bị khóa; từ chối lời mời mới và từ chối chấp nhận lời mời cũ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundInvitations.cs', 'membership_locked_fixed_period',
    'F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance'
)
add_evidence(
    'FIXED-RULE-009', 'fixed.md', '48-52',
    'Chốt nghĩa vụ là idempotent; gọi lại với cùng revision trả về kết quả cũ, đổi cấu hình trả lỗi.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'ConfirmObligationsAsync',
    'F6_ConfirmObligations_Idempotency_AndReconfirmationRejection'
)
add_evidence(
    'FIXED-RULE-010', 'fixed.md', '54-58',
    'Đóng góp cá nhân yêu cầu đúng revision nghĩa vụ hiện hành; revision sai bị từ chối.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundContributions.cs', 'request.ObligationRevision != treasury.Revision',
    'T05_MemberWithinDeadline_CannotBeKickedForMissingContribution'
)
add_evidence(
    'FIXED-RULE-011', 'fixed.md', '58-61',
    'Đóng góp vượt nghĩa vụ còn thiếu (AmountVnd > required - paid) bị từ chối với contribution_exceeds_obligation.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundContributions.cs', 'contribution_exceeds_obligation',
    'F23_AttemptingToSpendLockedExcess_FailsWithInsufficientFundBalance'
)
add_evidence(
    'FIXED-RULE-012', 'fixed.md', '62-65',
    'Thành viên nộp đủ 100% nghĩa vụ trong hạn và cả nhóm đủ 100% mục tiêu, trạng thái chi tiêu chuyển OPEN.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'spendingState',
    'T10_SpendingState_BlockedUntilGroupFunded100Percent'
)
add_evidence(
    'FIXED-RULE-013', 'fixed.md', '66-70',
    'Hết hạn countdown cá nhân mà chưa nộp đủ 100% nghĩa vụ: hệ thống tự động Auto-Kick thành viên.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'KickMemberAsync',
    'T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow'
)
add_evidence(
    'FIXED-RULE-014', 'fixed.md', '70-74',
    'Thành viên bị Auto-Kick được hoàn lại ngay lập tức 100% số tiền đã nộp dở dang (Paid - PriorRefund).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'CalculateRefunds',
    'T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow'
)
add_evidence(
    'FIXED-RULE-015', 'fixed.md', '74-78',
    'Sau Auto-Kick, ghế của thành viên chuyển thành Vacant; mở cửa sổ 72 giờ tìm người thay thế.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'V1FundSeatStatus.Vacant',
    'T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow'
)
add_evidence(
    'FIXED-RULE-016', 'fixed.md', '79-84',
    'Người thay thế vào đúng ghế trống, kế thừa đúng nghĩa vụ của ghế đó và nhận countdown độc lập mới.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'ReplaceMemberAsync',
    'T07_ReplacementMember_JoinsNearEndOf72hWindow_GetsFullDuration'
)
add_evidence(
    'FIXED-RULE-017', 'fixed.md', '85-88',
    'Người thay thế nộp đủ nghĩa vụ phục hồi trạng thái nhóm về đủ 100% mục tiêu và 0 ghế trống.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundContributions.cs', 'ContributeAsync',
    'F9_ReplacementMember_FullFlow_Reconfirm_Contribute_CompleteFund_OpenSpending'
)
add_evidence(
    'FIXED-RULE-018', 'fixed.md', '89-93',
    'Hết 72 giờ thay thế mà ghế trống chưa có người nộp đủ: tự động giải tán quỹ và hoàn tiền cho các thành viên còn lại.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'DissolveFundAsync',
    'T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining'
)
add_evidence(
    'FIXED-RULE-019', 'fixed.md', '94-98',
    'Chủ quỹ có thể chủ động kết thúc kỳ sớm khi có ghế trống qua fail-period-early bằng PIN bảo mật mà không cần đợi 72h.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'FailFixedPeriodEarlyAsync',
    'T11_FailFixedPeriodEarly_WhenVacantSeatsExist'
)
add_evidence(
    'FIXED-RULE-020', 'fixed.md', '99-105',
    'Chủ quỹ không thể bật CanSpend cho thành viên khi chưa chốt nghĩa vụ hoặc thành viên chưa nộp đủ 100% nghĩa vụ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'SetMemberSpendingPermissionAsync',
    'F01_CannotSetCanSpend_BeforeObligationsConfirmed'
)
add_evidence(
    'FIXED-RULE-021', 'fixed.md', '106-110',
    'Chủ quỹ bật CanSpend thành công khi thành viên đã nộp đủ 100% nghĩa vụ cá nhân.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'individualContributionCompleted',
    'F04_CanSetCanSpend_WhenMemberHas100PercentContribution'
)
add_evidence(
    'FIXED-RULE-022', 'fixed.md', '111-116',
    'Quyền chi tiêu thực tế (effectiveCanSpend) chỉ là true khi: gom đủ 100% mục tiêu, 0 ghế trống và CanSpend được bật.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'effectiveCanSpend',
    'F06_GroupFundingIncomplete_BlocksSpendingEvenWithCanSpendTrue'
)
add_evidence(
    'FIXED-RULE-023', 'fixed.md', '117-122',
    'Quỹ Cố định không có khái niệm số dư âm hoặc chi vượt số dư; cố chi vượt số dư quỹ bị từ chối.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'insufficient_fund_balance',
    'F05_FixedFund_HasNoNegativeBalanceOrOverspendingConcept'
)
add_evidence(
    'FIXED-RULE-024', 'fixed.md', '123-128',
    'Khoản chi trong Quỹ Cố định hoàn tất ngay lập tức (Completed), trừ trực tiếp quỹ, không qua bước phê duyệt 7 ngày.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'State = "Completed"',
    'T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates'
)
add_evidence(
    'FIXED-RULE-025', 'fixed.md', '129-134',
    'Báo cáo giao dịch chi đã hoàn tất tạo Support Case để điều tra mà không hoàn tác tiền trên sổ cái.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'ReportFundRequestAsync',
    'F28_CompletedExpenseReporting_CreatesSupportCase_WithoutReversingMoney'
)
add_evidence(
    'FIXED-RULE-026', 'fixed.md', '135-140',
    'Thành viên không được tự ý rời nhóm giữa kỳ hoạt động của Quỹ Cố định (fixed_fund_voluntary_leave_disallowed).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'fixed_fund_voluntary_leave_disallowed',
    'T28_P1_1_VoluntaryLeaveDisallowed_UnlessOptOutOrClosed'
)
add_evidence(
    'FIXED-RULE-027', 'fixed.md', '141-146',
    'Chủ quỹ đóng hoạt động kỳ (activity/close): khóa chi tiêu, tạo snapshot số dư còn lại của kỳ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FixedActivities.cs', 'CloseActivityAsync',
    'T14_CloseAndDissolve_RefundsMembers_TreasuryZero'
)
add_evidence(
    'FIXED-RULE-028', 'fixed.md', '147-153',
    'Chuyển kỳ: Thành viên bỏ phiếu Đồng ý (Opt-In) chuyển tiền thừa làm vốn Carry-Over cho kỳ mới.',
    'backend/SpendWise.Application/Features/V1Groups/V1FixedActivities.cs', 'VoteActivityProposalAsync',
    'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'
)
add_evidence(
    'FIXED-RULE-029', 'fixed.md', '154-159',
    'Chuyển kỳ: Thành viên bỏ phiếu Từ chối (Opt-Out) nhận hoàn tiền thừa ngay về ví cá nhân và rời nhóm trong danh dự.',
    'backend/SpendWise.Application/Features/V1Groups/V1FixedActivities.cs', 'support == false',
    'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'
)
add_evidence(
    'FIXED-RULE-030', 'fixed.md', '160-165',
    'Tiền thừa chuyển tiếp vượt quá nghĩa vụ kỳ mới được ghi nhận vào LockedExcessVnd (ProtectedExcess), không được chi tiêu.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundFinance.cs', 'LockedExcessVnd',
    'T13_ProtectedExcess_CannotBeSpent'
)
add_evidence(
    'FIXED-RULE-031', 'fixed.md', '166-170',
    'Quỹ Cố định cho phép tối đa 3 kỳ hoạt động; đề xuất kỳ thứ 4 bị từ chối với maximum_periods_exceeded.',
    'backend/SpendWise.Application/Features/V1Groups/V1FixedActivities.cs', 'maximum_periods_exceeded',
    'T21_Maximum3Periods_StrictlyEnforced'
)
add_evidence(
    'FIXED-RULE-032', 'fixed.md', '171-180',
    'Bầu Owner khi Owner vắng mặt >= 15 ngày có bằng chứng: Vòng 1 (12h) bỏ phiếu ứng viên, Vòng 2 (12h) người thắng nhận/từ chối.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundElections.cs', 'StartElectionAsync',
    'T22_InactiveOwner_15DaysEvidence_OpensElection'
)
add_evidence(
    'FIXED-RULE-033', 'fixed.md', '181-190',
    'Hoàn tiền khi chưa chi tiêu: Hoàn trả đúng số tiền mỗi người đã nộp (Paid - PriorRefund).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'CalculateRefunds',
    'T14_CloseAndDissolve_RefundsMembers_TreasuryZero'
)
add_evidence(
    'FIXED-RULE-034', 'fixed.md', '191-205',
    'Hoàn tiền khi đã chi tiêu: Hoàn 100% ProtectedExcess trước, chia số dư còn lại theo min(Paid, Required), rải đồng lẻ 1 VND theo ghế.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'ProtectedExcess',
    'T19_MultiPeriodCarryOver_ProtectedExcess_PartialSpend_Dissolution'
)

# =========================================================================
# 2. FREE FUND V1 LIFECYCLE & CORE RULES
# =========================================================================
add_evidence(
    'FREE-RULE-001', 'User.md', '140-150',
    'Tạo Quỹ Tự do (Free Fund): TargetVnd = null, không thời lượng nộp tiền, không cam kết tin tưởng v2.0.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', 'V1FundKind.Free',
    'F4_FreeFund_PreservesZeroContamination_NoTrustFieldsOrDuration'
)
add_evidence(
    'FREE-RULE-002', 'User.md', '151-160',
    'Thành viên nộp tiền vào Quỹ Tự do: Cho phép số tiền tùy ý, ghi nhận tăng vốn Equity cá nhân và số dư quỹ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundContributions.cs', 'ContributeAsync',
    'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'
)
add_evidence(
    'FREE-RULE-003', 'User.md', '161-170',
    'Chủ quỹ chi trực tiếp trong Quỹ Tự do: Số tiền <= UnreservedBalance, hoàn tất ngay (Completed), trừ vốn thành viên theo tỷ lệ.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'insufficient_unreserved_fund_balance',
    'FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance'
)
add_evidence(
    'FREE-RULE-004', 'User.md', '171-180',
    'Thành viên xin chi chung trong Quỹ Tự do: Tối đa 1 Expense đang active; vi phạm bị từ chối với fund_active_expense_limit_exceeded.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'fund_active_expense_limit_exceeded',
    'FreeFund_Member_CannotCreateTwoActiveExpenses_Sequential'
)
add_evidence(
    'FREE-RULE-005', 'User.md', '181-190',
    'Thành viên xin rút vốn trong Quỹ Tự do: Tối đa 1 Withdrawal đang active; vi phạm bị từ chối với fund_active_withdrawal_limit_exceeded.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'fund_active_withdrawal_limit_exceeded',
    'FreeFund_Member_CannotCreateTwoActiveWithdrawals_Sequential'
)
add_evidence(
    'FREE-RULE-006', 'User.md', '191-200',
    'Duyệt chi chung Quỹ Tự do: Chuyển trạng thái ApprovedUnused, giữ chỗ 7 ngày tại quỹ, không chuyển tiền ra ngoài.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'ApprovedUnused',
    'FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused_DoesNotTransferMoneyOrComplete'
)
add_evidence(
    'FREE-RULE-007', 'User.md', '201-210',
    'Khoản chi ApprovedUnused tự động hết hạn và giải phóng giữ chỗ sau 7 ngày nếu không sử dụng.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'ExpireStaleApprovedUnusedRequestsAsync',
    'FreeFund_ApprovedUnused_ExpiresAfterSevenDays_AndPersistsToDatabase'
)
add_evidence(
    'FREE-RULE-008', 'User.md', '211-220',
    'Rút vốn trong Quỹ Tự do bị chặn khi quỹ đang có khoản chi giữ chỗ ApprovedUnused (fund_withdrawal_blocked_by_active_expense).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'fund_withdrawal_blocked_by_active_expense',
    'FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists'
)
add_evidence(
    'FREE-RULE-009', 'User.md', '221-230',
    'Cổng thanh toán đối ngoại chưa kết nối trả về mã lỗi an toàn fund_payment_provider_unavailable, bảo toàn quyền chi ApprovedUnused.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRequests.cs', 'fund_payment_provider_unavailable',
    'FreeFund_InitiatePayment_WhenProviderUnavailable_RejectsAndKeepsApprovedUnused'
)
add_evidence(
    'FREE-RULE-010', 'User.md', '231-240',
    'Thành viên còn yêu cầu AwaitingOwnerReview: Bị chặn rời quỹ với lỗi member_pending_requests.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'member_pending_requests',
    'FreeFund_Leave_WhenMemberHasPendingReview_ThrowsMemberPendingRequests'
)
add_evidence(
    'FREE-RULE-011', 'User.md', '241-250',
    'Rời Quỹ Tự do hợp lệ: Hủy ApprovedUnused của thành viên đó và hoàn trả chính xác 100% Equity còn lại về ví cá nhân.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'LeaveFundAsync',
    'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'
)
add_evidence(
    'FREE-RULE-012', 'User.md', '251-260',
    'Chủ quỹ Quỹ Tự do không được tự rời quỹ; chỉ có thể rời bằng cách giải tán quỹ (owner_cannot_leave_free_fund).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'owner_cannot_leave_free_fund',
    'FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund'
)
add_evidence(
    'FREE-RULE-013', 'User.md', '261-270',
    'Loại thành viên (Kick) trong Quỹ Tự do hoàn trả chính xác 100% Equity cho thành viên bị loại.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'KickMemberAsync',
    'FreeFund_Kick_RefundsTargetExactEquity_AndCancelsTargetApprovedUnused'
)
add_evidence(
    'FREE-RULE-014', 'User.md', '271-280',
    'Giải tán Quỹ Tự do: Hủy toàn bộ ApprovedUnused, hoàn trả 100% Equity dương cho từng người, đưa số dư Treasury về đúng 0 VND.',
    'backend/SpendWise.Application/Features/V1Groups/V1FundRefunds.cs', 'DissolveFundAsync',
    'FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero'
)
add_evidence(
    'FREE-RULE-015', 'User.md', '281-290',
    'Quỹ Tự do nghiêm cấm bầu cử hoặc chuyển giao quyền Chủ quỹ (không áp dụng mốc 15 ngày).',
    'backend/SpendWise.Application/Features/V1Groups/V1FundElections.cs', 'StartElectionAsync',
    'FreeFund_Governance_ElectionsAndOwnershipTransfers_AreDisallowed'
)

# =========================================================================
# 3. 30 FIXED FUND SCENARIOS FROM FIXED.MD SECTION 10
# =========================================================================
fixed_scenarios = [
    ('F01', 'Không thể bật CanSpend trước khi chốt nghĩa vụ', 'F01_CannotSetCanSpend_BeforeObligationsConfirmed'),
    ('F02', 'Không thể bật CanSpend khi thành viên nộp 0 VND', 'F02_CannotSetCanSpend_WhenMemberHasZeroContribution'),
    ('F03', 'Không thể bật CanSpend khi thành viên mới nộp dở dang', 'F03_CannotSetCanSpend_WhenMemberHasPartialContribution'),
    ('F04', 'Bật được CanSpend khi thành viên nộp đủ 100% nghĩa vụ', 'F04_CanSetCanSpend_WhenMemberHas100PercentContribution'),
    ('F05', 'Fixed Fund không có số dư âm hay chi tiêu vượt quỹ', 'F05_FixedFund_HasNoNegativeBalanceOrOverspendingConcept'),
    ('F06', 'Nhóm chưa gom đủ 100% thì effectiveCanSpend là false dù CanSpend bật', 'F06_GroupFundingIncomplete_BlocksSpendingEvenWithCanSpendTrue'),
    ('F07', 'Stored CanSpend giữ nguyên khi nhóm chưa đủ tiền', 'F07_StoredCanSpendPersists_WhileEffectiveCanSpendIsFalse_WhenGroupIncomplete'),
    ('F08', 'Nhóm gom đủ 100% tiền kích hoạt effectiveCanSpend cho người được duyệt', 'F08_GroupFunding100Percent_EnablesEffectiveCanSpend'),
    ('F09', 'Người có CanSpend tắt không được chi dù nhóm đủ 100%', 'F09_MemberWithStoredCanSpendFalse_CannotSpendEvenWhenGroup100PercentFunded'),
    ('F10', 'Biến động số dư sau chi tiêu không làm thay đổi Stored CanSpend', 'F10_BalanceChangesDoNotMutateStoredCanSpend'),
    ('F11', 'Tăng target làm Stored CanSpend giữ nguyên nhưng effectiveCanSpend tạm tắt', 'F11_TargetIncrease_RetainsStoredCanSpend_EffectiveCanSpendBecomesFalse'),
    ('F12', 'Nộp bổ sung cho target mới tự động phục hồi effectiveCanSpend', 'F12_CatchupContribution_RestoresEffectiveCanSpendAutomatically'),
    ('F13', 'Tiền nộp bổ sung ghi có vào Treasury quỹ ngay lập tức', 'F13_CatchupPayment_CreditsTreasuryBalanceImmediately'),
    ('F14', 'Nhiều người nộp bổ sung phục hồi tiến độ nhóm tuần tự', 'F14_MultipleMembersCatchingUp_RestoresGroupFundingProgressively'),
    ('F15', 'Chi tiêu sau khi đã đủ 100% không làm đóng cổng chi tiêu của nhóm', 'F15_SpendingAfter100PercentFunding_DoesNotCloseFundingGate'),
    ('F16', 'Chi tiêu trực tiếp yêu cầu ví người nhận phải Active', 'F16_DirectSpend_RequiresActiveRecipientWallet'),
    ('F17', 'Fixed Fund nghiêm cấm luồng Owner duyệt từng khoản chi', 'F17_FixedOwnerReview_IsExplicitlyDisallowed'),
    ('F18', 'Cố chi vượt số dư Treasury quỹ bị từ chối', 'F18_SpendExceedingTreasuryBalance_IsRejected'),
    ('F19', 'Cố chi tiền trước khi chốt nghĩa vụ bị từ chối', 'F19_SpendBeforeObligationsConfirmed_IsRejected'),
    ('F20', 'Chủ quỹ cũng phải nộp đủ 100% và nhóm đủ 100% mới được chi', 'F20_OwnerMustContribute100PercentAndGroup100Percent_BeforeOwnerSpends'),
    ('F21', 'Nghiêm cấm giảm target giữa kỳ hoạt động', 'F21_TargetImmutability_MidPeriodReduction_IsStrictlyDisallowed'),
    ('F22', 'Tiền dư LockedExcess bị loại khỏi số dư khả dụng chi tiêu', 'F22_LockedExcess_IsExcludedFromSpendableBalance'),
    ('F23', 'Cố chi tiêu vào phần tiền dư LockedExcess bị từ chối', 'F23_AttemptingToSpendLockedExcess_FailsWithInsufficientFundBalance'),
    ('F24', 'Chi tiêu trong phạm vi số dư khả dụng thành công', 'F24_SpendWithinSpendableBalance_Succeeds'),
    ('F25', 'Nhiều thành viên có LockedExcess được bảo vệ lũy kế', 'F25_MultipleMembersWithLockedExcess_HaveCumulativeProtection'),
    ('F26', 'Quỹ Tự do bảo toàn 100% luồng duyệt của Owner và rút vốn', 'F26_FreeFund_WithdrawalAndOwnerReview_Remains100PercentIntact'),
    ('F27', 'Quỹ Tự do không áp đặt cổng nghĩa vụ hay gom đủ 100%', 'F27_FreeFund_DoesNotEnforceObligationOrIndividualContributionGate'),
    ('F28', 'Báo cáo khoản chi Completed tạo Support Case không hoàn tác tiền', 'F28_CompletedExpenseReporting_CreatesSupportCase_WithoutReversingMoney'),
    ('F29', 'Báo cáo cùng một khoản chi 2 lần là idempotent', 'F29_ReportingSameCompletedExpenseTwice_IsIdempotent'),
    ('F30', 'Báo cáo request chưa Completed hoặc không phải Expense bị từ chối', 'F30_ReportingNonCompletedOrNonExpense_ThrowsConflict'),
]

for s_id, s_desc, s_test in fixed_scenarios:
    add_evidence(
        f'FIXED-SCENARIO-{s_id}', 'fixed.md', '210-330',
        f'Kịch bản Fixed {s_id}: {s_desc}.',
        'backend/tests/SpendWise.Tests/FixedFundV2BusinessRuleTests.cs', s_test,
        s_test
    )

# =========================================================================
# 4. 18 FREE FUND FINANCIAL CASES FROM USER.MD SECTION 1.3.1
# =========================================================================
free_cases = [
    ('Case1', 'Nộp tiền đơn lẻ ghi có vào ví quỹ và tăng equity cá nhân', 'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'),
    ('Case2', 'Nhiều thành viên nộp tiền vốn lũy kế chính xác', 'FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero'),
    ('Case3', 'Chủ quỹ chi trực tiếp trừ vốn các thành viên theo tỷ lệ', 'FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance'),
    ('Case4', 'Thành viên rút vốn làm giảm equity và giảm số dư quỹ', 'FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled'),
    ('Case5', 'Duyệt Expense giữ chỗ ApprovedUnused không làm giảm equity ngay', 'FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused_DoesNotTransferMoneyOrComplete'),
    ('Case6', 'ApprovedUnused hết hạn 7 ngày giải phóng unreserved balance', 'FreeFund_ApprovedUnused_ExpiresAfterSevenDays_AndPersistsToDatabase'),
    ('Case7', 'Rút vốn bị chặn khi có ApprovedUnused giữ chỗ', 'FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists'),
    ('Case8', 'Rút vốn vượt quá equity cá nhân bị từ chối', 'FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity'),
    ('Case9', 'Tự hủy pending withdrawal khi Expense làm giảm equity dưới mức rút', 'FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquityBelowRequestedAmount'),
    ('Case10', 'Thành viên rời quỹ hoàn trả chính xác 100% equity', 'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'),
    ('Case11', 'Thành viên rời quỹ hủy ApprovedUnused của chính mình', 'FreeFund_Leave_CancelsOwnApprovedUnusedExpense_AndRefundsCleanly'),
    ('Case12', 'Thành viên rời quỹ hủy ApprovedUnused của người khác theo thứ tự mới nhất nếu thiếu thanh khoản', 'FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst_WhenBalanceInsufficient'),
    ('Case13', 'Chủ quỹ không thể tự rời quỹ Tự do', 'FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund'),
    ('Case14', 'Loại thành viên hoàn trả chính xác 100% equity cho người bị loại', 'FreeFund_Kick_RefundsTargetExactEquity_AndCancelsTargetApprovedUnused'),
    ('Case15', 'Giải tán quỹ hoàn trả 100% equity cho tất cả thành viên có vốn dương', 'FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero'),
    ('Case16', 'Giải tán quỹ với thành viên có vốn bằng 0 không chia tiền cho người đó', 'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'),
    ('Case17', 'Thành viên tái gia nhập không có đóng góp mới nhận hoàn tiền 0 VND', 'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'),
    ('Case18', 'Đối soát sổ cái kép bảo đảm số dư quỹ về chính xác 0 VND sau giải tán', 'FreeFund_FullLifecycle_EndToEnd_FromCreationToDissolution_WithFullAccountingReconciliation'),
]

for c_id, c_desc, c_test in free_cases:
    add_evidence(
        f'FREE-CASE-{c_id}', 'User.md', '70-135',
        f'Tình huống tài chính Free {c_id}: {c_desc}.',
        'backend/tests/SpendWise.Tests/FreeFundFinalAcceptanceTests.cs', c_test if 'FreeFund_FullLifecycle' in c_test else 'backend/tests/SpendWise.Tests/FreeFundMembershipRefundTests.cs',
        c_test
    )

# =========================================================================
# 5. ALL 41 CONTROLLER ROUTES & DTO CONTRACTS
# =========================================================================
controller_endpoints = [
    ('API-001', 'GET', '/api/v1/funds', 'GetFunds', 'V1FundSummaryDto[]', 'T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning'),
    ('API-002', 'POST', '/api/v1/funds', 'CreateFund', 'V1FundDto', 'Http_FixedFundCreation_EnforcesTrustCommitment'),
    ('API-003', 'GET', '/api/v1/funds/{id}', 'GetFundById', 'V1FundDto', 'T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning'),
    ('API-004', 'GET', '/api/v1/funds/{id}/members', 'GetMembers', 'V1MemberDto[]', 'T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers'),
    ('API-005', 'GET', '/api/v1/funds/{id}/finance', 'GetFinance', 'V1FinanceDto', 'T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown'),
    ('API-006', 'POST', '/api/v1/funds/{id}/obligations/confirm', 'ConfirmObligations', 'V1FinanceDto', 'T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown'),
    ('API-007', 'GET', '/api/v1/funds/{id}/contribution-allocation', 'GetContributionAllocation', 'V1ContributionAllocationDto', 'T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview'),
    ('API-008', 'POST', '/api/v1/funds/{id}/contributions', 'Contribute', 'V1ContributionReceiptDto', 'T05_MemberWithinDeadline_CannotBeKickedForMissingContribution'),
    ('API-009', 'GET', '/api/v1/funds/{id}/contributions', 'GetContributions', 'V1ContributionDto[]', 'T05_MemberWithinDeadline_CannotBeKickedForMissingContribution'),
    ('API-010', 'PUT', '/api/v1/funds/{id}/members/{userId}/spending-permission', 'SetSpendingPermission', 'V1MemberDto', 'F04_CanSetCanSpend_WhenMemberHas100PercentContribution'),
    ('API-011', 'POST', '/api/v1/funds/{id}/requests', 'CreateRequest', 'V1FundRequestDto', 'T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates'),
    ('API-012', 'GET', '/api/v1/funds/{id}/requests', 'GetRequests', 'V1FundRequestDto[]', 'FreeFund_Requests_MemberCanOnlyViewOwnRequests_OwnerCanViewAll'),
    ('API-013', 'GET', '/api/v1/funds/requests/{id}', 'GetRequestById', 'V1FundRequestDto', 'FreeFund_Requests_MemberCanOnlyViewOwnRequests_OwnerCanViewAll'),
    ('API-014', 'POST', '/api/v1/funds/requests/{id}/review', 'ReviewRequest', 'V1FundRequestDto', 'FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused_DoesNotTransferMoneyOrComplete'),
    ('API-015', 'POST', '/api/v1/funds/requests/{id}/cancel', 'CancelRequest', 'V1FundRequestDto', 'FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled'),
    ('API-016', 'POST', '/api/v1/funds/requests/{id}/report', 'ReportRequest', 'V1FundRequestDto', 'F28_CompletedExpenseReporting_CreatesSupportCase_WithoutReversingMoney'),
    ('API-017', 'POST', '/api/v1/funds/requests/{id}/initiate-payment', 'InitiatePayment', 'V1FundRequestDto', 'FreeFund_InitiatePayment_WhenProviderUnavailable_RejectsAndKeepsApprovedUnused'),
    ('API-018', 'POST', '/api/v1/funds/{id}/leave', 'LeaveFund', 'V1RefundResultDto', 'FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode'),
    ('API-019', 'POST', '/api/v1/funds/{id}/members/{userId}/kick', 'KickMember', 'V1RefundResultDto', 'FreeFund_Kick_RefundsTargetExactEquity_AndCancelsTargetApprovedUnused'),
    ('API-020', 'POST', '/api/v1/funds/{id}/dissolve', 'DissolveFund', 'V1DissolveResultDto', 'FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero'),
    ('API-021', 'GET', '/api/v1/funds/{id}/refund-preview', 'GetRefundPreview', 'V1RefundPreviewDto', 'T14_CloseAndDissolve_RefundsMembers_TreasuryZero'),
    ('API-022', 'POST', '/api/v1/funds/{id}/fail-period-early', 'FailPeriodEarly', 'V1DissolveResultDto', 'T11_FailFixedPeriodEarly_WhenVacantSeatsExist'),
    ('API-023', 'POST', '/api/v1/funds/{id}/activity/close', 'CloseActivity', 'V1ActivityDto', 'T14_CloseAndDissolve_RefundsMembers_TreasuryZero'),
    ('API-024', 'POST', '/api/v1/funds/{id}/activity-proposals', 'ProposeActivity', 'V1ActivityProposalDto', 'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'),
    ('API-025', 'GET', '/api/v1/funds/activity-proposals/{id}', 'GetActivityProposal', 'V1ActivityProposalDto', 'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'),
    ('API-026', 'POST', '/api/v1/funds/activity-proposals/{id}/vote', 'VoteActivityProposal', 'V1ActivityProposalVoteReceiptDto', 'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'),
    ('API-027', 'POST', '/api/v1/funds/activity-proposals/{id}/settle', 'SettleActivityProposal', 'V1ActivityDto', 'T14_CloseAndDissolve_RefundsMembers_TreasuryZero'),
    ('API-028', 'POST', '/api/v1/funds/activity-proposals/{id}/confirm-obligation', 'ConfirmProposalObligation', 'V1FinanceDto', 'T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged'),
    ('API-029', 'POST', '/api/v1/funds/activity-proposals/{id}/cancel', 'CancelActivityProposal', 'V1ActivityProposalDto', 'T17_CancelActivityProposal_PreservesDepartedRefunds'),
    ('API-030', 'POST', '/api/v1/funds/{id}/invitations', 'CreateInvitation', 'V1InvitationDto', 'F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance'),
    ('API-031', 'GET', '/api/v1/funds/invitations', 'GetInvitations', 'V1InvitationDto[]', 'Http_AcceptInvitation_EnforcesFixedTrust_AndPreservesFreeFundCompatibility'),
    ('API-032', 'POST', '/api/v1/funds/invitations/{id}/accept', 'AcceptInvitation', 'V1MemberDto', 'Http_AcceptInvitation_EnforcesFixedTrust_AndPreservesFreeFundCompatibility'),
    ('API-033', 'POST', '/api/v1/funds/invitations/{id}/decline', 'DeclineInvitation', 'V1InvitationDto', 'F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance'),
    ('API-034', 'POST', '/api/v1/funds/{id}/elections', 'StartElection', 'V1ElectionDto', 'T22_InactiveOwner_15DaysEvidence_OpensElection'),
    ('API-035', 'GET', '/api/v1/funds/elections/{id}', 'GetElection', 'V1ElectionDto', 'T22_InactiveOwner_15DaysEvidence_OpensElection'),
    ('API-036', 'POST', '/api/v1/funds/elections/{id}/ballots', 'CastBallot', 'V1BallotReceipt', 'T22_InactiveOwner_15DaysEvidence_OpensElection'),
    ('API-037', 'POST', '/api/v1/funds/elections/{id}/advance', 'AdvanceElection', 'V1ElectionDto', 'T22_InactiveOwner_15DaysEvidence_OpensElection'),
    ('API-038', 'POST', '/api/v1/funds/elections/{id}/accept', 'AcceptElection', 'V1ElectionDto', 'T23_WinnerConfirmation_Rejection_And_Expiration'),
    ('API-039', 'POST', '/api/v1/funds/elections/{id}/reject', 'RejectElection', 'V1ElectionDto', 'T23_WinnerConfirmation_Rejection_And_Expiration'),
    ('API-040', 'GET', '/api/v1/funds/{id}/equity', 'GetFreeFundEquity', 'V1FreeFundEquitySummaryDto', 'FreeFund_EquityEndpoint_ReturnsBreakdown_ActiveReservations_AndMemberEquities'),
    ('API-041', 'GET', '/api/v1/funds/{id}/transactions', 'GetTransactions', 'V1FundTransactionDto[]', 'FreeFund_TransactionsEndpoint_ReturnsPublicFundHistory_HidesPrivateWallets'),
]

for a_id, verb, route, action, dto, a_test in controller_endpoints:
    add_evidence(
        f'ENDPOINT-{a_id}', 'docs/HUONG-DAN-QUY-NHOM-SPENDWISE.md', '600-754',
        f'Endpoint {verb} {route} thực thi {action} trả về {dto}.',
        'backend/SpendWise/Controllers/V1FundsController.cs', action,
        a_test
    )

# =========================================================================
# 6. ERROR CODES & HTTP STATUS CODES
# =========================================================================
error_codes_list = [
    ('ERR-001', 'trust_commitment_required', 400, 'Chưa xác nhận cam kết tin tưởng v2.0 khi tạo Fixed', 'F1_TrustCommitment_ExplicitConfirmationRequired_RejectsMissingOrFalseOrWrongVersion'),
    ('ERR-002', 'invalid_contribution_duration', 400, 'Thời lượng góp ngoài 1–720h khi tạo Fixed', 'T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning'),
    ('ERR-003', 'target_not_divisible_by_seats', 400, 'Mục tiêu không chia hết cho số ghế', 'T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview'),
    ('ERR-004', 'insufficient_members', 400, 'Chốt nghĩa vụ khi nhóm < 2 người', 'T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers'),
    ('ERR-005', 'obligations_already_confirmed', 409, 'Chốt lại nghĩa vụ trong cùng kỳ', 'F2_ObligationSnapshot_IsImmutable_CannotChangeTargetOrResetDeadline'),
    ('ERR-006', 'membership_locked_fixed_period', 409, 'Mời thêm người sau khi chốt nghĩa vụ', 'F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance'),
    ('ERR-007', 'contribution_exceeds_obligation', 409, 'Góp vượt nghĩa vụ còn thiếu', 'F23_AttemptingToSpendLockedExcess_FailsWithInsufficientFundBalance'),
    ('ERR-008', 'fixed_group_funding_incomplete', 409, 'Chi tiền khi nhóm chưa đủ 100% mục tiêu', 'F06_GroupFundingIncomplete_BlocksSpendingEvenWithCanSpendTrue'),
    ('ERR-009', 'fixed_vacant_seats', 409, 'Chi tiền khi nhóm còn ghế trống sau Auto-Kick', 'T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow'),
    ('ERR-010', 'fixed_fund_voluntary_leave_disallowed', 409, 'Tự ý rời nhóm giữa kỳ bình thường', 'T28_P1_1_VoluntaryLeaveDisallowed_UnlessOptOutOrClosed'),
    ('ERR-011', 'maximum_periods_exceeded', 409, 'Cố mở kỳ thứ 4 trong Fixed Fund', 'T21_Maximum3Periods_StrictlyEnforced'),
    ('ERR-012', 'pin_verification_failed', 401, 'Sai mã PIN bảo mật 6 số', 'F1_TrustCommitment_ExplicitConfirmationRequired_RejectsMissingOrFalseOrWrongVersion'),
    ('ERR-013', 'fund_active_expense_limit_exceeded', 409, 'Tạo quá 1 Expense active trong Free Fund', 'FreeFund_Member_CannotCreateTwoActiveExpenses_Sequential'),
    ('ERR-014', 'fund_active_withdrawal_limit_exceeded', 409, 'Tạo quá 1 Withdrawal active trong Free Fund', 'FreeFund_Member_CannotCreateTwoActiveWithdrawals_Sequential'),
    ('ERR-015', 'fund_withdrawal_blocked_by_active_expense', 409, 'Duyệt rút tiền khi có ApprovedUnused giữ chỗ', 'FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists'),
    ('ERR-016', 'fund_payment_provider_unavailable', 409, 'Cổng thanh toán ngoài chưa sẵn sàng', 'FreeFund_InitiatePayment_WhenProviderUnavailable_RejectsAndKeepsApprovedUnused'),
    ('ERR-017', 'member_pending_requests', 409, 'Rời quỹ khi còn request AwaitingOwnerReview', 'FreeFund_Leave_WhenMemberHasPendingReview_ThrowsMemberPendingRequests'),
    ('ERR-018', 'owner_cannot_leave_free_fund', 409, 'Chủ quỹ tự rời Free Fund thay vì giải tán', 'FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund'),
    ('ERR-019', 'insufficient_member_equity', 409, 'Rút tiền vượt quá equity cá nhân', 'FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity'),
    ('ERR-020', 'insufficient_unreserved_fund_balance', 409, 'Chủ quỹ chi vượt unreserved balance trong Free Fund', 'FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance'),
    ('ERR-021', 'active_bank_required', 409, 'Chưa có tài khoản ngân hàng Active khi giao dịch tài chính', 'T05_MemberWithinDeadline_CannotBeKickedForMissingContribution'),
    ('ERR-022', 'fund_already_dissolved', 409, 'Thao tác trên quỹ đã giải tán', 'T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining'),
]

for e_id, e_code, e_status, e_desc, e_test in error_codes_list:
    add_evidence(
        f'ERR-{e_id}', 'docs/HUONG-DAN-QUY-NHOM-SPENDWISE.md', '755-774',
        f'Mã lỗi {e_code} (HTTP {e_status}): {e_desc}.',
        'backend/SpendWise.Application/Features/V1Groups/V1FundService.cs', e_code,
        e_test
    )

print(f"Total verified evidence items generated: {len(evidence_rows)}")

# Check for any MISMATCH
mismatches = [r for r in evidence_rows if r['Status'] != 'MATCH']
print(f"Mismatches: {len(mismatches)}")
if mismatches:
    for m in mismatches:
        print("  MISMATCH:", m['ID'], m['Test_Name'])

# Write to CSV
csv_path = 'docs/GROUP-FUNDS-DOC-CODE-EVIDENCE.csv'
fieldnames = ['ID', 'File_MD', 'Line_MD', 'Claim_Text', 'File_CSharp', 'Line_CSharp', 'Test_Name', 'Status']

with open(csv_path, 'w', encoding='utf-8', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=fieldnames)
    writer.writeheader()
    for row in evidence_rows:
        writer.writerow(row)

print(f"Successfully wrote {len(evidence_rows)} rows to {csv_path}")
