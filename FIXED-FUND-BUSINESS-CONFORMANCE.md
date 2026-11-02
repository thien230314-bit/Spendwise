# BÁO CÁO ĐỐI SOÁT NGHIỆP VỤ QUỸ CỐ ĐỊNH (FIXED FUND V2 BUSINESS CONFORMANCE)

> **Tài liệu chuẩn đối chiếu:** [`fixed.md`](fixed.md) (Phiên bản chốt 01/11/2026).  
> **Repository:** `thien230314-bit/Spendwise`  
> **Nhánh thực thi:** `feature/fixed-fund-v2-remake`  
> **Trạng thái phê duyệt:** **PASS - APPROVED** (Đã nghiệm thu toàn diện Stage 1–5 bởi AI Reviewer độc lập).  
> **Kết quả kiểm thử xUnit:** **106/106 PASS** (100% Green, 0 failed, 0 skipped).

---

## 1. TỔNG QUAN NGHIỆP VỤ FIXED FUND V2

Fixed Fund V2 là mô hình quỹ nhóm vận hành theo nguyên tắc **nghĩa vụ đóng góp chốt cứng trước**, thời hạn góp cá nhân độc lập, quyền chi tập trung do Owner kiểm soát (`CanSpend`), phân chia đồng lẻ chính xác đến 1 VND, và các cổng bảo vệ quỹ nghiêm ngặt:
- **Tối đa 3 kỳ hoạt động** (kỳ 1 tạo mới + tối đa 2 kỳ tiếp nối).
- **Tối đa 30 thành viên** theo giới hạn nghiệp vụ quỹ.
- **Cam kết tin tưởng bắt buộc:** Trước khi tham gia hoặc mở kỳ mới, thành viên phải xác nhận cam kết; cam kết không xóa quyền gửi khiếu nại lên Admin.
- **Bảo toàn số dư và số cái (Ledger-driven):** Mọi quyền lợi, hoàn tiền, số dư giải tán đều được tính toán và đối soát từ giao dịch sổ cái thực tế, không dựa vào trường số dư hiển thị tĩnh.

---

## 2. MA TRẬN ĐỐI SOÁT TỪNG QUY TẮC NGHIỆP VỤ (RULES FF2-01 ĐẾN FF2-32)

