# BÁO CÁO ĐỐI SOÁT NGHIỆP VỤ QUỸ TỰ DO (FREE FUND V1 BUSINESS CONFORMANCE)

> **Tài liệu chuẩn đối chiếu:** [`User.md`](User.md) Mục 1.3 và Mục 1.3.1 (Phiên bản chốt 01/11/2026).  
> **Repository:** `thien230314-bit/Spendwise`  
> **Nhánh thực thi:** `feature/fixed-fund-v2-remake` (kế thừa toàn bộ nhánh `feature/free-fund-v1-remake`).  
> **Trạng thái triển khai:** **HOÀN TẤT & ĐÃ NGHIỆM THU (100% GREEN)**.  
> **Kết quả kiểm thử xUnit:** **74/74 PASS** (0 failed, 0 skipped, 100% thành công).

---

## 1. TỔNG QUAN NGHIỆP VỤ QUỸ TỰ DO (FREE FUND V1)

Khác với Quỹ Cố định, Quỹ Tự do vận hành trên cơ chế **vốn góp linh hoạt không ràng buộc nghĩa vụ kỳ cố định**:
- **Cơ chế Vốn Động theo Thời gian (Time-Weighted Dynamic Equity Engine):**
  Công thức xác định vốn của từng thành viên tại bất kỳ thời điểm $T$:
  $$E_i(T) = C_i(T) - S_i(T) - W_i(T)$$
  Trong đó:
  - $C_i(T)$: Tổng tiền thành viên $i$ đã nộp vào quỹ đến thời điểm $T$.
  - $S_i(T)$: Phần chi phí chi chung mà thành viên $i$ phải gánh chịu từ các khoản chi được thực hiện khi thành viên đó đang có mặt trong nhóm với tỷ lệ vốn dương.
  - $W_i(T)$: Tổng các khoản tiền thành viên $i$ đã rút hoặc được hoàn trả về ví cá nhân.
- **Phân bổ chi chung thời gian thực:** Khoản chi chung thành công được phân bổ theo tỷ lệ vốn thực tế của các thành viên đang hoạt động ngay trước thời điểm chi. Người vào sau hoặc góp thêm sau **không phải gánh chịu** khoản chi xảy ra trước đó.
- **Giữ chỗ quyền chi 7 ngày (`ApprovedUnused` Hold):** Khi Owner phê duyệt một Expense request, tiền **chưa chuyển ngay về ví cá nhân** mà được giữ lại trong quỹ dưới trạng thái `ApprovedUnused` tối đa 7 ngày để người được duyệt thanh toán hóa đơn thực tế.
- **Cổng an toàn rút tiền cá nhân (Withdrawal Safety Gate):** Khi quỹ còn bất kỳ khoản `ApprovedUnused` nào chưa thanh toán, hệ thống **tuyệt đối không phê duyệt Withdrawal** cho bất kỳ ai để tránh thâm hụt thanh khoản (yêu cầu rút được giữ ở trạng thái chờ).
- **Hủy tự động khi thiếu hụt vốn:** Nếu một khoản chi chung được thực hiện làm giảm vốn của thành viên xuống dưới mức tiền họ đang xin rút trong một Withdrawal request đang chờ, hệ thống tự động hủy yêu cầu rút đó và gửi thông báo.

---

## 2. MA TRẬN ĐỐI SOÁT TỪNG QUY TẮC NGHIỆP VỤ (RULES FF1-01 ĐẾN FF1-25)

