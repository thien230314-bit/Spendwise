# External API Capability Audit — SpendWise V1.0

Ngày kiểm tra: **2026-10-20 (Asia/Saigon)**. Baseline `main` tại `ef5f8a696ddacd6a3670d1e59e359fc4b8cae1af`; origin `https://github.com/thien230314-bit/Spendwise`; working tree sạch và HEAD/origin/main đồng bộ trước audit.

Phạm vi: source, tài liệu chính thức, cấu hình cục bộ và kết nối an toàn. Chỉ sửa tài liệu. Không truy cập SQL01 hoặc bất kỳ database nào; không inference, gửi email/push, tạo tài nguyên provider, giao dịch tiền, migration hay đổi cấu hình. **Full Newman E2E NOT RUN**. Build/targeted Core tests NOT RUN trong task docs-only; không tái sử dụng PASS cũ thành kết quả mới. Core giữ trạng thái `CORE USER & ADMIN READY FOR EXTERNAL API VERIFICATION`; Frontend/Mobile chưa nghiệm thu.

## A. Provider Inventory

Các đường dẫn dưới đây tính từ repository root. Backend net10.0 dùng HTTP/BCL, không có SDK SePay, Google AI hay Expo chuyên dụng.

| Provider | API / thư viện | Source đang sử dụng | Phân loại |
|---|---|---|---|
| SePay Bank Hub | REST `/v1`, HttpClient, timeout20s | `backend/SpendWise.Infrastructure/Banking/SePayBankHubGateway.cs`, `SePayBankHubOptions.cs`, `SePayBankHubConfiguration.cs` | Adapter thật; token Basic → Bearer; Hosted Link, đọc lịch sử, simulator Sandbox |
| SePay Test Mode | REST `/v2`, HttpClient, timeout20s | `backend/SpendWise.Infrastructure/Banking/SePayTestModeAccountReader.cs`; `backend/SpendWise.Application/Features/BankConnections/TestModeSePayFeature.cs` | Development-only; Bearer token riêng; đọc account/transaction Sandbox; không phải adapter phát lệnh trích tiền |
| Gemini | REST `v1beta`, HttpClient, timeout30s | `backend/SpendWise.Infrastructure/AI/GeminiCompletionProvider.cs` | `x-goog-api-key`; model explicit; không có OpenAI fallback |
| Gmail SMTP | `smtp.gmail.com:587`, STARTTLS; .NET `System.Net.Mail.SmtpClient` | `backend/SpendWise.Infrastructure/Identity/ConfiguredEmailSender.cs` | Template HTML/text; email outbox retry tối đa5; Testing cưỡng chế Pickup |
| Expo Push Service | REST `/--/api/v2/push/send`, `/getReceipts`; HttpClient | `backend/SpendWise.Infrastructure/MobilePush/ExpoPushSender.cs`, `MobilePushDispatcher.cs`, `MobilePushReceiptReconciler.cs`; `backend/SpendWise/Controllers/MobilePushController.cs` | Dispatcher/outbox, receipts, retry tối đa8; hiện disabled |

Bank Hub dùng `https://bankhub-api-sandbox.sepay.vn` hoặc `https://bankhub-api.sepay.vn`. Test Mode dùng `https://userapi-sandbox.sepay.vn/v2`; đây là hai sản phẩm/credentials khác nhau. Gemini dùng `https://generativelanguage.googleapis.com`; Expo dùng `https://exp.host`.

Kiểm kê HTTP clients, URLs, packages, adapters, controller/webhook và jobs không tìm thấy provider Backend runtime khác. Local UserSecrets còn key OpenAI nhưng không có adapter/SDK/fallback đang dùng: không gọi thử, không đổi/xóa key. SWBank chỉ còn lịch sử migration. SQL Server/EF/SqlClient là database drivers, OpenXml/PDF là xử lý cục bộ. UI preview có ảnh `images.unsplash.com`: tài nguyên ảnh ngoài, không phải API tài chính; NOT TESTED. Không sửa Frontend/Mobile.

### Cấu hình và credentials quan sát được

Đọc bằng parser hỗ trợ cả JSON lồng và key dạng `Section:Key`, chỉ xuất presence/giá trị công khai. Thứ tự cấu hình Development: appsettings → Development settings → UserSecrets → process environment. Không in địa chỉ email, tên/chỉ số tài khoản, ID giao dịch, key hay password. Không có process environment xác định phiên chạy app; các kết luận sau là cấu hình hiệu lực **nếu chạy Development**, không chứng nhận deployment đang chạy.