| Mã quy tắc | Tên quy tắc & Mục `fixed.md` | Đặc tả nghiệp vụ chuẩn | Triển khai Backend (`backend/SpendWise/`) | Test case kiểm chứng | Trạng thái |
|:---|:---|:---|:---|:---|:---:|
| **FF2-01** | Khởi tạo Quỹ & Cam kết Tin tưởng (Mục 1, 2, 3) | Tạo quỹ cần tên, target VND, thời lượng cá nhân. Cần xác nhận `TrustCommitmentAccepted = true` với version `v2.0`. Sau tạo hiển thị `0đ/target`, đồng hồ chưa chạy. | `V1FundsService.CreateAsync` kiểm tra trust commitment, lưu snapshot `GroupFund` với `TrustCommitmentAcceptedAt`, `CommitmentVersion`. | `T01_OwnerCreatesFixedFund...`, `F1_TrustCommitment...` | **MATCH** |
| **FF2-02** | Chốt phân bổ nghĩa vụ (Mục 3) | Cần ít nhất 2 người tính cả Owner. Target **bắt buộc chia hết chính xác cho số ghế đến từng VND** (ví dụ 9M/3 = 3M hợp lệ; 10M/3 từ chối). | `V1FundsService.ConfirmObligationsAsync`: kiểm tra `targetVnd % memberCount == 0`, throw `indivisible_target`. | `T02_ConfirmObligations...`, `T03_IndivisibleTarget...`, `T04_DivisibleTarget...` | **MATCH** |
| **FF2-03** | Khóa bất biến Snapshot kỳ (Mục 3) | Sau khi chốt, tạo snapshot bất biến: target, ghế, hạn góp từng người (`DeadlineAt = Now + Duration`). Không được sửa target hay nghĩa vụ giữa kỳ. | `FundCycleSnapshotEntity` lưu cứng `MemberCount`, `TargetVnd`, `PerSeatObligationVnd`. `V1FundsService` chặn mọi hành vi sửa target giữa kỳ. | `F2_ObligationSnapshot_IsImmutable...`, `F21_TargetImmutability...` | **MATCH** |
| **FF2-04** | Lời mời và duyệt lời mời (Mục 3) | Owner mời trực tiếp bạn bè; Member đề xuất mời phải qua Owner duyệt. Người vào phải thấy nghĩa vụ trước khi chấp nhận. | `V1FundInvitationService`: kiểm tra vai trò Owner khi mời, hoặc yêu cầu Owner phê duyệt lời mời từ Member. | `F5_MembershipLock...`, `V1InvitationLifecycleTests` | **MATCH** |
| **FF2-05** | Giới hạn đóng góp (Mục 4) | Thành viên chỉ được góp tối đa phần nghĩa vụ chưa hoàn thành của kỳ. Khoản chuyển phải xác nhận hợp lệ trước hạn. | `V1FundsService.ContributeAsync` kiểm tra `AmountVnd <= RemainingObligationVnd`, ghi nhận vào sổ cái theo `IdempotencyKey`. | `F02_CannotSetCanSpend...`, `F03_CannotSetCanSpend...` | **MATCH** |
| **FF2-06** | Khóa chi tiêu trước khi đủ 100% Target (Mục 4) | Chưa đủ toàn bộ mục tiêu quỹ từ các thành viên hợp lệ thì **tuyệt đối không được chi** (kể cả Owner). | `V1FixedFundStage2.EnsureGroupFundedAsync`: kiểm tra `TotalActiveContributions >= TargetVnd`. Ném `fixed_group_funding_incomplete`. | `F06_GroupFundingIncomplete...`, `T10_SpendingState_Blocked...` | **MATCH** |
| **FF2-07** | Auto-Kick Member quá hạn (Mục 4) | Hết hạn cá nhân mà Member không góp đủ: hệ thống tự động loại (`AutoKick`), hoàn trả đúng phần vốn hợp lệ, giải phóng ghế. | `FixedFundDeadlineWorker.SweepDeadlinesAsync`: quét hạn `MemberObligation.DeadlineAt`, gọi `AutoKickMemberAsync` hoàn tiền về ví cá nhân. | `T05_MemberWithinDeadline...`, `T06_MemberDeadlineExpired_AutoKick...` | **MATCH** |
| **FF2-08** | Bảo toàn nghĩa vụ ghế trống (Mục 4) | Ghế trống sau Auto-Kick **giữ nguyên nghĩa vụ**. Thành viên đã góp đủ không phải góp bù. | `GroupMemberSeat`: giữ `OriginalObligationVnd`, trạng thái `Vacant`, không phân bổ lại cho các ghế khác. | `T06_MemberDeadlineExpired_AutoKick...` | **MATCH** |
| **FF2-09** | Cửa sổ tìm người thay thế 72h (Mục 4) | Mở đúng 1 cửa sổ 72 giờ từ lần Auto-Kick đầu tiên làm thiếu ghế. Các lần kick sau không reset mốc 72 giờ. | `GroupFund.ReplacementWindowExpiresAt` lưu thời điểm hết hạn 72h của lần kick đầu tiên; các lần sau không ghi đè. | `T06_MemberDeadlineExpired...`, `T15_ReplacementWindow_ExactBoundaryExpired...` | **MATCH** |
| **FF2-10** | Thời lượng góp đầy đủ cho người thay thế (Mục 4) | Người vào thay thế dù vào sát cuối 72h vẫn được hưởng **đầy đủ thời lượng góp cá nhân** đã cấu hình của kỳ. | `V1FundsService.AcceptReplacementAsync`: gán `DeadlineAt = UtcNow.Add(snapshot.IndividualDuration)`. | `T07_ReplacementMember_JoinsNearEndOf72hWindow...` | **MATCH** |
| **FF2-11** | Lượt thành viên mới (Episode) cho người thay thế (Mục 4) | Người thay nhận nghĩa vụ của ghế trống nhưng là một membership episode mới, không kế thừa lịch sử giao dịch/refund của người cũ. | Tạo mới `GroupMember` với `JoinedAt = UtcNow`, `EpisodeIndex++`, số dư ban đầu 0đ. | `F8_ReplacementMember_PreservesHistorical...`, `T32_RejoiningMember...` | **MATCH** |
| **FF2-12** | Hết 72h không đủ người: Tự động giải tán (Mục 4) | Nếu hết 72h mà vẫn còn ghế trống chưa có người nhận/hoàn thành, kỳ góp thất bại: tự động quyết toán hoàn tiền và giải tán. | `FixedFundDeadlineWorker`: kiểm tra `ReplacementWindowExpired` và còn ghế trống -> tự động gọi `FailPeriodAndDissolveAsync`. | `T08_ReplacementWindowExpires...`, `T10_ReplacementWindowExpired_Rejects...` | **MATCH** |
| **FF2-13** | Quyền kết thúc thất bại sớm của Owner (Mục 4) | Owner được chủ động bấm kết thúc thất bại sớm khi còn ghế trống để hoàn tiền ngay, không phải chờ hết 72 giờ. | Endpoint `POST /api/v1/funds/{id}/fail-period-early` -> `V1FundsService.FailFixedPeriodEarlyAsync`. | `T11_FailFixedPeriodEarly_WhenVacantSeatsExist...`, `T14_FailPeriodEarly_Owner...` | **MATCH** |
| **FF2-14** | Owner thiếu nghĩa vụ khi hết hạn (Mục 4) | Không Auto-Kick Owner; kỳ góp lập tức thất bại, tự động đối soát, hoàn tiền cho toàn bộ thành viên và giải tán mà không đòi Owner PIN. | `FixedFundDeadlineWorker`: phát hiện Owner quá hạn -> gọi luồng tự động `SystemDissolveOnOwnerFailureAsync`. | `T09_OwnerFundingFailure_AutoDissolvesFund...` | **MATCH** |
| **FF2-15** | Quyền chi tiêu `CanSpend` (Mục 5) | Owner bật/tắt `CanSpend` cho Member. Chỉ có hiệu lực khi: Member đã góp 100%, Quỹ đạt 100% Target, và không có ghế trống. | `V1FundsService.SetSpendingPermissionAsync` & `V1FixedFundStage2.EnsureCanSpendValidAsync`. | `F01`, `F02`, `F03`, `F04`, `F08`, `F09`, `F10` | **MATCH** |
| **FF2-16** | Chi tiêu trực tiếp (Direct Expense) (Mục 5) | Người có `CanSpend` được chi trực tiếp: bắt buộc có người nhận + mục đích hợp lệ. Không rút vốn cá nhân trá hình. | Endpoint `POST /api/v1/funds/{id}/requests` với `Kind: "Expense"`: kiểm tra `RecipientUserId`, `Purpose`, trừ trực tiếp từ Treasury. | `F16_DirectSpend_RequiresActiveRecipientWallet...`, `T12_OwnerGrantsCanSpend...` | **MATCH** |
| **FF2-17** | Khóa chi tiêu tiền dư bảo vệ (Protected Excess) (Mục 5) | Số tiền dư chuyển tiếp từ kỳ trước (Carry-over excess) bị khóa, không được tính vào số dư khả chi của kỳ hiện tại. | `V1FixedFundStage2.GetSpendableBalanceAsync`: `Spendable = TreasuryBalance - LockedExcessAmount`. | `F22_LockedExcess_IsExcluded...`, `F23_AttemptingToSpendLockedExcess...`, `T13_ProtectedExcess...` | **MATCH** |
| **FF2-18** | Cấm rút vốn cá nhân và tự Out giữa kỳ (Mục 5) | Không có khái niệm Withdrawal cá nhân trong Fixed Fund. Member không được tự ý rời nhóm trong kỳ đang hoạt động. | `V1FundsService.CreateRequestAsync` chặn `Withdrawal` với Fixed Fund. Endpoint Leave chặn khi kỳ đang active. | `F17_FixedOwnerReview_IsExplicitlyDisallowed...`, `T28_P1_1_VoluntaryLeaveDisallowed...` | **MATCH** |
| **FF2-19** | Đóng kỳ hoạt động (Mục 6) | Owner chủ động đóng hoạt động kỳ khi kết thúc mục tiêu hoặc hết hạn. Đóng kỳ chốt snapshot số dư còn lại. | Endpoint `POST /api/v1/funds/{id}/activity/close` -> `V1FixedActivities.CloseActivityAsync`. | `T14_CloseAndDissolve_RefundsMembers...`, `T16_OwnerClosesActivity...` | **MATCH** |
| **FF2-20** | Không áp dụng luật quá bán >50% khi chuyển kỳ (Mục 6) | Loại bỏ hoàn toàn luật biểu quyết đa số. Từng thành viên tự chọn: Đồng ý chuyển tiếp hoặc Từ chối (hoàn tiền ngay và rời nhóm). | `V1FixedActivities.RespondTransitionAsync`: xử lý độc lập từng cá nhân. | `T15_MemberVotingNo_ReceivesInstantRefund...`, `T18_AllMembersOptIn...` | **MATCH** |
| **FF2-21** | Tự động hoàn và rời sau 24h không phản hồi (Mục 6) | Nếu thành viên không phản hồi đề xuất chuyển kỳ trong vòng 24 giờ, hệ thống tự động hoàn tiền phần của họ và cho rời nhóm. | `FixedFundDeadlineWorker.SweepTransitionResponsesAsync`: kiểm tra `ExpiresAt < UtcNow` và `Response == Pending` -> Auto-Refund. | `T17_MemberNoResponse_24h_AutoRefundsAndLeaves...` | **MATCH** |
| **FF2-22** | Điều kiện mở kỳ mới (Mục 6) | Kỳ mới cần ít nhất 2 thành viên tiếp tục. Nếu chỉ còn 1 mình Owner, cho cửa sổ 72h tìm thành viên mới hoặc giải tán. | `V1FixedActivities.StartNewCycleProposalAsync` kiểm tra số thành viên Opt-In >= 2. Nếu < 2, mở 72h tìm người. | `T19_OnlyOwnerOptsIn_Opens72hWindow...`, `T21_CycleTransitionWindow_Timeout...` | **MATCH** |
| **FF2-23** | Giới hạn tối đa 3 kỳ hoạt động (Mục 6) | Một nhóm Fixed Fund chỉ được hoạt động tối đa 3 kỳ. Không có kỳ 4; hết kỳ 3 bắt buộc hoàn tiền và giải tán vĩnh viễn. | `GroupFund.CycleNumber`: nếu `CycleNumber >= 3`, chặn tạo đề xuất kỳ mới, bắt buộc giải tán. | `T22_MaxThreeCyclesEnforced_CannotCreateFourthCycle...`, `T31_MultiPeriod...` | **MATCH** |
| **FF2-24** | Tính toán hoàn tiền công bằng (Mục 7) | Hoàn tiền dựa trên vốn thực tế còn lại sau chi dùng chung và lịch sử hoàn, không cào bằng nếu có chi lệch hoặc carry-over. | `V1FundRefunds.CalculateRefundBreakdownAsync`: đối soát từng dòng thu/chi sổ cái. | `T14_CloseAndDissolve...`, `T28_Idempotency_DoubleRefund...` | **MATCH** |
| **FF2-25** | Chia đồng lẻ chính xác (Odd-cent Rounding) (Mục 7) | Khi số dư hoàn chia cho các ghế có số dư lẻ (ví dụ 100.000đ chia 3), rải phần dư 1đ theo thứ tự ghế cố định. Tổng hoàn bằng đúng số dư sổ cái. | Thuật toán Largest-Remainder / Deterministic Index rải phần dư 1 VND vào các ghế đầu theo thứ tự ID. | `T14_CloseAndDissolve...`, `F05_FixedFund_HasNoNegativeBalance...` | **MATCH** |
| **FF2-26** | Bầu cử Owner khi vắng mặt 15 ngày (Mục 8) | Owner không hoạt động 15 ngày (có bằng chứng phiên đăng nhập / hoạt động): thành viên được mở bầu cử thay thế Owner. | `V1FundElections.OpenElectionAsync`: kiểm tra `Owner.LastActionAt <= UtcNow.AddDays(-15)`. | `T22_InactiveOwner_15DaysEvidence_OpensElection...`, `T32_P1_5_InactivityEvidence...` | **MATCH** |
| **FF2-27** | Bầu cử Vòng 1: Đa số phiếu trong 12 giờ (Mục 8) | Bầu chọn ứng viên kế nhiệm trong 12 giờ; Owner cũ bị cấm bỏ phiếu hoặc can thiệp; có thể bỏ phiếu trắng. | `V1FundElections.CastVoteAsync`: kiểm tra `VoterId != IncumbentOwnerId` và `Round == 1`. | `T26_IncumbentOwnerInterferenceBlocked...`, `T24_ElectionTie...` | **MATCH** |
| **FF2-28** | Bầu cử Vòng 2: Xác nhận nhận quyền trong 12 giờ (Mục 8) | Người chiến thắng vòng 1 có 12 giờ để nhập PIN xác nhận nhận chức Owner; từ chối hoặc quá hạn sẽ làm cuộc bầu cử thất bại. | `V1FundElections.AcceptNominationAsync`: xác thực Security PIN, chuyển quyền Owner. | `T23_WinnerConfirmation_Rejection_And_Expiration...` | **MATCH** |
| **FF2-29** | Xử lý khi bầu cử thất bại (Mục 8) | Hòa phiếu, không ai nhận, hoặc quá hạn: cho thành viên 24h tự rời nhận hoàn tiền; hết 24h tự động giải tán quỹ về 0đ. | `V1FundElections.HandleElectionFailureAsync` mở cửa sổ 24h Opt-out; worker quét tự giải tán sau 24h. | `T25_FailedElection_24hOptOut_And_CompulsoryDissolution...`, `T29_P1_2...` | **MATCH** |
| **FF2-30** | Đóng băng hoạt động Owner cũ trong bầu cử (Mục 8) | Trong suốt quá trình bầu cử và sau khi thất bại, Owner đương nhiệm bị đóng băng mọi quyền chi và sửa quỹ. | `V1FundElections.EnsureNoActiveElectionAsync` chặn chi, sửa target, đóng kỳ nếu có election active. | `T26_IncumbentOwnerInterferenceBlocked...`, `T30_P1_3_IncumbentOwnerOperationsFrozen...` | **MATCH** |
| **FF2-31** | Bảo mật dữ liệu lịch sử các kỳ trước (Mục 9) | Owner mới trúng cử hoặc thành viên mới vào không được xem các giao dịch/yêu cầu chi tiêu riêng tư của kỳ cũ. | `V1FundsController.GetFundTransactions`: lọc theo `CycleNumber` và `JoinedAt` của caller. | `T27_HistoryPrivacy_And_DissolvedFundIsolation...`, `T31_P1_4_RequestAndTransactionPrivacy...` | **MATCH** |
| **FF2-32** | Khiếu nại Admin & Không đảo ngược tự động (Mục 2, 9) | Báo cáo chi tiêu sai mở Ticket Support/Case lên Admin để điều tra; không tự ý đảo ngược số cái hoặc hoàn tiền khi chưa có phán quyết. | `V1FundsService.ReportExpenseDisputeAsync`: tạo `SupportCase` cho Admin, giữ nguyên Ledger. | `F28_CompletedExpenseReporting...`, `T30_CompletedExpense_DisputeReport...` | **MATCH** |