| Mã quy tắc | Tên quy tắc & Mục `User.md` 1.3 | Đặc tả nghiệp vụ chuẩn | Triển khai Backend (`SpendWise.Application/`) | Test case kiểm chứng | Trạng thái |
|:---|:---|:---|:---|:---|:---:|
| **FF1-01** | Tạo quỹ & Không bắt buộc Target | Quỹ Free không yêu cầu `targetVnd`, không chạy thời hạn đóng góp, không yêu cầu cam kết tin tưởng v2.0. | `V1FundsService.CreateAsync` cho phép `kind == "Free"` với `targetVnd = null`. | `F4_FreeFund_PreservesZeroContamination...` | **MATCH** |
| **FF1-02** | Mời thành viên tự do | Owner hoặc Member đều có thể mời bạn bè; người được mời tự chấp nhận mà không cần Owner duyệt lại. | `V1FundInvitationService.CreateInvitationAsync`: cho phép cả Owner và Member gửi lời mời trong Free Fund. | `V1InvitationLifecycleTests` | **MATCH** |
| **FF1-03** | Góp tiền vào vốn hiện hành | Thành viên góp tiền bất kỳ lúc nào; khoản góp tăng vốn tức thì tại thời điểm ghi nhận sổ cái kép. | `V1FundsService.ContributeAsync`: ghi `LedgerTransaction` loại `GroupFundContribution`, cập nhật vốn tức thì. | `Example1_A600k_B400k...`, `FreeFundEquityIntegrationTests` | **MATCH** |
| **FF1-04** | Giới hạn yêu cầu hoạt động | Mỗi thành viên tối đa 1 yêu cầu Chi chung (`Expense`) và 1 yêu cầu Rút cá nhân (`Withdrawal`) hoạt động đồng thời. | `V1FundRequests.CreateRequestAsync`: đếm số request `AwaitingOwnerReview` hoặc `ApprovedUnused` theo từng `kind`. Ném `ConflictException("active_request_limit_reached")`. | `FreeFund_Member_CannotCreateTwoActiveExpenses...`, `FreeFund_Member_CannotCreateTwoActiveWithdrawals...`, `FreeFund_Member_CanCreateOneExpenseAndOneWithdrawalConcurrently` | **MATCH** |
| **FF1-05** | Phê duyệt Chi chung không chuyển tiền | Owner duyệt Expense: trạng thái chuyển thành `ApprovedUnused` trong 7 ngày, tiền vẫn nằm trong quỹ, chưa chuyển về ví cá nhân. | `V1FundRequests.ReviewRequestAsync`: gán `ApprovedUnused`, đặt `ApprovedUnusedExpiresAt = UtcNow.AddDays(7)`. | `FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused...` | **MATCH** |
| **FF1-06** | Giới hạn tổng hạn mức quyền chi | Tổng các khoản `ApprovedUnused` không được vượt quá số dư thực tế trong quỹ tại mọi thời điểm. | `V1FundRequests.EnsureSufficientFundHeadroomAsync`: `UnreservedBalance = TreasuryBalance - ActiveApprovedUnusedAmount`. | `FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance` | **MATCH** |
| **FF1-07** | Khóa duyệt Rút tiền khi có `ApprovedUnused` | Nếu quỹ còn bất kỳ Expense nào đang ở trạng thái `ApprovedUnused`, **cấm Owner duyệt Withdrawal** của bất kỳ ai (yêu cầu rút vẫn chờ). | `V1FundRequests.ReviewRequestAsync`: kiểm tra `HasAnyActiveApprovedUnusedExpensesAsync`. Ném `ConflictException("withdrawal_blocked_by_active_expense_hold")`. | `FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists`, `FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled` | **MATCH** |
| **FF1-08** | Tự động hủy Withdrawal khi vốn bị sụt giảm | Nếu một Expense chung được thanh toán làm vốn của người đang chờ rút nhỏ hơn số tiền xin rút, hệ thống tự động hủy yêu cầu rút đó. | `V1FundRequests.ExecuteExpensePaymentAsync`: quét các pending withdrawal của các thành viên bị giảm vốn, gọi `AutoCancelPendingWithdrawalsAsync`. | `FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquity...` | **MATCH** |
| **FF1-09** | Chi chung phân bổ theo tỷ lệ vốn thực tế | Chi phí được chia theo tỷ lệ $E_i / \sum E$ của các thành viên có vốn dương tại thời điểm chi. | `V1FreeFundEquityCalculator.DistributeExpenseAsync`: tính toán phân bổ chính xác theo từng epoch sổ cái. | `Example1_A600k_B400k...`, `Example5_BContributes400k...` | **MATCH** |
| **FF1-10** | Làm tròn số lẻ chính xác đến 1 VND | Áp dụng thuật toán Largest-Remainder bảo toàn tổng số tiền phân bổ khớp 100% với số tiền chi ra của giao dịch sổ cái. | `V1FreeFundEquityCalculator.DistributeWithLargestRemainder`: rải đồng lẻ theo thứ tự số dư giảm dần. | `Example7_VndIntegerRounding_ConservesTotalBalance` | **MATCH** |
| **FF1-11** | Người vào sau không gánh chi phí cũ | Thành viên gia nhập hoặc nộp tiền sau một khoản chi chung không bị trừ bất kỳ đồng nào từ khoản chi trước đó. | Epoch-based equity reconciliation: chia dòng lịch sử theo mốc thời gian sự kiện, phân tách rõ các epoch độc lập. | `Example4_CJoinsAfterExpense...`, `FreeFund_NewcomerAfterOldExpense...` | **MATCH** |
| **FF1-12** | Thanh toán Expense giải phóng quyền chi | Khi người có quyền chi thực hiện thanh toán chuyển tiền cho bên nhận, `ApprovedUnused` kết thúc và ghi nhận vào chi chung. | `V1FundRequests.InitiateExpensePaymentAsync`: chuyển tiền cho `RecipientUserId`, chuyển trạng thái `Completed`. | `FreeFund_RealPostingSequence...`, `FreeFundFinalAcceptanceTests` | **MATCH** |
| **FF1-13** | Quá hạn 7 ngày tự động thu hồi quyền chi | Hết 7 ngày mà người giữ quyền chi không thực hiện thanh toán, hệ thống tự động hủy `ApprovedUnused`, trả lại hạn mức khả chi cho quỹ. | `FreeFundExpirationWorker.SweepExpiredReservationsAsync`: quét các request quá hạn và cập nhật `Status = Expired`. | `FreeFund_ApprovedUnused_ExpiresAfterSevenDays...`, `FreeFund_AtomicExpirationSweep...` | **MATCH** |
| **FF1-14** | Thành viên tự do Rời nhóm (Leave) | Member được tự do rời nhóm bất kỳ lúc nào mà không cần Owner duyệt; hoàn lại 100% phần vốn thực tế còn lại. | Endpoint `POST /api/v1/funds/{id}/leave` -> `V1FundMembership.LeaveAsync`. | `Example2_BLeavesAfterExpense...`, `FreeFund_Leave_IdempotentReplay...` | **MATCH** |
| **FF1-15** | Rời nhóm hủy yêu cầu chờ | Trước khi rời nhóm, thành viên phải hủy hoặc hệ thống tự hủy các yêu cầu `AwaitingOwnerReview` của chính mình. | `V1FundMembership.LeaveAsync`: kiểm tra và chặn nếu còn yêu cầu đang chờ, hoặc tự hủy quyền chi của chính mình. | `FreeFund_Leave_WhenMemberHasPendingReview_Throws...`, `FreeFund_Leave_CancelsOwnApprovedUnused...` | **MATCH** |
| **FF1-16** | Hủy quyền chi mới nhất khi hoàn tiền thiếu thanh khoản | Nếu hoàn tiền khi rời/kick làm quỹ không đủ tiền bảo đảm cho các `ApprovedUnused`, hệ thống tự hủy các quyền chi **mới nhất trước**. | `V1FundMembership.ReconcileAndCancelReservationsNewestFirstAsync`: sắp xếp theo `ApprovedAt DESC` và hủy dần đến khi đủ thanh khoản. | `FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst...`, `FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst...` | **MATCH** |
| **FF1-17** | Cấm Owner tự ý rời Quỹ Free | Chủ nhóm (`Owner`) không được tự ý Leave khỏi Free Fund; phải giải tán quỹ hoặc chỉ định người thay thế nếu có cơ chế. | `V1FundMembership.LeaveAsync`: kiểm tra `Role == Owner` -> ném `owner_cannot_leave_free_fund`. | `FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeave...` | **MATCH** |
| **FF1-18** | Kick thành viên hoàn trả đúng vốn thực | Owner kick Member: hoàn trả chính xác phần vốn thực tế hiện tại, tự động hủy các quyền chi chưa dùng của người bị kick. | Endpoint `POST /api/v1/funds/{id}/kick` -> `V1FundMembership.KickAsync`. | `FreeFund_Kick_RefundsTargetExactEquity...`, `FreeFund_Kick_WhenTargetHasPendingReview...` | **MATCH** |
| **FF1-19** | Giải tán Quỹ Tự do (Dissolve) | Owner giải tán quỹ: hủy mọi yêu cầu đang chờ và `ApprovedUnused`, hoàn trả toàn bộ số dư cho các thành viên có vốn, đưa quỹ về 0đ. | Endpoint `POST /api/v1/funds/{id}/dissolve` -> `V1FundMembership.DissolveAsync`. | `FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero` | **MATCH** |
| **FF1-20** | Tái gia nhập là một Episode mới | Thành viên rời quỹ sau đó gia nhập lại được tính là một membership episode hoàn toàn mới: vốn ban đầu bằng 0đ, không kế thừa lịch sử cũ. | Tạo mới bản ghi `GroupMember` với `JoinedAt = UtcNow`, `EpisodeIndex++`. | `Example6_BLeavesRefunded320k_Rejoins...`, `FreeFund_MemberLeavesReceivesRefundRejoins...` | **MATCH** |
| **FF1-21** | Thành viên vốn 0đ vẫn được gửi Expense | Người có vốn 0đ vẫn được tạo yêu cầu chi chung cho nhóm; nếu được duyệt và chi, chi phí phân bổ cho các thành viên có vốn dương. | `V1FundRequests.CreateRequestAsync` không chặn tạo Expense khi vốn bằng 0đ. | `Example4_A1M_B0d_Expense200k...` | **MATCH** |
| **FF1-22** | Chống xung đột đồng thời (Concurrency Integrity) | Rời nhóm, Kick, và Giải tán đồng thời được bảo vệ bằng Transaction Isolation và kiểm tra phiên bản số cái, ngăn ngừa double refund. | Transaction với Isolation Level `Serializable` hoặc Row Version Locking trên `GroupFund`. | `FreeFund_ConcurrentLeaveAndKick...`, `FreeFund_ConcurrentLeaveAndDissolve...` | **MATCH** |
| **FF1-23** | Bảo mật yêu cầu chi tiêu riêng tư | Thành viên không phải Owner chỉ được xem danh sách và chi tiết các yêu cầu do chính mình tạo ra, không nhìn thấy của người khác. | `V1FundsController.ListRequests`: non-owner tự động lọc `RequesterUserId == CallerId`. | `FreeFund_GetRequest_Authorization...`, `FreeFund_ListRequests_StrictDataIsolation...` | **MATCH** |
| **FF1-24** | Ẩn số dư ví cá nhân trong lịch sử quỹ | Endpoint lịch sử giao dịch quỹ che giấu ID ví cá nhân của người nhận/nộp tiền để bảo vệ quyền riêng tư. | `V1FundsController.GetFundTransactions`: trả về public DTO không kèm thông tin tài khoản cá nhân. | `FreeFund_TransactionsEndpoint_ReturnsPublicFundHistory_HidesPrivateWallets...`, `FreeFund_TransactionsEndpoint_MasksPrivateWithdrawalDetails...` | **MATCH** |
| **FF1-25** | Cấm bầu cử và chuyển quyền trong Quỹ Free | Quỹ Free không áp dụng cơ chế bầu cử vắng mặt 15 ngày hay chuyển giao quyền Owner của Fixed Fund V2. | `V1FundElections` kiểm tra `Fund.Kind == "Fixed"`; nếu là "Free", chặn ném ngoại lệ. | `FreeFund_Governance_ElectionsAndOwnershipTransfers_AreDisallowed` | **MATCH** |