| Nhóm | Cấu hình cục bộ Development | Kết quả |
|---|---|---|
| Bank Hub | Enabled=false; Environment=Sandbox; base URLs đúng; ClientId/ClientSecret/CompanyXid/LifecycleWebhookSecret/IpnApiKey và mobile completion redirect thiếu | `CREDENTIALS_REQUIRED`; chưa auth Bank Hub |
| SePay TestMode | ApiToken có trong UserSecrets; không có khoảng trắng thừa | Auth và hai GET Sandbox thành công |
| Gemini | AI provider Gemini; ApiKey có; Model=`gemini-1.5-flash`; env alias `GEMINI_API_KEY` không có | Key hợp lệ nhưng model sai vòng đời |
| SMTP | DeliveryMode=Smtp; host Gmail, port587, EnableSsl=true; From/Username/Password có; From khớp Username | TLS/auth thành công; không gửi thư |
| Expo | `MobilePush:Enabled` không đặt → mặc định false | Có adapter, delivery chưa bật; không có option server AccessToken |

Production không load Development UserSecrets. Cấu hình Production quan sát được tại máy này không có bộ model/key/SMTP/Bank Hub cần thiết, Bank Hub/Push chưa bật. Secrets ở deployment bên ngoài không quan sát được: **UNKNOWN**, không kết luận production đã cấu hình hay credentials bị từ chối. Không sửa settings/credentials.

## B. Capability Matrix

`AUTH_VERIFIED` chỉ xác nhận credentials được chấp nhận ở endpoint đã thử. `SANDBOX_VERIFIED` chỉ cho phép đọc Sandbox đã nêu. Không có bằng chứng `FEATURE_NOT_ENTITLED`, `QUOTA_BLOCKED` hoặc `UNSUPPORTED` cho mọi sản phẩm SePay; không gán các nhãn này từ việc thiếu public endpoint.

| Capability | Officially Supported | Existing Code | Auth Verified | Sandbox Verified | Conclusion / action |
|---|---|---|---|---|---|
| Bank Linking | Bank Hub Hosted Link được công bố [S1,S2] | CreateLink + event handler | Không; thiếu Bank Hub credentials | Không | `DOCUMENTED_ONLY`, `CREDENTIALS_REQUIRED`; KEEP / WAIT_FOR_CREDENTIALS |
| Read Available Balance | Account có accumulated; chưa xác nhận available/realtime [S3,S4,S5] | TestMode đọc accumulated; Bank Hub không có balance method | Chỉ v2 account GET | Chỉ schema account GET | `UNKNOWN`; NEED_PROVIDER_CONFIRMATION |
| Authorized Bank Debit | Chưa có bằng chứng trong Bank Hub catalogue đã đọc [S1] | Không có debit initiation method | Không | Không | `UNKNOWN` capability; chưa triển khai; NEED_PROVIDER_CONFIRMATION / REPLACE_CANDIDATE nếu không đáp ứng |
| Wallet Top-Up | Quan sát giao dịch có; linked-bank debit chưa xác minh | TestMode Pending + exact match + ledger; không phát lệnh ngân hàng | Chỉ v2 reads | Không tạo/confirm topup | All-or-nothing code nội bộ không chứng minh topup thật; provider integration PENDING |
| Wallet Withdrawal | Chưa xác minh payout tổng quát; refund có giới hạn riêng [S7] | Gateway không có payout method | Không | Không | `PAYOUT_API_NOT_AVAILABLE` trong adapter/Bank Hub catalogue đã rà; quyền tài khoản UNKNOWN; NEED_PROVIDER_CONFIRMATION |
| Webhook | Lifecycle/IPN được công bố [S4,S6] | Secret validation, ingestion, dedup | Không | Không gọi webhook | `DOCUMENTED_ONLY`, `CREDENTIALS_REQUIRED`; KEEP |
| Reconciliation | Có transaction history [S8,S9] | Đọc/paging, matching/dedup hiện có | v2 history GET thành công | v2 history GET200 | `AUTH_VERIFIED`, `SANDBOX_VERIFIED` cho đọc; settlement/timeout E2E NOT RUN |

## C. SePay Banking Gaps

### Version, linking và permissions

Code Bank Hub `/v1` khác SePay user API v1/v2. Host, Basic token, Hosted Link request/response và transaction pagination của adapter phù hợp tài liệu đang công bố [S1,S2,S8]. API v2 user dùng UUID/integer VND và nested pagination; nguồn v1 vẫn được công bố hoạt động song song, không được gắn DEPRECATED chỉ vì có v2 [S10]. Không có SDK cần nâng cấp trong task này.

