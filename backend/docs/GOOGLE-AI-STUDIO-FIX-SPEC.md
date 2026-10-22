# GOOGLE AI STUDIO FRONTEND FIX SPECIFICATION

> **Dành cho:** Kỹ sư hoàn thiện Frontend Google AI Studio / SpendWise User Mobile  
> **Mục tiêu:** Đồng bộ 100% Mock State và Data Contract của Google AI Studio với Backend ASP.NET Core chính thức.  
> **Nguyên tắc:** Mô phỏng đúng các bước xác thực bảo mật, phân định rõ định danh `userId` vs `requestId`, và giữ nghiêm ngặt nghiệp vụ Quỹ cố định vs Quỹ tự do.

---

## 1. Yêu cầu Sửa Đổi Cấp Bách (Priority 0)

### 1.1 Khắc phục Luồng Đăng nhập 2 Bước (Auth PIN Challenge Flow)
- **Vấn đề hiện tại:** Sau khi nhập Email và Mật khẩu, Frontend tự động cho rằng đăng nhập thành công và mở Dashboard ngay lập tức.
- **Quy chuẩn Backend:**
  - `POST /api/auth/login` kiểm tra mật khẩu.
  - Nếu người dùng đã cài PIN, Backend trả về response yêu cầu xác thực PIN (kèm theo `challengeToken` hoặc phiên tạm thời).
  - Người dùng **bắt buộc** phải nhập mã PIN 6 số tại màn hình nhập PIN (`POST /api/auth/pin/verify`).
  - Chỉ sau khi `POST /api/auth/pin/verify` trả về mã HTTP 200 kèm Access Token JWT đầy đủ thì ứng dụng mới được lưu token và chuyển hướng vào Dashboard.
- **Đặc tả Component cần bổ sung/chỉnh sửa:**
  - Component: `LoginScreen.tsx` hoặc `AuthModal.tsx`.
  - Khi `login()` trả về trạng thái yêu cầu PIN → Hiển thị bàn phím số nhập mã PIN 6 số với hiệu ứng che dấu chấm tròn.
  - Xử lý lỗi: Nếu sai PIN quá 5 lần, Backend sẽ khóa tạm thời (`HTTP 403 Forbidden` / `pin_locked`). Hiển thị cảnh báo số lần thử còn lại.

---

### 1.2 Tích hợp Khám phá Người dùng Mới & Phân định Định danh (User Discovery & Request ID Integrity)
- **Vấn đề hiện tại:** 
  1. Thanh tìm kiếm tại tab Bạn bè (`FriendsScreen.tsx`) chỉ lọc chuỗi trên mảng bạn bè hiện có (`friends.filter(...)`). Nếu gõ tên người lạ ("Nguyen"), giao diện hiển thị "Không tìm thấy người dùng phù hợp".
  2. Frontend dễ nhầm lẫn giữa **`userId`** và **`requestId`** khi thao tác với các mối quan hệ bạn bè.