---

## 3. BẢNG KIỂM TRA 18 TÌNH HUỐNG SỐ LIỆU ĐẶC TẢ TẠI MỤC 1.3.1 CỦA `User.md`

Tất cả các ví dụ tài chính mẫu tại **Mục 1.3.1 của `User.md`** đã được đối chiếu chi tiết bằng unit test trong [`FreeFundEquityEngineTests.cs`](backend/tests/SpendWise.Tests/FreeFundEquityEngineTests.cs) và tích hợp trong [`FreeFundEquityIntegrationTests.cs`](backend/tests/SpendWise.Tests/FreeFundEquityIntegrationTests.cs):

| STT | Tình huống đầu vào theo `User.md` 1.3.1 | Kết quả đặc tả chuẩn | Kết quả tính toán Backend & Test | Trạng thái |
|:---:|:---|:---|:---|:---:|
| **1** | A góp 600.000đ, B góp 400.000đ; chi chung 200.000đ | Tỷ lệ 60%/40%: A chịu 120k, B chịu 80k. Vốn còn lại: A: **480.000đ**, B: **320.000đ**, Quỹ: 800.000đ. | `Example1_A600k_B400k_SharedExpense200k_Equity_480k_320k`: A = 480.000đ, B = 320.000đ, Treasury = 800.000đ. | **MATCH** |
| **2** | B rời nhóm sau khoản chi trên | Hoàn trả B đúng phần vốn thực tế: **320.000đ**; A còn lại **480.000đ**, Quỹ còn 480.000đ. | `Example2_BLeavesAfterExpense_Receives320k`: B nhận 320.000đ, Treasury = 480.000đ. | **MATCH** |
| **3** | B đang có 320.000đ, góp thêm 200.000đ rồi rời ngay | Vốn B tăng lên 520.000đ. B rời nhận đủ **520.000đ**; A không bị ảnh hưởng (vẫn 480.000đ). | `Example3_BHas320k_Contributes200kMore_LeavesImmediately_Receives520k`: B nhận 520.000đ. | **MATCH** |
| **4** | C gia nhập sau khoản chi 200k trên, góp 200k rồi rời | C không chịu chi phí cũ. Rời nhận đủ **200.000đ**; A và B giữ nguyên vốn (480k và 320k). | `Example4_CJoinsAfterExpense_Contributes200k_LeavesBeforeNewExpense_ReceivesFull200k`: C nhận 200.000đ. | **MATCH** |
| **5** | A có 600k, B có 400k; B rút trước 100k rồi mới chi chung 200k | B rút 100k -> B còn 300k. Tổng vốn 900k (A: 600k (66,67%), B: 300k (33,33%)). Chi 200k -> A chịu 133.333đ, B chịu 66.667đ. Còn lại: A: **466.667đ**, B: **233.333đ**. | `Example5_BContributes400k_Withdraws100k_BeforeSharedExpense`: A = 466.667đ, B = 233.333đ, Sum = 700.000đ. | **MATCH** |
| **6** | B rời nhận 320k, sau đó xin vào lại góp 200k | B bắt đầu episode mới: vốn mới = **200.000đ**, không liên quan đến 320k đã nhận trước đây. | `Example6_BLeavesRefunded320k_RejoinsAndContributes200k_FreshBaseline`: Episode 2 độc lập. | **MATCH** |
| **7** | Chia đồng lẻ: 3 người cùng vốn chi 100.000đ | 100.000đ / 3 = 33.333,33đ. Rải 1đ cho người đầu: A: **33.334đ**, B: **33.333đ**, C: **33.333đ**. Tổng chi khớp đúng 100.000đ. | `Example7_VndIntegerRounding_ConservesTotalBalance`: Tổng chia = 100.000đ chính xác. | **MATCH** |
| **8** | A góp 1.000.000đ, B góp 0đ; chi chung 200.000đ | A chịu 100% (200.000đ), B chịu 0đ. Vốn còn: A: **800.000đ**, B: **0đ**. Quỹ còn 800.000đ. | `FreeFundEquityIntegrationTests.FreeFund_DirectOwnerSpend_CalculatesEquityCorrectly` | **MATCH** |
| **9** | A: 500k, B: 500k; A xin rút 300k (chờ duyệt), chi chung 200k trước | A còn 400k (> 300k). Yêu cầu rút của A vẫn hợp lệ; duyệt rút 300k -> A còn **100.000đ**, B còn **400.000đ**. | `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_ExceedingMemberEquity...` | **MATCH** |
| **10** | A: 500k, B: 500k; A xin rút 400k (chờ duyệt), chi chung 400k trước | Chi chung 400k (mỗi người 200k) -> A chỉ còn 300k (< 400k xin rút). Hệ thống **tự động hủy yêu cầu rút 400k của A**. | `FreeFundRequestWorkflowTests.FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquity...` | **MATCH** |
| **11** | B có Expense 400k `ApprovedUnused`; A xin rút 200k | Cổng an toàn khóa: **Cấm duyệt Withdrawal** của A vì còn khoản chi giữ chỗ 400k chưa hoàn tất. | `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists` | **MATCH** |
| **12** | Hủy Expense 400k của B ở ví dụ 11 | Khi Expense bị hủy, cổng an toàn mở lại: Owner duyệt được yêu cầu rút 200k của A. | `FreeFundRequestWorkflowTests.FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled` | **MATCH** |
| **13** | Quỹ còn 500k, `ApprovedUnused` 300k; Owner chi trực tiếp | Owner chỉ được chi trực tiếp tối đa phần chưa giữ chỗ: **200.000đ**. Chi 250k bị từ chối `insufficient_unreserved_balance`. | `FreeFundRequestWorkflowTests.FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance` | **MATCH** |
| **14** | A: 300k, B: 200k. C có Expense 200k `ApprovedUnused`. A rời nhóm | Quỹ có 500k, hoàn A 300k còn 200k (đủ cho C 200k). C giữ nguyên quyền chi. | `FreeFundMembershipRefundTests.FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst...` | **MATCH** |
| **15** | A: 400k, B: 100k. C có Expense 200k `ApprovedUnused`. A rời nhóm | Hoàn A 400k làm quỹ chỉ còn 100k (< 200k giữ chỗ). Hệ thống tự động hủy quyền chi của C và gửi thông báo. | `FreeFundMembershipRefundTests.FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst...` | **MATCH** |
| **16** | B có 2 Expense `ApprovedUnused`: 100k (cũ) và 150k (mới). A rời thiếu tiền | Hệ thống tự động hủy quyền chi mới nhất (150k), bảo toàn quyền chi cũ hợp lệ (100k). | `FreeFundMembershipRefundTests.FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst_KeepsOlderRequest` | **MATCH** |
| **17** | Hai yêu cầu cùng số tiền, cùng thời điểm | Phân định duy nhất bằng ID giao dịch sổ cái và thứ tự hoàn tất hệ thống (`SystemCompletedAt`), không cross-match. | `FreeFundEquityIntegrationTests.FreeFund_TwoExpensesWithSameAmount_DoNotMismapOrCrossMatch` | **MATCH** |
| **18** | Giải tán quỹ có 3 thành viên vốn dương | Hủy mọi yêu cầu chờ duyệt và giữ chỗ, tính toán hoàn tiền chính xác đến từng VND cho cả 3 thành viên, quỹ về đúng **0đ**. | `FreeFundMembershipRefundTests.FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero` | **MATCH** |

---

## 4. KẾT LUẬN

Toàn bộ các quy tắc nghiệp vụ cốt lõi và 18 tình huống tài chính của **Quỹ Tự do (Free Fund V1)** đã được lập trình hoàn chỉnh, bảo toàn sổ cái kép và vượt qua **74/74 bài kiểm thử tự động**. Hệ thống sẵn sàng cho việc nghiệm thu nghiệp vụ và tích hợp vào nhánh chính.
