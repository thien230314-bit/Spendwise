# SPENDWISE FREE FUND V1 — MOBILE API INTEGRATION & HANDOFF SPECIFICATION

Tài liệu bàn giao API và nghiệp vụ Free Fund V1 cho Mobile UI, tuân thủ tuyệt đối quy định nghiệp vụ tại `User.md` (chốt ngày 01/11/2026) và đã được kiểm chứng bằng automated integration tests.

---

## 1. TỔNG QUAN VÀ NGUYÊN TẮC CỐT LÕI

1. **Không có nghĩa vụ đóng góp cố định (No Fixed Target/Obligations)**:
   - Thành viên nạp tùy ý theo nhu cầu thực tế (`POST /api/v1/funds/{id}/contributions`).
   - Không có chu kỳ hoạt động cố định, không bầu cử (`free_fund_election_disallowed`), không chuyển nhượng nhóm (`free_fund_ownership_transfer_disallowed`).
2. **Quyền chi tiêu (Reservation / ApprovedUnused)**:
   - Chi tiêu nhóm cần được Owner duyệt. Sau khi duyệt, yêu cầu chuyển sang trạng thái `ApprovedUnused` trong **7 ngày**.
   - Tiền vẫn nằm trong kho quỹ (Treasury), không chuyển về ví cá nhân. Số tiền được tạm giữ trong `ActiveReservedVnd` để đảm bảo khi chi tiêu thực tế quỹ luôn đủ tiền.
   - Sau 7 ngày nếu không sử dụng, yêu cầu tự động hết hạn (`FundRequestExpired`) và giải phóng hạn mức.
3. **Bảo mật và phân quyền riêng tư**:
   - Thành viên thường chỉ thấy danh sách yêu cầu do chính mình tạo ra (`GET /api/v1/funds/{id}/requests`).
   - Chỉ Trưởng nhóm (Owner) và người tạo yêu cầu (Requester) mới có quyền truy cập chi tiết yêu cầu (`GET /api/v1/funds/requests/{id}`). Thành viên khác truy cập sẽ nhận `403 Forbidden`.
   - Lịch sử giao dịch chung của quỹ (`GET /api/v1/funds/{id}/transactions`) hiển thị minh bạch cho tất cả thành viên trong nhóm (gồm nạp tiền, hoàn tiền, chi tiêu, rút vốn) nhưng **không làm lộ ID ví cá nhân hoặc token nhạy cảm**.
   - Các thông báo (`Notification`) và dòng hoạt động (`Activity`) của yêu cầu riêng tư (như rút vốn, chi tiêu) được định tuyến nghiêm ngặt giữa Người yêu cầu và Trưởng quỹ; thành viên khác không nhận thông báo và không đọc được trong activity feed.
4. **Bảo toàn hoàn tiền khi rời quỹ (Leave / Kick / Dissolve)**:
   - Thành viên nhận lại đúng số vốn ròng (Equity) của lượt tham gia hiện tại.
   - Khi hoàn tiền làm giảm số dư kho quỹ dưới mức tạm giữ các quyền chi `ApprovedUnused`, hệ thống tự động hủy các quyền chi theo thứ tự **mới nhất trước** (`ReviewedAt DESC, CreatedAt DESC, Id DESC`) để bảo toàn quyền chi cũ hơn.
   - Giải tán quỹ (`Dissolve`) đưa số dư kho quỹ về chính xác **0 VND**, toàn bộ tiền hoàn khớp 100% tổng số dư.

---

## 2. DANH MỤC API ENDPOINTS

Base URL: `/api/v1/funds`
Xác thực: Bearer JWT Token (`Authorization: Bearer <token>`)

### 2.1. Quản lý Tài chính & Vốn ròng (Equity)

#### `GET /api/v1/funds/{id}/equity`
Lấy tổng quan tài chính, vốn ròng của quỹ và của người dùng hiện tại.
- **Quyền**: Thành viên hoặc Trưởng nhóm.
- **Response `200 OK`**:
```json
{
  "fundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fundName": "Quỹ Đi Phượt",
  "fundKind": "Free",
  "totalBalanceVnd": 1000000,
  "activeReservedVnd": 200000,
  "availableBalanceVnd": 800000,
  "currentUserEquity": {
    "userId": "b4c238b1-36aa-4e9e-9988-123456789abc",
    "fullName": "Nguyễn Văn B",
    "role": "Member",
    "contributedVnd": 400000,
    "paidExpensesVnd": 0,
    "netEquityVnd": 400000,
    "eligibleRefundVnd": 400000
  },
  "memberEquities": [
    {
      "userId": "a1b2c3d4-e5f6-7a8b-9c0d-112233445566",
      "fullName": "Trần Văn A",
      "role": "Owner",
      "contributedVnd": 600000,
      "paidExpensesVnd": 0,
      "netEquityVnd": 600000,
      "eligibleRefundVnd": 600000
    },
    {
      "userId": "b4c238b1-36aa-4e9e-9988-123456789abc",
      "fullName": "Nguyễn Văn B",
      "role": "Member",
      "contributedVnd": 400000,
      "paidExpensesVnd": 0,
      "netEquityVnd": 400000,
      "eligibleRefundVnd": 400000
    }
  ]
}
```

