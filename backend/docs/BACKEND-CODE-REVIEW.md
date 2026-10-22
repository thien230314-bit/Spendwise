# BACKEND CODE REVIEW & AUDIT REPORT

> **Target Baseline:** Git commit `b5e1dcbd5ba425057814257818936873daefc390` (Branch: `main`)  
> **Repository:** https://github.com/thien230314-bit/Spendwise  
> **Review Scope:** Commits `04aefec` -> `b5e1dcb`, B1/B2 enhancements, social discovery, rates, contracts, duplicate queries, N+1 analysis, security & privacy boundaries.

---

## 1. Executive Summary

- **Mâu thuẫn nghiệp vụ:** **KHÔNG CÓ.** Các thay đổi từ `04aefec` đến `b5e1dcb` hoàn toàn tuân thủ các quy tắc cốt lõi của SpendWise về Ledger, Ví cá nhân, Quỹ nhóm (Fixed & Free Fund), PIN, Auth, và bảo vệ trạng thái bạn bè.
- **Code thừa / Dead code / Nghi trùng lặp:** **KHÔNG CÓ.** Các trường dữ liệu mới (`AvatarUrl`, `FullName`, `UserDiscoveryDto`, `userDiscovery` rate limiter) đều được tiêu thụ hoặc phục vụ trực tiếp cho API contract mở rộng mà không làm phát sinh duplication hay logic rác.
- **N+1 Queries:** **ĐÃ TRIỆT TIÊU TOÀN BỘ (0 N+1).** Cả `DiscoverUsersQueryHandler` và `V1FundService.MembersAsync` đều sử dụng phép chiếu JOIN trực tiếp tại SQL database hoặc truy vấn theo lô (`HashSet<Guid>`) cho trang hiện tại.
- **Tương thích ngược (Backward Compatibility):** **100% BẢO TOÀN.** Cấu trúc các constructor cũ (`V1MemberDto`, `FriendContactDto`, `FriendDto`) vẫn hỗ trợ giá trị mặc định (`FullName = ""`, `AvatarUrl = null`), giúp toàn bộ các bài test cũ biên dịch và chạy không đổi.

---

## 2. Chi tiết 4 Điểm Rà soát Nghi vấn Hợp đồng (Contract Discrepancies)

Trong quá trình rà soát đối chiếu giữa source code Backend và tài liệu `USER-FRONTEND-API-ALIGNMENT.md`:

1. **Payload Confirm Đổi Email:**
   - *Thực tế Backend:* [`ConfirmChangeEmailCommand.cs`](file:///E:/source/SpendWise-final-merged/backend/SpendWise.Application/Features/Users/Commands/ConfirmChangeEmail/ConfirmChangeEmailCommand.cs) định nghĩa `public sealed record ConfirmChangeEmailRequest(string Otp);`.
   - *Khắc phục:* Đã cập nhật tài liệu chính xác thành `{ "otp": "..." }` thay vì `{ "newEmail": "...", "token": "..." }`.
2. **Trường Thời gian Kết bạn trong `FriendDto`:**
   - *Thực tế Backend:* [`FriendContracts.cs`](file:///E:/source/SpendWise-final-merged/backend/SpendWise.Application/Social/FriendContracts.cs) định nghĩa trường là `DateTime FriendsSince`.
   - *Khắc phục:* Đã sửa mẫu JSON trong tài liệu thành `"friendsSince": "2026-10-22T04:45:00Z"` (không phải `acceptedAt`).
3. **Thuộc tính Thành viên Quỹ trong `V1MemberDto`:**
   - *Thực tế Backend:* [`V1FundService.cs`](file:///E:/source/SpendWise-final-merged/backend/SpendWise.Application/Features/V1Groups/V1FundService.cs) chứa các thuộc tính `UserId`, `Seat`, `IsOwner`, `CanSpend`, `JoinedAt`, `FullName`, `AvatarUrl`. Không có trường `LeftAt` (thành viên đã rời quỹ không nằm trong danh sách active members của `MembersAsync`).
   - *Khắc phục:* Đã đồng bộ JSON mẫu trong tài liệu sang `isOwner: true` và loại bỏ `leftAt`.
4. **Quy tắc Tìm kiếm Danh bạ Chính xác (`GET /api/friends/search`):**
   - *Thực tế Backend:* [`FriendFeature.cs`](file:///E:/source/SpendWise-final-merged/backend/SpendWise.Application/Features/Friends/FriendFeature.cs) hỗ trợ `kind` là `null`, `"auto"`, `"email"`, `"phone"`, `"spendId"`.
   - *Chính sách bảo mật thực tế:*
     - Khi tìm theo `spendId`: `Phone` trả về `null` trừ khi đã là `FRIENDS`.
     - Khi tìm theo số điện thoại (người dùng đã biết trước số điện thoại): Trả về số điện thoại xác nhận người dùng tìm đúng.
   - *Khắc phục:* Tài liệu đã được bổ sung giải thích đầy đủ hành vi phân loại này.

---

## 3. Kiến trúc, Hiệu năng & Bảo mật

| Tiêu chí | Đánh giá | Bằng chứng kiểm tra trong mã nguồn |
|---|:---:|---|
| **Rate Limiting** | **ĐẠT** | Endpoint `GET /api/friends/discover` được bảo vệ độc lập bởi policy `userDiscovery` (30 req/phút theo `UserId`), trả mã HTTP 429 khi vượt ngưỡng, không làm nghẽn các API bạn bè khác. |
| **Chống Tràn số (Overflow Guard)** | **ĐẠT** | Giới hạn `query.Page <= 100,000` chặn triệt để nguy cơ tràn số nguyên `int` trong phép tính `(page - 1) * pageSize` của LINQ `.Skip()`. |
| **Quyền riêng tư (Privacy Boundary)** | **ĐẠT** | Biệt danh bạn bè (`FriendPreference.Nickname`) là dữ liệu cá nhân hóa cục bộ của chủ sở hữu; tìm kiếm khám phá người dùng chỉ truy vấn trường danh tính hợp pháp `FullName`. |
| **Bảo vệ Dữ liệu Nhạy cảm** | **ĐẠT** | `UserDiscoveryDto` không chứa email, số điện thoại, số dư tài khoản hay cấu hình bảo mật của người lạ. Loại trừ chính người dùng gọi và các tài khoản hệ thống/đình chỉ. |
| **Cô lập Cơ sở Dữ liệu** | **ĐẠT** | Không có migration EF Core mới; toàn bộ unit/integration test chạy trên in-memory SQLite fixture, hoàn toàn cô lập và không đụng vào `SQL01`. |