Bank Hub token TTL/cache, semaphore single-flight và retry401 một lần đã có code. Account linking cần company/account context, redirect và người dùng hoàn tất Hosted Link. Đồng ý liên kết **không chứng minh** mandate trích tiền. Chưa có Bank Hub credentials nên account permissions, danh sách ngân hàng thực tế và trạng thái hợp đồng đều UNKNOWN. TestMode token đọc thành công không thay cho quyền Bank Hub. SePay v2 công bố giới hạn3 requests/giây [S11]; quota/gói phí/entitlements tài khoản hiện tại chưa được xác minh.

### Available balance và vô hiệu hóa nguồn thiếu tiền

Yêu cầu sản phẩm giữ nguyên: `AvailableBankBalance >= RequestedTopUpAmount + SourceFees` nếu nguồn chịu phí. UI phải làm mờ nguồn thiếu tiền; Backend kiểm tra lại trước phát lệnh. **FRONTEND PENDING**.

Không có bằng chứng `accumulated` là số dư khả dụng realtime sau holds/phí cho tài khoản cá nhân liên kết. Tài liệu Bank Hub IPN ghi trường số dư sau giao dịch chưa hỗ trợ, mặc định0 [S4]. TestMode mô tả accumulated dạng số dư mô phỏng/cộng dồn và việc cập nhật phụ thuộc nguồn/ngân hàng [S5]. Vì vậy GET account200 và giá trị Int64 không đủ làm nguồn quyết định thanh toán thật.

Code hiện chỉ so sánh `AccumulatedBalance` ở đường TestMode; Bank Hub gateway chưa expose available balance. Cần SePay xác nhận endpoint, freshness/TTL, available vs ledger balance, holds/phí, consent/scopes, tài khoản cá nhân, danh sách ngân hàng, tần suất/chi phí. Trạng thái bắt buộc: **UNKNOWN / NEED_PROVIDER_CONFIRMATION**; không hạ yêu cầu PO.

### Authorized debit và all-or-nothing top-up

`IBankHubGateway` chỉ expose create link, đọc transactions và simulator Sandbox; không có lệnh debit/payment initiation. `transfer_type=debit` trong lịch sử là quan sát tiền ra, không phải API ra lệnh trích tiền. Chưa xác minh mandate cá nhân, ngân hàng hỗ trợ, idempotency của lệnh debit hay trạng thái payment cuối.

TestMode có Pending→Processing atomic claim, match đúng account/direction/reference, exact AmountOut bằng số yêu cầu, AmountIn=0, dedup provider transaction rồi post toàn bộ ledger; không nạp một phần hoặc cộng ví trước xác nhận. Đây là logic hiện có được đọc source, **không chạy lại tests/settlement trong audit này**, không phải bằng chứng ngân hàng đã trích tiền. Không tạo mock hay gọi simulator để lấp capability thiếu.

IPN route `/api/integrations/sepay/bankhub/ipn` xác thực Apikey; lifecycle `/events` dùng X-Secret-Key. `ProcessBankTransactionIpnCommandHandler` ghi nhận external BankTransaction, không tự ghi có Wallet/Ledger/FinancialTransaction. Nhận định Task16.3 “IPN đã hỗ trợ nạp ví” được hiệu chỉnh tại đây và User/Admin: ingestion không đồng nghĩa wallet settlement. Chưa thử public HTTPS delivery, retry, duplicate, timeout hoặc failed-payment reconciliation thực tế.

### Withdrawal/payout

Không có outgoing payout operation/status contract trong gateway hiện tại. **PAYOUT_API_NOT_AVAILABLE chỉ áp dụng adapter và Bank Hub public catalogue đã rà**, không khẳng định mọi sản phẩm SePay không có chi tiền hoặc tài khoản bị từ chối entitlement.

SePay API v2 có hoàn tiền giao dịch thu gốc, giới hạn VietinBank doanh nghiệp đủ điều kiện/kích hoạt; yêu cầu idempotency key. Sandbox không chứng minh production eligibility [S7]. Refund này không tự đáp ứng rút ví tùy ý hay trích tài khoản cá nhân. Không gọi refund/payout; account entitlement UNKNOWN. Cần hợp đồng/quyền và tài liệu status/reconciliation/idempotency; nếu SePay không đáp ứng thì đề xuất ngân hàng/PSP có mandate, balance và disbursement phù hợp, chỉ ở mức **REPLACE_CANDIDATE**, chưa chọn/triển khai.

