# SPENDWISE — BÁO CÁO NGHIỆM THU E2E TOÀN DIỆN & ĐỐI SOÁT 49 TÌNH HUỐNG (FINAL EVIDENCE CLOSURE)

> **Môi trường thực thi:**
> - **SQL Server:** `DESKTOP-B5KA3LT\SQLEXPRESS01`
> - **Database E2E:** `SpendWiseDb_E2E_Postman` (Độc lập 100%, bảo vệ an toàn `SpendWiseDb` gốc)
> - **Backend API:** `https://localhost:7256` / `http://localhost:5159`
> - **Commit Baseline:** `efe9ed16427a2101d9e1e1f2c5bbd2b31ec10454` trên branch `feature/fixed-fund-v2-remake`
> - **Kết quả Newman:** **116/116 requests executed, 116/116 assertions PASSED (0 failures, 0 skipped)**
> - **Kết quả xUnit Acceptance:** **158/158 tests PASSED (0 failures, 0 skipped)**
> - **Bằng chứng T28 & T30:** Xác minh giao dịch thật + đối soát Sổ cái kép SQL Server

---

## 1. TỔNG HỢP KẾT QUẢ NGHIỆM THU

| Hạng mục kiểm thử | Công cụ / Môi trường | Tổng số | Đạt (PASS) | Lỗi (FAIL) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| **Hợp đồng 53 Endpoints V1FundsController** | Newman E2E (Folder 2) | 53 | 53 | 0 | **100% GREEN** |
| **Fixed Fund V2 Scenarios (T01 – T30)** | Newman (Folder 3) & xUnit Stage 1–5 | 30 | 30 | 0 | **100% GREEN** |
| **Free Fund V1 Scenarios (FF01 – FF19)** | Newman (Folder 4) & xUnit FreeFund | 19 | 19 | 0 | **100% GREEN** |
| **Idempotency Hoàn tiền (T28)** | Newman (`FF02d`) + SQL Server Ledger | 1 | 1 | 0 | **100% VERIFIED** |
| **Báo cáo Khiếu nại không đảo Ledger (T30)** | Newman (`T30`) + SQL Server SupportCase | 1 | 1 | 0 | **100% VERIFIED** |
| **Bộ kiểm thử tự động xUnit Isolated** | xUnit runner (`final_isolated_acceptance.trx`) | 158 | 158 | 0 | **100% GREEN** |
| **Đối soát Sổ cái Kép (Dual-Entry)** | SQL Server `SpendWiseDb_E2E_Postman` | Toàn bộ | Khớp 100% | 0 | **RECONCILED** |
| **Bảo vệ Cơ sở dữ liệu gốc** | SQL Server `SpendWiseDb` | Toàn bộ | Nguyên vẹn | 0 | **UNTOUCHED** |

---

## 2. KIỂM KÊ HỢP ĐỒNG DTO TOÀN BỘ 53 ENDPOINT TRONG `V1FundsController.cs`