- **Quy chuẩn Backend & Phân định Định danh:**
  - Endpoint tìm kiếm toàn cục: `GET /api/friends/discover?query={term}&page={page}&pageSize={pageSize}`
  - Model trả về: `PagedResult<UserDiscoveryDto>`
    ```typescript
    interface UserDiscoveryDto {
      userId: string;          // GUID của User được tìm thấy
      fullName: string;
      avatarUrl: string | null;
      spendId: string;
      relationshipState: 'NONE' | 'REQUEST_SENT' | 'REQUEST_RECEIVED' | 'FRIENDS';
    }
    ```
  - **LƯU Ý ĐẶC BIỆT VỀ ĐỊNH DANH:**
    - `UserDiscoveryDto` **CHỈ CHỨA `userId`**, hoàn toàn **KHÔNG CHỨA `requestId`**.
    - Khi gửi lời mời mới: `POST /api/friends/requests` với body `{ "contact": user.spendId }` (hoặc phone/email).
    - Khi nhận được kết quả `relationshipState === 'REQUEST_SENT'` (chờ đối phương phản hồi): Muốn **Hủy** lời mời, Frontend phải tra cứu `requestId` từ danh sách lời mời đã gửi (`GET /api/friends/requests/outgoing`), sau đó gọi `DELETE /api/friends/requests/{requestId}`. **Tuyệt đối KHÔNG truyền `userId` vào URL xóa request!**
    - Khi nhận được kết quả `relationshipState === 'REQUEST_RECEIVED'` (đối phương gửi cho mình): Muốn **Chấp nhận**, Frontend phải tra cứu `requestId` từ danh sách lời mời đến (`GET /api/friends/requests/incoming`), sau đó gọi `POST /api/friends/requests/{requestId}/accept`.
  - Rate limiting: Endpoint `GET /api/friends/discover` có giới hạn 30 lượt gọi/phút. Cần debounce tối thiểu 300ms - 500ms trước khi gửi request.

---

### 1.3 Quy trình Đăng ký 3 Bước Rời (Register → Email OTP → PIN Setup)
- **Vấn đề hiện tại:** Form Đăng ký hiện tại gộp chung cả Mật khẩu và Mã PIN 6 số trên cùng một form 1 bước, sau đó tạo tài khoản ngay.
- **Quy chuẩn Backend:**
  - **Bước 1:** `POST /api/auth/register` (nhận Họ tên, Email, Mật khẩu, Số điện thoại).
  - **Bước 2:** Xác thực email qua mã OTP 8 chữ số (`POST /api/auth/email-verification/verify`).
  - **Bước 3:** Cài đặt mã PIN giao dịch 6 chữ số lần đầu (`POST /api/auth/pin/setup`).
  - Frontend cần tách thành quy trình wizard 3 bước rõ ràng để phản ánh trung thực kiến trúc bảo mật của SpendWise.

---

## 2. Yêu cầu Hoàn thiện Dữ liệu & Nghiệp vụ (Priority 1)

### 2.1 Cập nhật Quy trình Xác thực Đổi Email
- **Endpoint:** `POST /api/users/me/email/confirm`
- **Body gửi lên Backend:**
  ```json
  {
    "otp": "12345678"
  }
  ```
  *(Lưu ý: Không gửi kèm `newEmail` hay `token` ở bước Confirm; Backend lấy trực tiếp `PendingNewEmail` từ phiên User hiện tại trong transaction nguyên tử).*

### 2.2 Đọc đúng thuộc tính Danh sách Bạn bè & Thành viên Quỹ
- **Danh sách Bạn bè (`GET /api/friends`):**
  - Đọc trường thời gian bắt đầu kết bạn là `friendsSince` (ISO 8601 string).
  - Đọc trường `avatarUrl` để render ảnh đại diện thật (nếu `null` mới fallback về 2 chữ cái viết tắt `NM`, `BN`).
- **Danh sách Thành viên Quỹ (`GET /api/v1/funds/{id}/members`):**
  - Đọc các trường: `userId`, `seat`, `isOwner`, `canSpend`, `joinedAt`, `fullName`, `avatarUrl`, `displayName`.
  - Hiển thị huy hiệu "Chủ nhóm" dựa trên thuộc tính `isOwner === true`.

### 2.3 Phân biệt rõ Fixed Fund và Free Fund
- **Quỹ cố định (Fixed):** Cho phép Chủ nhóm bật/tắt quyền chi tiêu (`canSpend`) cho thành viên. Áp dụng hạn mức rút 20% và khóa thời gian 24 giờ.
- **Quỹ tự do (Free):** **Tuyệt đối KHÔNG hiển thị switch `canSpend`**. Mọi khoản chi tiêu/rút tiền của thành viên đều bắt buộc tạo Request và chờ Chủ nhóm phê duyệt (`POST /api/v1/funds/requests/{id}/review`).