## D. Gemini / SMTP / Push

### Gemini

Key hiện tại được chấp nhận: model listing200, 63 models. Cấu hình `gemini-1.5-flash` không có trong list; GET metadata model đó404. Changelog Google ghi Gemini1.5 đã shutdown ngày2025-09-29 [G1]. Kết luận: **AUTH_VERIFIED cho key; DEPRECATED (đã shutdown) + CONFIG_ERROR cho model; inference/chat NOT VERIFIED**. Không gọi generateContent vì model đang cấu hình không dùng được; không tự thay model.

### Gemini (Cập nhật Task G1 — 2026-10-20)
Product Owner đã chọn model gemini-3.5-flash-lite (Free Tier). Cấu hình đã được cập nhật qua User Secrets (Gemini:Model = gemini-3.5-flash-lite) và ppsettings.Development.json.
- API Key: AUTH_VERIFIED (Google API listing 50 models trả về HTTP 200).
- Model: MODEL_VERIFIED (models/gemini-3.5-flash-lite tồn tại và hỗ trợ generateContent).
- Free Tier: Confirmed; không bật billing, không phát sinh chi phí.
- Synthetic Inference: INFERENCE_VERIFIED (single test hoàn tất với finishReason STOP, 17 tokens).
- Unit & HTTP Test Suite: 28/28 provider tests PASS; 24/24 AI HTTP tests PASS.


Metadata bằng key hiện tại liệt kê `gemini-3.5-flash-lite`, `gemini-3.8-flash`, `gemini-2.5-flash`, `gemini-2.5-flash-lite`, đều hỗ trợ generateContent, input1,048,576/output65,536 tokens. Metadata không chứng minh quota/inference thành công [G2]. Đề xuất PO cân nhắc3.5 Flash-Lite cho chi phí;3.8 Flash nếu cần năng lực cao hơn. Giá Standard công bố tại ngày audit:3.5 Flash-Lite USD0.30 input/2.50 output mỗi triệu tokens;3.8 Flash USD0.75/3.75 đến2026-12-31, USD1.50/7.50 từ2027-01-01 [G3]. Đây không phải dự toán/billing của tài khoản. Quota phụ thuộc model/project/tier, cần chủ tài khoản kiểm tra dashboard [G4]. Model2.5 có hạn chế availability cho người dùng mới theo trang vòng đời [G5]; không chọn mặc định chỉ vì list có tên.

Adapter REST v1beta hiện có contents user/model, systemInstruction, inlineData, functionDeclarations.parametersJsonSchema, tool loop tối đa5 và server allowlist/confirmation. Đây là schema code, chưa kiểm chứng inference/tool compatibility với model thay thế. Không có SDK bắt buộc nâng cấp ngay; bước sau là duyệt model/config/budget rồi synthetic inference và tool contract an toàn. Missing model/key hoặc lỗi transport trả controlled503; provider “Configured” chỉ là presence. Key resolver dùng null-coalescing giữa Gemini:ApiKey và env alias, không bảo đảm fallback nếu config là chuỗi rỗng; không gặp trường hợp đó trong cấu hình đã kiểm tra.

### SMTP

**SMTP_CONFIGURATION_PRESENT; SMTP_CONNECTION_VERIFIED; SMTP_AUTH_VERIFIED; DELIVERY_NOT_TESTED.** Gmail587 greeting/EHLO250, STARTTLS, chứng chỉ/hostname hợp lệ, TLS1.3, một AUTH qua TLS235, NOOP250. Không gửi MAIL FROM/RCPT/DATA hay message. Credentials hợp lệ trong phép thử này không chứng minh inbox, SPF/DKIM/DMARC, alias, quota hoặc production secrets.

Development có thể explicit Smtp; mặc định Pickup. Testing luôn Pickup; Production cần cấu hình SMTP. `.test` và `.invalid` bị chặn trước SMTP Internet. Source có template HTML/text và outbox retry5; không kiểm tra retry bằng gửi mail. Gmail/App Password guidance còn công bố [M1,M2]. SmtpClient tồn tại trong .NET10 nhưng Microsoft không khuyến nghị cho phát triển mới [M3]; không đồng nghĩa API đã bị gỡ. KEEP cấu hình hiện tại; OAuth/transport upgrade chỉ khi chính sách triển khai yêu cầu, không tự đổi thư viện.