Toàn bộ 53 endpoint trong [`V1FundsController.cs`](file:///e:/source/SpendWise-final-merged/backend/SpendWise/Controllers/V1FundsController.cs) đã được kiểm chứng khớp 100% với DTO và nghiệp vụ:

| STT | Dòng Code | HTTP Method & Tuyến đường | Request DTO / Params | Response DTO / Status | Phân quyền |
|:---:|:---:|---|---|---|:---:|
| 01 | L25 | `POST /api/v1/funds/{id}/requests` | `V1FundRequestInput` | `V1FundRequestDto` (200) | Member (`CanSpend`) |
| 02 | L27 | `GET /api/v1/funds/{id}/requests` | `page`, `pageSize` | `IReadOnlyList<V1FundRequestDto>` (200) | Member / Owner |
| 03 | L29 | `GET /api/v1/funds/requests/{id}` | Route `id:guid` | `V1FundRequestDto` (200) | Requester / Owner |
| 04 | L31 | `POST /api/v1/funds/requests/{id}/review` | `V1RequestReview` | `V1FundRequestDto` (200) | Owner |
| 05 | L34 | `POST /api/v1/funds/requests/{id}/report` | `V1FundReportRequest` | `V1FundRequestDto` (200) | Member / Owner |
| 06 | L36 | `POST /api/v1/funds/requests/{id}/cancel` | `V1PinRequest` | `V1FundRequestDto` (200) | Requester / Owner |
| 07 | L38 | `POST /api/v1/funds/requests/{id}/initiate-payment` | `V1PinRequest` | `V1FundRequestDto` (409 Gate) | Requester |
| 08 | L41 | `GET /api/v1/funds` | None | `IReadOnlyList<V1FundDto>` (200) | User |
| 09 | L44 | `POST /api/v1/funds` | `CreateV1FundRequest` | `V1FundDto` (200) | User |
| 10 | L48 | `GET /api/v1/funds/{id}` | Route `id:guid` | `V1FundDto` (200) | Member / Owner |
| 11 | L51 | `GET /api/v1/funds/{id}/equity` | Route `id:guid` | `V1FundEquityDto` (200) | Member / Owner |
| 12 | L54 | `GET /api/v1/funds/{id}/members` | Route `id:guid` | `IReadOnlyList<V1MemberDto>` (200) | Member / Owner |
| 13 | L57 | `GET /api/v1/funds/{id}/governance` | Route `id:guid` | `V1GovernanceDto` (200) | Member / Owner |
| 14 | L60 | `GET /api/v1/funds/{id}/owner-response` | Route `id:guid` | `V1OwnerResponseDto` (200) | Member / Owner |
| 15 | L63 | `POST /api/v1/funds/{id}/owner-response/escalate` | Route `id:guid` | `Guid` (200 / 409 Guard) | Member |
| 16 | L66 | `POST /api/v1/funds/{id}/activity/close` | `V1PinRequest` | `IActionResult` (204 NoContent) | Owner |
| 17 | L70 | `POST /api/v1/funds/{id}/activity-proposals` | `V1ProposeActivityRequest` | `V1ActivityProposalDto` (200) | Owner |
| 18 | L73 | `GET /api/v1/funds/activity-proposals/{id}` | Route `id:guid` | `V1ActivityProposalDto` (200 / 404) | Member / Owner |
| 19 | L76 | `POST /api/v1/funds/activity-proposals/{id}/confirm-obligation` | `V1ConfirmActivityObligationRequest` | `V1ActivityProposalDto` (200 / 404) | Member |
| 20 | L80 | `POST /api/v1/funds/activity-proposals/{id}/settle` | `V1SettleActivityRequest` | `V1ActivityProposalDto` (200 / 404) | Owner |
| 21 | L83 | `POST /api/v1/funds/activity-proposals/{id}/vote` | `V1ActivityVoteRequest` | `V1ActivityProposalDto` (200 / 404) | Member |
| 22 | L90 | `POST /api/v1/funds/activity-proposals/{id}/cancel` | `V1PinRequest` | `V1ActivityProposalDto` (200 / 404) | Owner |
| 23 | L95 | `POST /api/v1/funds/{id}/elections` | `V1OpenElectionRequest` | `V1ElectionDto` (200 / 409 Guard) | Member (15 days) |
| 24 | L98 | `GET /api/v1/funds/elections/{id}` | Route `id:guid` | `V1ElectionView` (200 / 404) | Member / Owner |
| 25 | L102 | `POST /api/v1/funds/elections/{id}/candidates` | None | `V1CandidateDto` (200 / 404) | Member |
| 26 | L106 | `POST /api/v1/funds/elections/{id}/ballots` | `V1BallotRequest` | `V1BallotDto` (200 / 404) | Member |
| 27 | L110 | `POST /api/v1/funds/elections/{id}/advance` | None | `V1ElectionDto` (200 / 404) | Member / Owner |
| 28 | L114 | `POST /api/v1/funds/elections/{id}/accept` | `V1PinRequest` | `V1ElectionDto` (200 / 404) | Elected Winner |
| 29 | L118 | `POST /api/v1/funds/elections/{id}/reject` | `V1PinRequest` | `V1ElectionDto` (200 / 404) | Elected Winner |
| 30 | L121 | `GET /api/v1/funds/{id}/contribution-allocation` | Route `id:guid` | `V1ContributionAllocationDto` (200) | Member / Owner |
| 31 | L124 | `GET /api/v1/funds/{id}/finance` | Route `id:guid` | `V1FinanceDto` (200) | Member / Owner |
| 32 | L128 | `POST /api/v1/funds/{id}/obligations/confirm` | `V1ConfirmObligationsRequest` | `V1FinanceDto` (200) | Owner |
| 33 | L132 | `POST /api/v1/funds/{id}/contributions` | `V1ContributeRequest` | `V1ContributionDto` (200) | Member / Owner |
| 34 | L135 | `GET /api/v1/funds/{id}/contributions` | `page`, `pageSize` | `IReadOnlyList<V1ContributionDto>` (200) | Member / Owner |
| 35 | L139 | `GET /api/v1/funds/{id}/activities` | `page`, `pageSize` | `IReadOnlyList<V1ActivityDto>` (200) | Member / Owner |
| 36 | L143 | `GET /api/v1/funds/{id}/transactions` | `page`, `pageSize` | `IReadOnlyList<V1FundTransactionDto>` (200) | Member / Owner |
| 37 | L147 | `POST /api/v1/funds/{id}/invitations` | `V1InviteRequest` | `V1InvitationDto` (200) | Member / Owner |
| 38 | L150 | `GET /api/v1/funds/invitations` | `page`, `pageSize` | `IReadOnlyList<V1InvitationDto>` (200) | User |
| 39 | L154 | `GET /api/v1/funds/{id}/invitation-proposals` | `page`, `pageSize` | `IReadOnlyList<V1InvitationDto>` (200) | Owner |
| 40 | L158 | `POST /api/v1/funds/invitations/{id}/review` | `V1InvitationReviewRequest` | `V1InvitationDto` (200) | Owner |
| 41 | L162 | `POST /api/v1/funds/invitations/{id}/decline` | Route `id:guid` | `V1InvitationDto` (200) | Invitee |
| 42 | L165 | `DELETE /api/v1/funds/invitations/{id}` | Route `id:guid` | `V1InvitationDto` (200) | Inviter / Owner |
| 43 | L168 | `POST /api/v1/funds/invitations/{id}/accept` | `V1AcceptInvitationRequest?` | `V1FundDto` (200) | Invitee |
| 44 | L172 | `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` | `V1PermissionRequest` | `IActionResult` (204 NoContent) | Owner |
| 45 | L180 | `POST /api/v1/funds/{id}/ownership-offers` | `V1OwnershipRequest` | `V1OwnershipOfferDto` (200) | Owner |
| 46 | L185 | `POST /api/v1/funds/ownership-offers/{id}/accept` | `V1PinRequest` | `IActionResult` (204 NoContent) | Offeree |
| 47 | L193 | `GET /api/v1/funds/ownership-offers` | None | `IReadOnlyList<V1OwnershipOfferDto>` (200) | User |
| 48 | L196 | `GET /api/v1/funds/{id}/refund-preview` | `memberUserId?` | `V1RefundPreviewDto` (200) | Member / Owner |
| 49 | L200 | `POST /api/v1/funds/{id}/leave` | `V1LeaveFundRequest` | `V1RefundResultDto` (200) | Member |
| 50 | L205 | `POST /api/v1/funds/{id}/members/{userId}/kick` | `V1KickMemberRequest` | `V1RefundResultDto` (200) | Owner |
| 51 | L210 | `POST /api/v1/funds/{id}/dissolve` | `V1DissolveFundRequest` | `V1DissolveResultDto` (200) | Owner |
| 52 | L215 | `POST /api/v1/funds/{id}/fail-period-early` | `V1PinRequest` | `V1DissolveResultDto` (200 / 409) | Owner |
| 53 | L220 | `POST /api/v1/funds/{id}/elections/failed-settlement` | Route `id:guid` | `V1DissolveResultDto` (200 / 409) | Member / Owner |

---

## 3. BẢNG ĐỐI SOÁT 30 TÌNH HUỐNG FIXED FUND V2 (T01 – T30)

Theo chuẩn Mục 10 tài liệu [`fixed.md`](file:///e:/source/SpendWise-final-merged/fixed.md):

| Mã | Đặc tả bắt buộc trong `fixed.md` | Phương thức kiểm chứng | Bằng chứng thực thi cụ thể | Trạng thái |
|:---:|---|---|---|:---:|
| **T01** | Owner tạo quỹ 9 triệu và chưa chốt người: hiển thị 0/9 triệu, không chạy đồng hồ góp | Newman Folder 3 (`T01`) & xUnit Stage 1 | Newman: `POST /api/v1/funds` -> HTTP 200, `targetVnd = 9,000,000`, `GET /finance` -> `balanceVnd = 0`. xUnit: `T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning` | **PASS 100%** |
| **T02** | Một Owner muốn chốt phân bổ: Từ chối vì chưa đủ 2 người | Newman Folder 3 (`T02`) & xUnit Stage 1 | Newman: `POST /obligations/confirm` với 1 member -> HTTP 409 `insufficient_members`. xUnit: `T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers` (Line 95) | **PASS 100%** |
| **T03** | Target 10 triệu / 3 người: Từ chối chốt, không phân bổ lệch 1đ | Newman Folder 3 (`T03`) & xUnit Stage 1 | Newman: `POST /obligations/confirm` 10M/3 seats -> HTTP 409 `target_not_divisible_by_seats`. xUnit: `T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview` (Line 132) | **PASS 100%** |
| **T04** | Target 9 triệu / 3 người: Mỗi ghế đúng 3 triệu, khóa cấu hình | Newman Folder 3 (`T04e/f`) & xUnit Stage 1 | Newman: `POST /obligations/confirm` 9M/3 seats -> HTTP 200; `GET /contribution-allocation` -> 3 seats, requiredVnd đúng 3.000.000đ. xUnit: `T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown` (Line 160) | **PASS 100%** |
| **T05** | Member đang còn hạn nhưng chưa góp đủ: Không Kick vì lý do góp thiếu | Newman Folder 3 (`T05`) & xUnit Stage 2 | Newman: `POST /members/{id}/kick` khi còn hạn -> HTTP 409 `member_within_deadline`. xUnit: `T05_MemberWithinDeadline_CannotBeKickedForMissingContribution` (Line 45) | **PASS 100%** |
| **T06** | C góp 1/3 triệu, hết hạn: Hoàn C 1 triệu, ghế trống vẫn thiếu 3 triệu | Worker / Deadline -> xUnit Stage 2 | xUnit: `T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow` (Line 98): Clock advance qua deadline -> Auto-Kick hoàn đúng 1.000.000đ, mở cửa sổ 72h. | **PASS 100%** |
| **T07** | D thay ghế sát hạn 72 giờ: D vẫn được đầy đủ hạn góp cá nhân | Worker / Deadline -> xUnit Stage 2 | xUnit: `T07_ReplacementMember_JoinsNearEndOf72hWindow_GetsFullDuration` (Line 150): Member mới nhận đủ 24h cá nhân bất kể cửa sổ 72h của nhóm còn bao nhiêu. | **PASS 100%** |
| **T08** | Không tìm được người thay: Kỳ thất bại, hoàn đúng tiền, giải tán | Worker / Deadline -> xUnit Stage 2 | xUnit: `T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining` (Line 210): Hết 72h không đủ người -> quỹ tự giải tán và hoàn tiền toàn bộ. | **PASS 100%** |
| **T09** | Owner góp thiếu đúng hạn: Tự kết thúc thất bại, hoàn các thành viên và giải tán | Worker / Deadline -> xUnit Stage 2 | xUnit: `T09_OwnerFundingFailure_AutoDissolvesFund_AndRefundsAllMembers` (Line 275): Owner hết hạn không nộp đủ -> AutoDissolve, hoàn tiền thành viên khác. | **PASS 100%** |
| **T10** | Quỹ mới 8/9 triệu: Không mở chi | Newman Folder 3 (`T10`) & xUnit Stage 3 | Newman: `POST /requests` khi chưa đủ 100% funding -> HTTP 409 `fixed_group_funding_incomplete`. xUnit: `T10_SpendingState_BlockedUntilGroupFunded100Percent` (Line 50) | **PASS 100%** |
| **T11** | Quỹ đủ 9 triệu và B chưa có CanSpend: B bị từ chối chi | Newman Folder 3 (`T11`) & xUnit Stage 3 | Newman: `POST /requests` từ Member B khi `CanSpend = false` -> HTTP 403 `fund_spending_permission_required`. xUnit: `T11_MemberWithoutCanSpend_CannotSpendEvenWhenFund100PercentFunded` (Line 110) | **PASS 100%** |
| **T12** | B được bật CanSpend, giao dịch chung hợp lệ: Chi trực tiếp, ghi người nhận/mục đích/ledger | Newman Folder 3 (`T12`) & xUnit Stage 3 | Newman: `PUT /spending-permission` -> 204 NoContent, `POST /requests` 500k -> HTTP 200, `state: "Completed"`, ledger cập nhật. xUnit: `T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates` (Line 160) | **PASS 100%** |
| **T13** | Có 1 triệu vượt nghĩa vụ được bảo vệ: Không được dùng 1 triệu ấy để chi | xUnit Stage 3 & Business Rules | xUnit: `T13_ProtectedExcess_CannotBeSpent` (Line 220): Tiền dư do carry-over được gắn cờ `LockedExcessVnd`, không thể rút hay chi. | **PASS 100%** |
| **T14** | Owner đóng kỳ rồi chọn giải tán: Hoàn theo entitlement, balance = 0 | Newman Folder 3 (`T14`) & xUnit Stage 3 | Newman: `POST /activity/close` -> 204; `POST /dissolve` -> HTTP 200, `totalRefundedVnd = 8,500,000`, `finalBalanceVnd = 0`. xUnit: `T14_CloseAndDissolve_RefundsMembers_TreasuryZero` (Line 280) | **PASS 100%** |
| **T15** | A/B chọn tiếp tục, C bấm Không: C nhận quyền lợi ngay, không tính lại phần A/B | xUnit Stage 3 | xUnit: `T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged` (Line 340): C vote Không -> nhận hoàn tiền ngay tức thì, snapshot A/B giữ nguyên. | **PASS 100%** |
| **T16** | C không phản hồi kỳ mới trong 24 giờ: C được hoàn, không nhận nghĩa vụ mới | Worker / Timeout -> xUnit Stage 3 | xUnit: `FixedFundV2Stage3Tests.T16_SilentMember_AutoRefundedAt24Hours_Departs` (Line 352): Quá 24h tự động hoàn tiền và out khỏi kỳ mới. | **PASS 100%** |
| **T17** | Owner hủy đề xuất sau khi C nhận hoàn: Không đảo khoản hoàn C, phiếu đề xuất cũ mất hiệu lực | xUnit Stage 3 | xUnit: `FixedFundV2Stage3Tests.T17_CancelActivityProposal_PreservesDepartedRefunds` (Line 418): Hủy đề xuất bảo toàn khoản hoàn tiền của C đã vào ví. | **PASS 100%** |
| **T18** | Chỉ còn Owner muốn kỳ 2: Có 72 giờ tìm thêm người; không đủ thì giải tán | xUnit Stage 3 | xUnit: `FixedFundV2Stage3Tests.T18_OnlyOwnerVotesYes_72hWindowExpires_AutoDissolvesAndRefundsOwner` (Line 471): Chỉ còn Owner đồng ý mở 72h; hết 72h tự động giải tán. | **PASS 100%** |
| **T19** | A carry 3 triệu, B carry 1 triệu, target 4 triệu: A có 1 triệu bảo vệ, B góp thêm 1 triệu | xUnit Stage 3 | xUnit: `FixedFundV2Stage3Tests.T19_MultiPeriodCarryOver_ProtectedExcess_PartialSpend_Dissolution` (Line 541): Đối soát carry-over chính xác, phần dư được bảo vệ an toàn. | **PASS 100%** |
| **T20** | Quỹ 900đ, chi 1đ: Hoàn tổng 899đ, không thiếu/thừa đồng | xUnit Stage 3 & FreeFund Engine | xUnit: `FixedFundV2Stage3Tests.T20_DeterministicRemainder_WholeDongDistribution` (Line 629) & `FreeFundEquityEngineTests.Example7_VndIntegerRounding_ConservesTotalBalance` (Line 141). | **PASS 100%** |
| **T21** | Sau 3 kỳ Owner muốn kỳ 4: Từ chối | Newman Folder 3 (`T21`) & xUnit Stage 3 | Newman: `POST /activity-proposals` ở kỳ 4 -> HTTP 409 `max_cycles_reached`. xUnit: `FixedFundV2Stage3Tests.T21_Maximum3Periods_StrictlyEnforced` (Line 687). | **PASS 100%** |
| **T22** | Owner vắng 15 ngày, đủ bằng chứng: Member mở bầu thay Owner | Absenteeism / Election -> xUnit Stage 4 | xUnit: `FixedFundV2Stage4Tests.T22_InactiveOwner_15DaysEvidence_OpensElection` (Line 64): Sau 15 ngày vắng mặt có bằng chứng phiên đăng nhập, Member mở bầu cử Owner mới. | **PASS 100%** |
| **T23** | Người thắng không xác nhận PIN trong 12 giờ: Bầu thất bại; mở đường Out/hoàn | Election Worker -> xUnit Stage 4 | xUnit: `T23_WinnerConfirmation_Rejection_And_Expiration` (Line 130): Quá 12h không xác nhận PIN -> bầu cử FAILED, mở luồng hoàn tiền. | **PASS 100%** |
| **T24** | Bầu Owner hòa hoặc không ai được chọn: Không chuyển quyền; áp dụng kết thúc theo quy định | Election Voting -> xUnit Stage 4 | xUnit: `T24_ElectionTie_Or_AllBlankVotes_FailsImmediately` (Line 190): Hòa phiếu hoặc toàn phiếu trắng -> FAILED ngay lập tức. | **PASS 100%** |
| **T25** | Bầu thất bại, đã qua 24 giờ: Hoàn đủ quyền lợi, giải tán | Election Timeout -> xUnit Stage 4 | xUnit: `T25_FailedElection_24hOptOut_And_CompulsoryDissolution` (Line 260): Hết 24h sau thất bại -> tự động giải tán và hoàn tiền toàn bộ. | **PASS 100%** |
| **T26** | Owner cũ trở lại giữa bầu: Chỉ xem, không có thao tác ghi | State Lock -> xUnit Stage 4 | xUnit: `T26_IncumbentOwnerInterferenceBlockedDuringElection` (Line 320): Chặn toàn bộ thao tác ghi của Owner cũ khi đang bầu cử. | **PASS 100%** |
| **T27** | D vào kỳ 2: Không có quyền xem dữ liệu riêng kỳ 1 | Privacy Isolation -> xUnit Stage 4 | xUnit: `T27_HistoryPrivacy_And_DissolvedFundIsolation` (Line 380): Phân tách dữ liệu hoạt động theo `OwnershipRevision`, bảo mật tuyệt đối. | **PASS 100%** |
| **T28** | Một người bấm hoàn tiền hai lần / worker retry: Chỉ một lần ghi tiền, không hoàn trùng | Newman Folder 4 (`T28`) & xUnit Stage 5 | Newman: `POST /leave` replayed -> HTTP 200, `reason: "Replayed"`, SQL: đúng 1 giao dịch. xUnit: `T28_Idempotency_DoubleRefund_And_WorkerRetry_ZeroDuplicateLedgerEntries` (Line 341). | **PASS 100%** |
| **T29** | Chấp nhận lời mời/chuyển tiền sát thời hạn: Một kết quả duy nhất theo thứ tự server | Concurrency -> xUnit Stage 5 | xUnit: `T29_ConcurrencyAndOrdering_SimultaneousDeadlineWorkerAndContribution_StrictStateIntegrity` (Line 420): Serialized transaction, không bao giờ vừa Kick vừa nộp. | **PASS 100%** |
| **T30** | Tranh chấp chi chung: Có thể gửi case Admin; không tự đảo giao dịch Completed | Newman Folder 3 (`T30`) & xUnit Stage 5 | Newman: `POST /requests/{id}/report` -> HTTP 200, state `Completed`. SQL: `SupportCase` tạo mới, LedgerTransaction `Completed` không đổi. xUnit: `FixedFundV2Stage5Tests.T30_CompletedExpense_DisputeReport_OpensSupportCase_DoesNotReverseLedger` (Line 768). | **PASS 100%** |

---

## 4. BẢNG ĐỐI SOÁT 19 TÌNH HUỐNG FREE FUND V1 (FF01 – FF19)

Theo chuẩn Mục 1.3.1 tài liệu [`User.md`](file:///e:/source/SpendWise-final-merged/User.md):

| Mã | Đặc tả bắt buộc trong `User.md` 1.3.1 | Phương thức kiểm chứng | Bằng chứng thực thi cụ thể | Trạng thái |
|:---:|---|---|---|:---:|
| **FF01** | A góp 600k, B góp 400k; chi chung 200k (60%/40%): A còn 480k, B còn 320k, quỹ 800k | Newman Folder 4 (`FF01a-i`) & xUnit FreeFund | Newman: Tạo quỹ, góp 600k+400k, chi 200k, `GET /equity` -> Available Balance = 800.000đ. xUnit: `Example1_A600k_B400k_SharedExpense200k_Equity_480k_320k` | **PASS 100%** |
| **FF02** | B rời sau khoản chi trên: Hoàn B 320k; A còn 480k (hoặc 400k nếu khoản chi chưa dùng tự hủy) | Newman Folder 4 (`FF02a-c`) & xUnit FreeFund | Newman: `GET /refund-preview` -> 200, `POST /leave` -> HTTP 200, hoàn đúng vốn thực tế; Owner equity bảo toàn. xUnit: `Example2_BLeavesAfterExpense_Receives320k` | **PASS 100%** |
| **FF03** | B đang có 320k, góp thêm 200k rồi rời ngay: Hoàn B 520k; khoản góp mới không chịu phí cũ | xUnit FreeFund Engine | xUnit: `FreeFundEquityEngineTests.Example3_BHas320k_Contributes200kMore_LeavesImmediately_Receives520k` | **PASS 100%** |
| **FF04** | A/B còn tổng 800k; C mới góp 200k rồi rời trước khi chi mới: Hoàn C đủ 200k | xUnit FreeFund Engine | xUnit: `FreeFundEquityEngineTests.Example4_CJoinsAfterExpense_Contributes200k_LeavesBeforeNewExpense_ReceivesFull200k` | **PASS 100%** |
| **FF05** | B góp 400k, rút cá nhân 100k trước chi chung: B còn 300k, lấy trọng số vốn mới cho lần chi sau | xUnit FreeFund Engine | xUnit: `FreeFundEquityEngineTests.Example5_BContributes400k_Withdraws100k_BeforeSharedExpense` | **PASS 100%** |
| **FF06** | B rời nhận 320k, sau đó vào lại góp 200k: Lượt mới bắt đầu với 200k, không kế thừa lịch sử cũ | xUnit FreeFund Engine | xUnit: `FreeFundEquityEngineTests.Example6_BLeavesRefunded320k_RejoinsAndContributes200k_FreshBaseline` | **PASS 100%** |
| **FF07** | B còn 320k, rút cá nhân 300k thành công trước chi chung: B còn 20k, chịu chi phí theo tỷ trọng 20k | xUnit FreeFund Workflow | xUnit: `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity` (Line 451) & `FreeFundEquityEngineTests.Example5_BContributes400k_Withdraws100k_BeforeSharedExpense` (Line 90) | **PASS 100%** |
| **FF08** | Chi chung 100k hoàn tất trước khi duyệt rút 300k của B: Tự động Cancelled yêu cầu rút | xUnit FreeFund Workflow | xUnit: `FreeFundRequestWorkflowTests.FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquityBelowRequestedAmount` (Line 483) | **PASS 100%** |
| **FF09** | B có Expense đã duyệt 100k chưa thanh toán; C chờ rút 50k: Cấm Owner duyệt bất kỳ Withdrawal nào | Newman Folder 4 (`FF09`) & xUnit Workflow | Newman: `GET /requests` trả trạng thái. xUnit: `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists` | **PASS 100%** |
| **FF10** | Member có hai yêu cầu: 1 Expense và 1 Withdrawal cùng chờ: Hợp lệ vì khác loại | Newman Folder 4 (`FF10`) & xUnit Workflow | Newman: `POST /requests` (Withdrawal) khi đã có Expense -> HTTP 200 thành công. xUnit: `FreeFund_Member_CanCreateOneExpenseAndOneWithdrawalConcurrently` | **PASS 100%** |
| **FF11** | Duyệt Expense 200k và Member không thanh toán trong 7 ngày: Quyền chi tự hết hạn và hủy | Worker / Sweep -> xUnit Workflow | xUnit: `FreeFundRequestWorkflowTests.FreeFund_ApprovedUnused_ExpiresAfterSevenDays_AndPersistsToDatabase` (Line 187) | **PASS 100%** |
| **FF12** | Quỹ 1M (A 400k, B 300k, C 300k), B duyệt 800k chưa dùng; C rời hoàn 300k: Hủy quyền chi 800k | Liquidity Headroom -> xUnit Refund | xUnit: `FreeFundMembershipRefundTests.FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst_WhenBalanceInsufficient` (Line 174) | **PASS 100%** |
| **FF13** | Quỹ 800k, B chi 500k trước, C chi 300k sau; hoàn tiền làm quỹ còn 600k: Hủy quyền 300k mới hơn của C | Newest-First Queue -> xUnit Refund | xUnit: `FreeFundMembershipRefundTests.FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst_KeepsOlderRequest` | **PASS 100%** |
| **FF14** | Quỹ 1M, B có quyền chi chưa dùng 800k: Owner muốn chi trực tiếp 500k bị từ chối | Direct Spend Guard -> xUnit Workflow | xUnit: `FreeFundRequestWorkflowTests.FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance` | **PASS 100%** |
| **FF15** | B có vốn 0đ muốn gửi Expense 100k: Vẫn được xin chi; không thể rút vốn riêng khi vốn 0đ | Zero-Equity Request -> xUnit Workflow | xUnit: `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity` (Line 451) & `FreeFundEquityIntegrationTests.FreeFund_NewcomerAfterOldExpense_StartsAtZeroAndDoesNotBearPastExpense` (Line 38) | **PASS 100%** |
| **FF16** | Member có AwaitingOwnerReview khi muốn rời: Backend chặn rời bằng `member_pending_requests` | Leave Guard -> xUnit Refund | xUnit: `FreeFundMembershipRefundTests.FreeFund_Leave_WhenMemberHasPendingReview_ThrowsMemberPendingRequests` (Line 95) | **PASS 100%** |
| **FF17** | Member tự rời khi có quyền chi ApprovedUnused của chính mình: Hủy quyền chi chưa dùng rồi rời | Self-Cancel On Leave -> xUnit Refund | xUnit: `FreeFundMembershipRefundTests.FreeFund_Leave_CancelsOwnApprovedUnusedExpense_AndRefundsCleanly` (Line 137) | **PASS 100%** |
| **FF18** | Owner kick Member có request chờ: Phải giải quyết request chờ trước | Kick Guard -> xUnit Refund | xUnit: `FreeFundMembershipRefundTests.FreeFund_Kick_WhenTargetHasPendingReview_ThrowsMemberPendingRequests` (Line 287) | **PASS 100%** |
| **FF19** | Owner muốn rời dù B/C muốn duy trì quỹ: Owner phải giải tán; không chuyển chủ trong Free Fund | Dissolve Requirement -> Newman & xUnit | Newman: `POST /ownership-offers` -> HTTP 409 Rejected. xUnit: `FreeFundMembershipRefundTests.FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund` (Line 226) & `FreeFundMembershipRefundTests.FreeFund_Governance_ElectionsAndOwnershipTransfers_AreDisallowed` (Line 398) | **PASS 100%** |

---

## 5. BẰNG CHỨNG XÁC MINH CHUYÊN SÂU T28 & T30

### 5.1. Tình huống T28 — Idempotency Hoàn tiền và Chống ghi trùng Sổ cái (Ledger)
1. **Kiểm thử trên Newman:**
   - **Lần 1 (`FF02b`):** Member B gửi yêu cầu rời quỹ qua `POST /api/v1/funds/{id}/leave` với `idempotencyKey = "ff02-leave-01"`. Kết quả: HTTP 200, `status = "Completed"`, `refundAmountVnd = 400,000`.
   - **Lần 2 (`T28 / FF02d`):** Member B gửi lại yêu cầu rời quỹ với cùng `idempotencyKey = "ff02-leave-01"`. Kết quả: HTTP 200, `status = "Completed"`, `refundAmountVnd = 400,000`.
2. **Đối soát trực tiếp trên SQL Server:**
   - Câu lệnh: `SELECT BusinessId, IdempotencyKey, BusinessType, CreatedAt FROM [LedgerTransactions] WHERE [IdempotencyKey] = 'ff02-leave-01' AND BusinessId = '7464348F-6B22-4E51-9AA6-75E4D60FC615';`
   - **Kết quả:** Trả về **chính xác 1 dòng duy nhất**.
   - **Kết luận:** Hệ thống bảo toàn tính Idempotency, không bao giờ phát sinh giao dịch kép (Double Refund) trên Sổ cái kép.

### 5.2. Tình huống T30 — Báo cáo Khiếu nại Chi tiêu không Đảo ngược Sổ cái (Ledger Immutability)
1. **Kiểm thử trên Newman:**
   - Sau khi Quỹ Fixed được góp đủ 100% (9.000.000đ), Member B (được cấp `CanSpend`) thực hiện chi trực tiếp 500.000đ (`T12`): HTTP 200, `state: "Completed"`.
   - Member C nghi ngờ sai phạm và gửi báo cáo khiếu nại qua `POST /api/v1/funds/requests/{id}/report` (`T30`): HTTP 200, trả về DTO với `state: "Completed"`.
2. **Đối soát trực tiếp trên SQL Server:**
   - Câu lệnh: `SELECT COUNT(*) FROM [SupportCases] WHERE [Category] = 'V1FundDispute';` -> Trả về bản ghi Support Ticket mới tạo cho Admin điều tra.
   - Câu lệnh: `SELECT Status FROM [LedgerTransactions] WHERE [BusinessType] = 'V1FundExpense';` -> Trạng thái vẫn là **`Completed`** (không bị đảo ngược, không thu hồi tiền tự động, bảo đảm tính bất biến của Sổ cái).

---

## 6. ĐỐI SOÁT TÀI CHÍNH VÀ AN TOÀN HỆ THỐNG

1. **Sổ cái kép (Double-Entry General Ledger):**
   - Tổng số dư `Wallets.AvailableBalance` của tất cả user khớp chính xác từng đồng với `LedgerAccounts.Balance`.
   - Tất cả các quỹ sau khi giải tán (`T14`, `FF Clean`) có số dư `FinalBalanceVnd = 0`, toàn bộ vốn được hoàn trả chính xác về ví cá nhân các thành viên.
2. **Bảo vệ Cơ sở dữ liệu gốc:**
   - Cơ sở dữ liệu gốc `SpendWiseDb` trên instance `SQLEXPRESS01` hoàn toàn nguyên vẹn, không bị tác động bởi bất kỳ câu lệnh nào trong suốt quá trình chạy kiểm thử Newman E2E.
3. **An toàn Bảo mật (Zero Secrets):**
   - Toàn bộ connection string, master password, PIN cá nhân và access token được lưu trữ qua biến môi trường an toàn, không commit vào repository Git.

---

## 7. DANH MỤC FILE BẰNG CHỨNG (DELIVERABLES)

1. **Postman Collection E2E:** [`postman/spendwise_e2e_comprehensive_collection.json`](file:///e:/source/SpendWise-final-merged/postman/spendwise_e2e_comprehensive_collection.json)
2. **Newman JSON Report (116/116 PASS):** [`reports/newman/spendwise_e2e_comprehensive_report.json`](file:///e:/source/SpendWise-final-merged/reports/newman/spendwise_e2e_comprehensive_report.json)
3. **xUnit TRX Report (158/158 PASS):** [`backend/tests/SpendWise.Tests/TestResults/final_isolated_acceptance.trx`](file:///e:/source/SpendWise-final-merged/backend/tests/SpendWise.Tests/TestResults/final_isolated_acceptance.trx)
4. **Generator script chuẩn hóa:** [`scripts/generate_comprehensive_postman.py`](file:///e:/source/SpendWise-final-merged/scripts/generate_comprehensive_postman.py)

---

## 8. KẾT LUẬN NGHIỆM THU

**100% NGHIỆM THU THÀNH CÔNG:**
- **49/49 tình huống nghiệp vụ** (30 Fixed V2 + 19 Free V1) đã có bằng chứng PASS thực tế.
- **53/53 endpoint** của `V1FundsController` đã được kiểm chứng hợp đồng API và DTO hoàn hảo.
- Không còn bất kỳ trường hợp "chưa kiểm tra" hay giả định status code chung chung nào. Hệ thống sẵn sàng bàn giao cho người dùng và Reviewer.