---

### 2.2. Lịch sử Giao dịch Chung của Quỹ

#### `GET /api/v1/funds/{id}/transactions?page=1&pageSize=20`
Lịch sử đầy đủ dòng tiền kho quỹ (Nạp tiền, Chi tiêu, Rút vốn, Hoàn tiền).
- **Quyền**: Thành viên hoặc Trưởng nhóm.
- **Response `200 OK`**:
```json
[
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "fundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "Expense",
    "amountVnd": 200000,
    "description": "Thanh toán ăn trưa nhóm",
    "occurredAt": "2026-11-01T10:15:00Z",
    "actorName": "Nguyễn Văn B",
    "relatedRequestId": "550e8400-e29b-41d4-a716-446655440000"
  },
  {
    "id": "2d3e4f5a-6b7c-8d9e-0f1a-223344556677",
    "fundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "Withdrawal",
    "amountVnd": 100000,
    "description": "Rút vốn cá nhân",
    "occurredAt": "2026-11-01T09:45:00Z",
    "actorName": "Nguyễn Văn B",
    "relatedRequestId": "660e8400-e29b-41d4-a716-446655440001"
  },
  {
    "id": "1b2c3d4e-5f6a-7b8c-9d0e-112233445566",
    "fundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "Transfer",
    "amountVnd": 400000,
    "description": "Nạp tiền vào quỹ",
    "occurredAt": "2026-11-01T09:30:00Z",
    "actorName": "Nguyễn Văn B",
    "relatedRequestId": null
  }
]
```

---

### 2.3. Quản lý Yêu cầu (Expense & Withdrawal)

#### `POST /api/v1/funds/{id}/requests`
Tạo yêu cầu Chi tiêu (`Expense`) hoặc Rút tiền (`Withdrawal`).
- **Body**:
```json
{
  "kind": "Expense",
  "amountVnd": 150000,
  "recipientUserId": "b4c238b1-36aa-4e9e-9988-123456789abc",
  "purpose": "Mua nước ngọt và bánh mì",
  "idempotencyKey": "req-20261101-001",
  "securityPin": "123456"
}
```
- **Lưu ý**: Mỗi thành viên chỉ được có tối đa 1 yêu cầu chi tiêu đang hoạt động cùng lúc.

#### `GET /api/v1/funds/{id}/requests?page=1&pageSize=20`
Lấy danh sách yêu cầu.
- **Quyền**: Trưởng nhóm thấy toàn bộ yêu cầu; Thành viên thường chỉ thấy yêu cầu do chính mình tạo.

#### `GET /api/v1/funds/requests/{id}`
Lấy chi tiết một yêu cầu cụ thể.
- **Quyền**: Chỉ Trưởng nhóm hoặc Người tạo yêu cầu. Người ngoài nhận `403 Forbidden`.

#### `POST /api/v1/funds/requests/{id}/review`
Trưởng nhóm duyệt hoặc từ chối yêu cầu.
- **Body**:
```json
{
  "approved": true,
  "reason": "Duyệt chi",
  "securityPin": "123456"
}
```

#### `POST /api/v1/funds/requests/{id}/cancel`
Người tạo tự hủy yêu cầu đang chờ duyệt hoặc đã duyệt nhưng chưa dùng.
- **Body**:
```json
{
  "securityPin": "123456"
}
```

#### `POST /api/v1/funds/requests/{id}/initiate-payment`
Bắt đầu thanh toán hóa đơn chi tiêu đối ngoại từ quyền chi `ApprovedUnused`.
- **Hành vi thực tế tại Backend hiện tại**:
  Khi cổng thanh toán đối ngoại (External Payment Provider) chưa sẵn sàng hoặc chưa được cấu hình, Backend trả về HTTP `409 Conflict` với mã lỗi `fund_payment_provider_unavailable`. Quyền chi tiêu an toàn giữ nguyên trạng thái `ApprovedUnused` và không bị khóa quỹ.
- **Body**:
```json
{
  "securityPin": "123456"
}
```

---

### 2.4. Hoàn tiền, Rời Quỹ, Kick & Giải tán