### Push

Provider thực tế là **Expo Push Service**, không phải Backend FCM trực tiếp. Endpoint getReceipts với `ids:[]` trả200/data object: **OPERATIONAL_VERIFIED chỉ cho public empty receipt lookup**; không AUTH_VERIFIED, không ticket, không delivery. Delivery disabled theo cấu hình; backend có device registration theo user/session, dispatcher/retry8 và receipt reconciliation xử lý DeviceNotRegistered. Submitted/receipt success không chứng minh thiết bị hiển thị [P1].

Expo enhanced push security có bearer token tùy chọn; adapter hiện không có AccessToken option/header, sẽ cần thay đổi riêng nếu project bật chế độ này [P1]. Không suy diễn public receipt200 thành project credentials hợp lệ.

Mobile package khai báo expo~57.0.26, expo-constants~57.0.20 nhưng chưa có expo-notifications/expo-device/firebase dependency. Native registration/build/permission và Android FCMv1/iOS APNs credentials chưa nghiệm thu/không kiểm tra EAS. Tài liệu setup yêu cầu notification libraries, token, user permission và platform credentials [P2]. **FRONTEND/MOBILE PENDING; PUSH DELIVERY NOT VERIFIED**. KEEP Expo; cấu hình triển khai và client pending, không tự bật gửi.

## E. Evidence

Kết nối thực tế ngày2026-10-20; lượt probe đầu08:14 UTC (15:14 Asia/Saigon). Chỉ ghi HTTP/SMTP status và schema an toàn, không lưu raw bank payload vào repository. Không dùng kết quả build/mock để xác nhận provider.

| Phép thử thực tế | Kết quả | Phạm vi bằng chứng |
|---|---|---|
| Gemini GET `/v1beta/models?pageSize=1000`, private API-key header | 200;63 models | AUTH_VERIFIED, metadata list |
| Gemini GET `/v1beta/models/gemini-1.5-flash` | 404 | Model cấu hình không khả dụng; không inference |
| SePay Sandbox GET `/v2/bank-accounts?page=1&per_page=1` bằng .NET HttpClient như adapter | 200; status success; data array; meta.pagination.has_more boolean; accumulated Int64, id/active string | AUTH_VERIFIED + SANDBOX_VERIFIED cho account read; không available balance guarantee |
| SePay Sandbox GET `/v2/transactions?page=1&per_page=1` cùng HttpClient | 200; status success; amounts Int64, IDs/date/direction string | AUTH_VERIFIED + SANDBOX_VERIFIED cho history read; không settlement |
| Gmail587 STARTTLS/auth/NOOP | 250 / TLS1.3 /235 /250 | Connection + auth only; không email |
| Expo POST `/--/api/v2/push/getReceipts`, `{"ids":[]}` | 200; data object | Reachability/empty query only; không push |

Lượt Python urllib ban đầu trả403 cho hai SePay v2 GET; chẩn đoán một account GET với User-Agent riêng trả200, sau đó cả hai GET .NET không User-Agent đều200 với cùng token đã trim. Token không có whitespace thừa. Chưa xác định nguyên nhân request-dependent403; **không gán AUTH_FAILED**. Anonymous Bank Hub `/v1/merchant-config` cũng403; thiếu credentials nên không làm authenticated Bank Hub call, không dùng kết quả đó kết luận dịch vụ ngừng hay thiếu entitlement. Không retry vô hạn.

### Tài liệu chính thức đối chiếu ngày2026-10-20

