# GOOGLE AI STUDIO FRONTEND FIX SPECIFICATION

> **Dành cho:** Kỹ sư hoàn thiện Frontend Google AI Studio / SpendWise User Mobile  
> **Mục tiêu:** Đồng bộ 100% Client-side Logic với Backend ASP.NET Core chính thức.  
> **Nguyên tắc:** Không giả lập popup thành công nếu Backend từ chối; thực thi đúng luồng xác thực PIN và phân trang API.

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

### 1.2 Tích hợp Khám phá Người dùng Mới (`GET /api/friends/discover`)
- **Vấn đề hiện tại:** Thanh tìm kiếm tại tab Bạn bè (`FriendsScreen.tsx`) chỉ lọc chuỗi trên mảng bạn bè hiện có (`friends.filter(...)`). Nếu gõ tên người lạ ("Nguyen"), giao diện hiển thị "Không tìm thấy người dùng phù hợp".
- **Quy chuẩn Backend:**
  - Endpoint: `GET /api/friends/discover?query={term}&page={page}&pageSize={pageSize}`
  - Tham số: `query` tối thiểu 2 ký tự, tối đa 80 ký tự.
  - Rate limiting: Endpoint có giới hạn 30 lượt gọi/phút. Cần debounce tối thiểu 300ms - 500ms trước khi gửi request.
  - Phản hồi: `PagedResult<UserDiscoveryDto>`:
    ```typescript
    interface UserDiscoveryDto {
      userId: string;
      fullName: string;
      avatarUrl: string | null;
      spendId: string;
      relationshipState: 'NONE' | 'REQUEST_SENT' | 'REQUEST_RECEIVED' | 'FRIENDS';
    }
    ```
- **Xử lý UI tương ứng theo `relationshipState`:**
  - `NONE`: Hiển thị nút "Kết bạn" (`POST /api/friends/requests`).
  - `REQUEST_SENT`: Hiển thị nhãn "Đã gửi lời mời" kèm nút "Hủy" (`DELETE /api/friends/requests/{id}`).
  - `REQUEST_RECEIVED`: Hiển thị nút "Chấp nhận" (`POST /api/friends/requests/{id}/accept`) và "Từ chối".
  - `FRIENDS`: Hiển thị nút "Chuyển tiền" hoặc nhãn "Bạn bè".

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
