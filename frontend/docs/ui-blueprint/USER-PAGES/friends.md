# USER PAGE INVENTORY: FRIENDS & CONTACTS (BẠN BÈ & LIÊN HỆ)
**Page ID:** `USER-FRIENDS`  
**Route:** `/friends` (Alias: `/contacts`)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/friends/FriendsPage.tsx` (1105 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm danh bạ tài chính xã hội: tìm kiếm người dùng SpendWise qua số điện thoại/email, gửi và chấp nhận lời mời kết bạn, gắn sao bạn bè thân thiết (Favorites), đặt biệt danh (Nickname), xem lịch sử giao dịch chuyển tiền qua lại với từng người bạn, và kích hoạt chuyển tiền nhanh 1 chạm.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `FRND-SEC-01` | Friends Header & Add Friend Action | `FriendsPage.tsx` lines 210-270 | Đầu trang (Top header) | Luôn hiển thị |
| `FRND-SEC-02` | Tabs Bar: All Friends / Pending Requests | `FriendsPage.tsx` lines 280-330 | Hàng 1 (Tab chuyển đổi) | Luôn hiển thị |
| `FRND-SEC-03` | Friends Search & Favorite Filter | `FriendsPage.tsx` lines 340-390 | Thanh tìm kiếm | Luôn hiển thị |
| `FRND-SEC-04` | Friends Cards & Interactions Grid | `FriendsPage.tsx` lines 400-650 | Thân trang (Danh sách bạn bè) | Luôn hiển thị |
| `FRND-SEC-05` | Pending Friend Requests Tray | `FriendsPage.tsx` lines 660-800 | Tab Lời mời (Requests) | Khi xem tab Lời mời |
| `FRND-SEC-06` | Friend Profile & Mutual Tx Drawer | `FriendsPage.tsx` lines 810-1050 | Drawer bên phải | Khi click vào 1 người bạn |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section FRND-SEC-01 & 02: Header & Tabs
- **Feature ID:** `FRIEND-001`
  - **Tên:** Nút Thêm bạn mới (+ Kết bạn)
  - **Mục đích:** Mở modal nhập số điện thoại hoặc email để tìm kiếm và gửi lời mời kết bạn.
  - **API Call:** `friendService.sendFriendRequest(targetIdentifier)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `FRIEND-002`
  - **Tên:** Tab Danh sách bạn bè & Tab Lời mời kết bạn chờ duyệt
  - **Mục đích:** Hiển thị số lượng bạn bè và số lời mời kết bạn đang chờ (`pendingCount`).
  - **Verification:** `SOURCE_VERIFIED`.

### Section FRND-SEC-03 & 04: Friends Interactions
- **Feature ID:** `FRIEND-003`
  - **Tên:** Gắn sao bạn bè thân thiết (Toggle Favorite)
  - **Mục đích:** Đưa người bạn lên đầu danh sách gợi ý chuyển tiền nhanh (`isFavorite`).
  - **API Call:** `friendService.toggleFavorite(friendId)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `FRIEND-004`
  - **Tên:** Đặt biệt danh cho bạn bè (Edit Nickname)
  - **Mục đích:** Tùy chỉnh tên gọi thân mật (ví dụ: "Mẹ yêu", "Sếp Hùng").
  - **API Call:** `friendService.setNickname(friendId, nickname)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `FRIEND-005`
  - **Tên:** Chuyển tiền nhanh cho bạn (Quick Transfer Button)
  - **Mục đích:** Bấm nút "Chuyển tiền" trên thẻ bạn bè để nhảy ngay sang `/transfer?phone={friendPhone}`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section FRND-SEC-05 & 06: Requests & Mutual History
- **Feature ID:** `FRIEND-006`
  - **Tên:** Đồng ý / Từ chối lời mời kết bạn (Accept/Reject Requests)
  - **Mục đích:** Xử lý các lời mời kết bạn từ người khác gửi đến.
  - **API Calls:** `friendService.acceptRequest(requestId)`, `rejectRequest(requestId)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `FRIEND-007`
  - **Tên:** Xem lịch sử giao dịch chung với bạn bè (Mutual Transactions Drawer)
  - **Mục đích:** Thống kê tổng số tiền đã chuyển cho nhau, lịch sử các lần chuyển tiền trực tiếp trong quá khứ.
  - **Verification:** `SOURCE_VERIFIED`.