- [S1 — Bank Hub overview/auth/endpoints](https://developer.sepay.vn/vi/bankhub/api/tong-quan-api)
- [S2 — Hosted Link request/response](https://developer.sepay.vn/vi/bankhub/api/link-token/tao-link-token)
- [S3 — Bank Hub accounts](https://developer.sepay.vn/vi/bankhub/api/tai-khoan-ngan-hang/danh-sach-tai-khoan)
- [S4 — Bank Hub IPN và accumulated](https://developer.sepay.vn/vi/bankhub/thong-bao-bien-dong-so-du)
- [S5 — Test Mode simulation/balance limitations](https://developer.sepay.vn/vi/tien-ich-khac/test-mode/mo-phong-giao-dich)
- [S6 — Lifecycle webhook](https://developer.sepay.vn/vi/bankhub/webhook-event)
- [S7 — API v2 refund eligibility/idempotency](https://developer.sepay.vn/vi/sepay-api/v2/hoan-tien)
- [S8 — Bank Hub transaction history](https://developer.sepay.vn/vi/bankhub/api/api-giao-dich/danh-sach-giao-dich)
- [S9 — SePay API v2](https://developer.sepay.vn/vi/sepay-api/v2/gioi-thieu)
- [S10 — v1/v2 migration and coexistence](https://developer.sepay.vn/vi/sepay-api/v2/nang-cap-tu-v1)
- [S11 — v2 authentication/rate limit](https://developer.sepay.vn/vi/sepay-api/v2/xac-thuc)
- [G1 — Gemini changelog/shutdown](https://ai.google.dev/gemini-api/docs/changelog)
- [G2 — Models metadata API](https://ai.google.dev/api/models)
- [G3 — Gemini current pricing](https://ai.google.dev/gemini-api/docs/pricing)
- [G4 — Gemini rate limits](https://ai.google.dev/gemini-api/docs/rate-limits)
- [G5 — Gemini model lifecycle](https://ai.google.dev/gemini-api/docs/deprecations)
- [M1 — Gmail client configuration](https://support.google.com/mail/answer/7126229)
- [M2 — Google App Passwords](https://support.google.com/accounts/answer/185833)
- [M3 — .NET10 SmtpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient?view=net-10.0)
- [P1 — Expo sending, receipts and security](https://docs.expo.dev/push-notifications/sending-notifications/)
- [P2 — Expo client/platform setup](https://docs.expo.dev/push-notifications/push-notifications-setup/)

## F. Final Recommendation

| Provider/Feature | Status | Keep or Replace | Reason | Next Action |
|---|---|---|---|---|
| SePay v2 TestMode reads | AUTH_VERIFIED / SANDBOX_VERIFIED | KEEP | Account/history200; schema khớp | Giữ giới hạn Sandbox; chưa promote production |
| Bank Hub linking/webhook | DOCUMENTED_ONLY / CREDENTIALS_REQUIRED | KEEP / WAIT_FOR_CREDENTIALS | Adapter có; thiếu bộ quyền/key riêng | PO cấp Sandbox ClientId/ClientSecret/CompanyXid, webhook secrets, redirects/public HTTPS an toàn qua secret store |
| Available balance + authorized debit | UNKNOWN | NEED_PROVIDER_CONFIRMATION / REPLACE_CANDIDATE | accumulated/observed debit không đủ | PO lấy xác nhận hợp đồng, bank support, balance freshness/fees, mandate và API initiation/status/idempotency; nếu thiếu thì chọn đối tác đáp ứng |
| Payout | PAYOUT_API_NOT_AVAILABLE trong phạm vi adapter/catalogue; entitlement UNKNOWN | NEED_PROVIDER_CONFIRMATION / REPLACE_CANDIDATE | Không có payout method; enterprise original-refund là capability khác | PO cung cấp hợp đồng/entitlement và API payout/status/reconciliation; không giả định corporate refund đủ rút ví |
| Gemini | AUTH_VERIFIED key; DEPRECATED + CONFIG_ERROR model | KEEP provider / CONFIG_FIX + UPGRADE model | Model1.5 shutdown, metadata404 | PO duyệt model3.5 Flash-Lite hoặc3.8 Flash/budget/quota; sau đó task config + synthetic contract test riêng |
| SMTP Gmail | Connection/auth VERIFIED; DELIVERY_NOT_TESTED | KEEP | TLS/auth thành công | Operator cung cấp production secret deployment và kế hoạch controlled delivery acceptance riêng; không cần đổi key vì audit này |
| Expo | Public receipt lookup VERIFIED; delivery disabled/unverified | KEEP / CONFIG_FIX; conditional security UPGRADE | Adapter có, chưa nghiệm thu client/platform | PO cung cấp project/platform credentials qua secret store, quyết định enhanced security; task riêng cho client/build/delivery |

Quyết định audit: không thay provider, không đổi logic all-or-nothing, model, cấu hình hay dependency. Các credentials Gemini/TestMode/SMTP hiện có được chấp nhận ở phép thử đã nêu, không yêu cầu reset vô cớ. Production provisioning, Bank Hub credentials và quyền nghiệp vụ vẫn cần chủ tài khoản; không gửi secrets vào tài liệu/chat. User.md/Admin.md được đồng bộ capability, không hạ yêu cầu sản phẩm hoặc dùng network metadata thay nghiệm thu UI/Core E2E. Dừng sau bàn giao; chưa khởi động task thay thế.