#### `GET /api/v1/funds/{id}/refund-preview`
Xem trước số tiền hoàn lại trước khi rời nhóm.
- **Query**: `memberUserId` (nếu Trưởng nhóm xem cho thành viên; để trống nếu thành viên xem cho chính mình).
- **Response `200 OK`**:
```json
{
  "fundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "b4c238b1-36aa-4e9e-9988-123456789abc",
  "currentFundBalanceVnd": 1000000.0,
  "eligibleRefundAmountVnd": 400000,
  "caseType": "CaseC_FreeEquityRefund",
  "status": "Eligible"
}
```
*(Ghi chú: Để tra cứu chi tiết số tiền đã nạp và phần chi phí đã gánh, Mobile UI gọi endpoint `GET /api/v1/funds/{id}/equity`).*

#### `POST /api/v1/funds/{id}/leave`
Thành viên tự rời quỹ và nhận hoàn tiền về ví cá nhân.
- **Body**:
```json
{
  "securityPin": "123456",
  "idempotencyKey": "leave-key-001",
  "destinationWalletId": null
}
```

#### `POST /api/v1/funds/{id}/members/{userId}/kick`
Trưởng nhóm xóa thành viên khỏi quỹ và hoàn tiền.
- **Body**:
```json
{
  "securityPin": "123456",
  "reason": "Vi phạm quy chế",
  "idempotencyKey": "kick-key-001"
}
```

#### `POST /api/v1/funds/{id}/dissolve`
Trưởng nhóm giải tán quỹ. Hoàn toàn bộ số dư cho tất cả thành viên còn hoạt động.
- **Body**:
```json
{
  "securityPin": "123456",
  "idempotencyKey": "dissolve-key-001"
}
```

---

## 3. BẢNG MÃ LỖI CHUẨN HÓA

| HTTP Code | Error Code | Mô tả |
| :--- | :--- | :--- |
| **404** | `fund_not_found` | Quỹ không tồn tại hoặc người dùng không phải thành viên |
| **404** | `fund_request_not_found` | Yêu cầu không tồn tại |
| **403** | `Forbidden` | Không có quyền truy cập yêu cầu của người khác |
| **409** | `member_pending_requests` | Thành viên còn yêu cầu đang chờ duyệt, phải hủy trước khi rời/kick |
| **409** | `fund_payment_processing` | Quỹ đang có khoản chi đang xử lý thanh toán chưa đối soát |
| **409** | `fund_payment_provider_unavailable` | Cổng thanh toán hóa đơn đối ngoại chưa sẵn sàng |
| **409** | `owner_cannot_leave_free_fund` | Trưởng nhóm không thể tự rời quỹ Free Fund |
| **409** | `insufficient_unreserved_fund_balance` | Số dư khả dụng chưa phân bổ của quỹ không đủ để cấp quyền chi |
| **409** | `idempotency_conflict` | Idempotency key đã được dùng ở lượt tham gia trước hoặc mục đích khác |
| **409** | `fund_already_dissolved` | Quỹ đã giải tán |
| **409** | `free_fund_election_disallowed` | Quỹ Free Fund không hỗ trợ bầu cử |
| **409** | `free_fund_ownership_transfer_disallowed`| Quỹ Free Fund không hỗ trợ chuyển nhượng nhóm |
| **400** | `invalid_pin` | Mã PIN bảo mật không đúng |
| **400** | `invalid_amount` | Số tiền không hợp lệ |

---

## 4. HỆ THỐNG THÔNG BÁO (NOTIFICATIONS & OUTBOX)

Tất cả các thông báo của quỹ được phân phối với `NotificationType = V1FundActivity`:
- `FundRequestCreated`: Gửi riêng cho Trưởng nhóm khi thành viên tạo yêu cầu mới. Thành viên khác không nhận.
- `FundExpenseApproved` / `FundWithdrawalExecuted`: Gửi riêng cho Thành viên tạo yêu cầu khi được duyệt.
- `FundRequestRejected`: Gửi riêng cho Thành viên tạo yêu cầu kèm lý do.
- `FundRequestCancelled`: Gửi riêng cho Trưởng nhóm khi thành viên tự hủy.
- `FundRequestExpired`: Gửi Thành viên và Trưởng nhóm khi quyền chi quá hạn 7 ngày tự động hết hạn.
- `MemberLeft`: Gửi Trưởng nhóm và các thành viên khi có người tự rời nhóm.
- `MemberKicked`: Gửi riêng cho Thành viên bị kick khỏi nhóm.
- `FundDissolved`: Gửi toàn bộ thành viên khi nhóm được giải tán.