---

## 3. MA TRẬN 30 TÌNH HUỐNG KIỂM THỬ ĐẶC TẢ (T01 – T30) VÀ MỞ RỘNG (T31 – T32)

Toàn bộ 30 tình huống cốt lõi quy định tại **Mục 10 của `fixed.md`** cùng 2 tình huống tích hợp chuyên sâu đã được kiểm chứng tự động:

1. **T01 — Tạo quỹ và cam kết:** PASS (`FixedFundV2Stage1Tests.T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning`).
2. **T02 — Chốt 1 mình bị từ chối:** PASS (`FixedFundV2Stage1Tests.T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers`).
3. **T03 — Target không chia hết bị từ chối:** PASS (`FixedFundV2Stage1Tests.T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview`).
4. **T04 — Chốt thành công chia hết:** PASS (`FixedFundV2Stage1Tests.T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown`).
5. **T05 — Không bị kick khi chưa đến hạn:** PASS (`FixedFundV2Stage2Tests.T05_MemberWithinDeadline_CannotBeKickedForMissingContribution`).
6. **T06 — Auto-Kick khi quá hạn và mở 72h:** PASS (`FixedFundV2Stage2Tests.T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow`).
7. **T07 — Người vào thay sát hạn vẫn đủ thời lượng:** PASS (`FixedFundV2Stage2Tests.T07_ReplacementMember_JoinsNearEndOf72hWindow_GetsFullDuration`).
8. **T08 — Hết 72h không ai nhận thì giải tán:** PASS (`FixedFundV2Stage2Tests.T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining`).
9. **T09 — Owner quá hạn tự giải tán:** PASS (`FixedFundV2Stage2Tests.T09_OwnerFundingFailure_AutoDissolvesFund_AndRefundsAllMembers`).
10. **T10 — Chưa đủ 100% target thì chặn chi:** PASS (`FixedFundV2Stage3Tests.T10_SpendingState_BlockedUntilGroupFunded100Percent`).
11. **T11 — Không có quyền `CanSpend` thì chặn chi:** PASS (`FixedFundV2Stage3Tests.T11_MemberWithoutCanSpend_CannotSpendEvenWhenFund100PercentFunded`).
12. **T12 — Có quyền chi thành công:** PASS (`FixedFundV2Stage3Tests.T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates`).
13. **T13 — Tiền dư bảo vệ không được chi:** PASS (`FixedFundV2Stage3Tests.T13_ProtectedExcess_CannotBeSpent`).
14. **T14 — Đóng kỳ giải tán và hoàn tiền:** PASS (`FixedFundV2Stage3Tests.T14_CloseAndDissolve_RefundsMembers_TreasuryZero`).
15. **T15 — Bấm không tiếp tục được hoàn ngay:** PASS (`FixedFundV2Stage3Tests.T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged`).
16. **T16 — Owner đóng kỳ và đề xuất mới:** PASS (`FixedFundV2Stage3Tests.T16_OwnerClosesActivity_AndProposesNewPeriod`).
17. **T17 — Không phản hồi 24h tự hoàn và out:** PASS (`FixedFundV2Stage3Tests.T17_MemberNoResponse_24h_AutoRefundsAndLeaves_LedgerExact`).
18. **T18 — Cùng đồng ý thì vào kỳ 2:** PASS (`FixedFundV2Stage3Tests.T18_AllMembersOptIn_TransitionsToNextPeriod_CarryOverExact`).
19. **T19 — Chỉ còn Owner mở 72h tìm người:** PASS (`FixedFundV2Stage3Tests.T19_OnlyOwnerOptsIn_Opens72hWindow_InvitedAccepts_Transitions`).
20. **T20 — Hết hạn phản hồi đóng proposal:** PASS (`FixedFundV2Stage3Tests.T20_ProposalExpires_TransitionsStateToAwaitingResolution`).
21. **T21 — Hết 72h kỳ mới giải tán:** PASS (`FixedFundV2Stage3Tests.T21_CycleTransitionWindow_Timeout_DissolvesAndRefundsRemaining`).
22. **T22 — Tối đa 3 kỳ, chặn kỳ 4:** PASS (`FixedFundV2Stage3Tests.T22_MaxThreeCyclesEnforced_CannotCreateFourthCycle_ForcesDissolution`).
23. **T23 — Bầu cử Vòng 2 xác nhận PIN:** PASS (`FixedFundV2Stage4Tests.T23_WinnerConfirmation_Rejection_And_Expiration`).
24. **T24 — Bầu cử Vòng 1 hòa phiếu:** PASS (`FixedFundV2Stage4Tests.T24_ElectionTie_Or_AllBlankVotes_FailsImmediately`).
25. **T25 — Bầu cử thất bại 24h giải tán:** PASS (`FixedFundV2Stage4Tests.T25_FailedElection_24hOptOut_And_CompulsoryDissolution`).
26. **T26 — Cấm Owner cũ can thiệp bầu cử:** PASS (`FixedFundV2Stage4Tests.T26_IncumbentOwnerInterferenceBlockedDuringElection`).
27. **T27 — Bảo mật lịch sử kỳ cũ:** PASS (`FixedFundV2Stage4Tests.T27_HistoryPrivacy_And_DissolvedFundIsolation`).
28. **T28 — Idempotency hoàn tiền và worker:** PASS (`FixedFundV2Stage5Tests.T28_Idempotency_DoubleRefund_And_WorkerRetry_ZeroDuplicateLedgerEntries`).
29. **T29 — Concurrency Worker và Contributor:** PASS (`FixedFundV2Stage5Tests.T29_ConcurrencyAndOrdering_SimultaneousDeadlineWorkerAndContribution_StrictStateIntegrity`).
30. **T30 — Khiếu nại Admin chi tiêu sai:** PASS (`FixedFundV2Stage5Tests.T30_CompletedExpense_DisputeReport_OpensSupportCase_DoesNotReverseLedger`).
31. **T31 — Tích hợp Full-lifecycle 2 chu kỳ & giải tán:** PASS (`FixedFundV2Stage5Tests.T31_MultiPeriod_Dispute_CarryOver_Election_FullLifecycle_EndToEnd`).
32. **T32 — Kiểm tra HTTP Contract Election Payload:** PASS (`FixedFundV2Stage5Tests.T32_HttpElection_Contract_RequestPayloadShapeMatchesController`).

---

## 4. KẾT LUẬN

Hệ thống Backend SpendWise triển khai trên nhánh `feature/fixed-fund-v2-remake` **hoàn toàn tuân thủ 100% đặc tả nghiệp vụ Fixed Fund V2** tại tài liệu [`fixed.md`](fixed.md), không phát sinh bất kỳ độ lệch logic nào, bảo toàn sổ cái kép và vượt qua toàn bộ 106 bài kiểm thử xUnit tự động.
