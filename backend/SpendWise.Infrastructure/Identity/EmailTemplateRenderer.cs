using System.Net;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Renderer tập trung giữ mọi email giao dịch trong cùng một visual shell an toàn cho email client.
public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string AutomatedFooter =
        "Email giao dịch tự động từ SpendWise — nền tảng quản lý tài chính cá nhân. Vui lòng không trả lời email này.";

    public RenderedEmail Render(string templateName, IReadOnlyDictionary<string, string?> values)
    {
        if (string.Equals(templateName, "registration-verification", StringComparison.Ordinal))
            return RegistrationVerification(values);
        if (string.Equals(templateName, "registration-welcome", StringComparison.Ordinal))
            return RegistrationWelcome(values);
        if (string.Equals(templateName, "password-reset-verification", StringComparison.Ordinal))
            return PasswordResetVerification(values);
        if (string.Equals(templateName, "password-reset-success", StringComparison.Ordinal))
            return PasswordResetSuccess(values);
        if (string.Equals(templateName, "support-received", StringComparison.Ordinal))
            return SupportReceived(values);
        if (string.Equals(templateName, "support-resolved", StringComparison.Ordinal))
            return SupportDecision(values, resolved: true);
        if (string.Equals(templateName, "support-rejected", StringComparison.Ordinal))
            return SupportDecision(values, resolved: false);
        if (string.Equals(templateName, "support-info-request", StringComparison.Ordinal))
            return SupportInfoRequest(values);
        if (string.Equals(templateName, "support-fund-investigation", StringComparison.Ordinal))
            return SupportFundInvestigation(values);
        if (string.Equals(templateName, "welcome-back", StringComparison.Ordinal))
            return WelcomeBack(values);
        if (string.Equals(templateName, "new-device-login", StringComparison.Ordinal))
            return NewDeviceLogin(values);
        if (string.Equals(templateName, "security-pin-reset", StringComparison.Ordinal))
            return SecurityPinReset(values);
        if (string.Equals(templateName, "email-change-verification", StringComparison.Ordinal))
            return EmailChangeVerification(values);
        if (string.Equals(templateName, "email-change-notice", StringComparison.Ordinal))
            return EmailChangeNotice(values);

        return Generic(values);
    }

    private static RenderedEmail EmailChangeVerification(IReadOnlyDictionary<string, string?> values)
    {
        var email = Value(values, "Email", string.Empty);
        var otp = Value(values, "Otp", string.Empty);
        var minutes = Value(values, "Minutes", "10");
        var content = Intro("Bạn vừa yêu cầu thay đổi địa chỉ email cho tài khoản SpendWise của mình. Sử dụng mã OTP bên dưới để xác nhận email mới.") +
                      OtpCard("Mã xác thực đổi email", otp) +
                      DetailCard(
                          DetailRow("Email mới", email) +
                          DetailRow("Thời hạn", $"{minutes} phút")) +
                      SecurityNote(
                          "Không chia sẻ mã này với bất kỳ ai. Nếu bạn không yêu cầu thay đổi email, hãy kiểm tra bảo mật tài khoản ngay lập tức.") +
                      MutedNote("Mã OTP này chỉ có hiệu lực một lần.");
        var text = $"[SpendWise] Xác nhận thay đổi email\n\n" +
                   $"Email mới: {email}\nMã xác thực: {otp}\nMã có hiệu lực trong {minutes} phút.\n\n" +
                   "Không chia sẻ mã này với bất kỳ ai.\n\n" + AutomatedFooter;
        return new RenderedEmail(
            Layout(
                "SpendWise — Xác nhận thay đổi email",
                "Mã xác thực đổi địa chỉ email của bạn",
                "BẢO MẬT TÀI KHOẢN",
                "Xác nhận thay đổi email",
                content),
            text);
    }

    private static RenderedEmail EmailChangeNotice(IReadOnlyDictionary<string, string?> values)
    {
        var oldEmail = Value(values, "OldEmail", string.Empty);
        var newEmail = Value(values, "NewEmail", string.Empty);
        var content = Intro("Địa chỉ email liên kết với tài khoản SpendWise của bạn vừa được thay đổi thành công.") +
                      DetailCard(
                          DetailRow("Email cũ", oldEmail) +
                          DetailRow("Email mới", newEmail) +
                          DetailRow("Thời gian", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss") + " UTC")) +
                      SecurityNote(
                          "Nếu bạn không thực hiện thay đổi này, hãy liên hệ ngay với bộ phận hỗ trợ SpendWise để bảo vệ tài khoản của bạn.");
        var text = $"[SpendWise] Cảnh báo bảo mật: Email của bạn đã được thay đổi\n\n" +
                   $"Email cũ: {oldEmail}\nEmail mới: {newEmail}\n\n" +
                   "Nếu bạn không thực hiện thay đổi này, vui lòng liên hệ hỗ trợ ngay.\n\n" + AutomatedFooter;
        return new RenderedEmail(
            Layout(
                "SpendWise — Thông báo thay đổi email",
                "Email của tài khoản đã được thay đổi",
                "CẢNH BÁO BẢO MẬT",
                "Thay đổi email thành công",
                content),
            text);
    }

    private static RenderedEmail RegistrationVerification(IReadOnlyDictionary<string, string?> values)
    {
        var email = Value(values, "Email", string.Empty);
        var otp = Value(values, "Otp", string.Empty);
        var minutes = Value(values, "Minutes", "10");
        var content = Intro("Dùng mã bên dưới để hoàn tất việc xác thực tài khoản SpendWise của bạn.") +
                      OtpCard("Mã xác thực của bạn", otp) +
                      DetailCard(
                          DetailRow("Email nhận", email) +
                          DetailRow("Thời hạn", $"{minutes} phút")) +
                      SecurityNote(
                          "Không chia sẻ mã này với bất kỳ ai. SpendWise sẽ không bao giờ yêu cầu bạn cung cấp mã qua điện thoại hoặc tin nhắn.") +
                      MutedNote("Nếu bạn không yêu cầu đăng ký tài khoản, bạn có thể bỏ qua email này.");
        var text = $"[SpendWise] Mã xác thực đăng ký\n\n" +
                   $"Email nhận: {email}\nMã xác thực: {otp}\nMã có hiệu lực trong {minutes} phút.\n\n" +
                   "Không chia sẻ mã này với bất kỳ ai. SpendWise sẽ không yêu cầu mã qua điện thoại hoặc tin nhắn.\n\n" +
                   "Nếu bạn không yêu cầu đăng ký tài khoản, hãy bỏ qua email này.\n\n" + AutomatedFooter;
        return new RenderedEmail(
            Layout(
                "SpendWise — Xác thực tài khoản",
                "Mã xác thực tài khoản SpendWise của bạn",
                "BẢO MẬT TÀI KHOẢN",
                "Xác thực tài khoản SpendWise",
                content),
            text);
    }

    private static RenderedEmail RegistrationWelcome(IReadOnlyDictionary<string, string?> values)
    {
        var userName = Value(values, "UserName", "bạn");
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var actionText = Value(values, "ActionText", "Truy cập SpendWise");
        var content = Greeting(userName) +
                      Intro("Tài khoản của bạn đã được kích hoạt thành công. SpendWise đã sẵn sàng giúp bạn theo dõi dòng tiền, ngân sách và mục tiêu tài chính rõ ràng hơn.") +
                      InformationNote("Bước tiếp theo", "Thiết lập PIN bảo mật, sau đó bắt đầu thêm ví và ngân sách đầu tiên của bạn.") +
                      ActionButton(actionUrl, actionText);
        var text = $"[SpendWise] Chào mừng bạn đến với SpendWise\n\nXin chào {userName},\n\n" +
                   "Tài khoản của bạn đã được kích hoạt thành công. SpendWise giúp bạn theo dõi dòng tiền, ngân sách và mục tiêu tài chính.\n\n" +
                   "Bước tiếp theo: Thiết lập PIN bảo mật, sau đó bắt đầu thêm ví và ngân sách đầu tiên.\n\n" +
                   $"{actionText}: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Chào mừng",
                "Tài khoản SpendWise của bạn đã sẵn sàng",
                "TÀI KHOẢN ĐÃ KÍCH HOẠT",
                "Chào mừng bạn đến với SpendWise",
                content),
            text);
    }

    private static RenderedEmail PasswordResetVerification(IReadOnlyDictionary<string, string?> values)
    {
        var email = Value(values, "Email", string.Empty);
        var otp = Value(values, "Otp", string.Empty);
        var minutes = Value(values, "Minutes", "10");
        var content = Intro("SpendWise đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.") +
                      OtpCard("Mã OTP đặt lại mật khẩu", otp) +
                      DetailCard(
                          DetailRow("Email nhận", email) +
                          DetailRow("Thời hạn", $"{minutes} phút") +
                          DetailRow("Hiệu lực", "Chỉ sử dụng một lần")) +
                      SecurityNote("Không chia sẻ mã này. Nếu bạn không gửi yêu cầu, hãy bỏ qua email và giữ nguyên mật khẩu hiện tại.");
        var text = $"[SpendWise] Mã OTP đặt lại mật khẩu\n\nEmail nhận: {email}\nMã OTP: {otp}\n" +
                   $"Mã có hiệu lực trong {minutes} phút và chỉ sử dụng một lần.\n\n" +
                   "Không chia sẻ mã này. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email.\n\n" + AutomatedFooter;
        return new RenderedEmail(
            Layout(
                "SpendWise — Đặt lại mật khẩu",
                "Mã OTP đặt lại mật khẩu SpendWise của bạn",
                "YÊU CẦU BẢO MẬT",
                "Đặt lại mật khẩu SpendWise",
                content),
            text);
    }

    private static RenderedEmail PasswordResetSuccess(IReadOnlyDictionary<string, string?> values)
    {
        var userName = Value(values, "UserName", "bạn");
        var changedAt = Value(values, "ChangedAt", string.Empty);
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var actionText = Value(values, "ActionText", "Truy cập SpendWise");
        var content = Greeting(userName) +
                      SuccessNote("Mật khẩu đã được thay đổi", "Mật khẩu SpendWise của bạn đã được đặt lại thành công. Tất cả phiên đăng nhập cũ đã bị thu hồi.") +
                      DetailCard(DetailRow("Thời gian (UTC)", changedAt)) +
                      SecurityNote("Nếu bạn không thực hiện thay đổi này, hãy liên hệ hỗ trợ SpendWise ngay.") +
                      ActionButton(actionUrl, actionText);
        var text = $"[SpendWise] Mật khẩu đã được đặt lại\n\nXin chào {userName},\n\n" +
                   "Mật khẩu SpendWise của bạn đã được đặt lại thành công. Tất cả phiên đăng nhập cũ đã bị thu hồi.\n" +
                   $"Thời gian (UTC): {changedAt}\n\nNếu bạn không thực hiện thay đổi này, hãy liên hệ hỗ trợ SpendWise ngay.\n\n" +
                   $"{actionText}: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Mật khẩu đã thay đổi",
                "Mật khẩu SpendWise của bạn đã được thay đổi",
                "XÁC NHẬN BẢO MẬT",
                "Mật khẩu đã được đặt lại",
                content),
            text);
    }

    private static RenderedEmail SupportReceived(IReadOnlyDictionary<string, string?> values)
    {
        var ticketNumber = Value(values, "TicketNumber", string.Empty);
        var subject = Value(values, "Subject", string.Empty);
        var submittedAt = Value(values, "SubmittedAt", string.Empty);
        var status = Value(values, "Status", string.Empty);
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var content = Intro("SpendWise đã nhận yêu cầu hỗ trợ của bạn. Đội ngũ phụ trách sẽ xem xét và cập nhật trạng thái trong hệ thống.") +
                      StatusBadge(status, "#075985", "#e0f2fe") +
                      DetailCard(
                          DetailRow("Mã yêu cầu", ticketNumber) +
                          DetailRow("Chủ đề", subject) +
                          DetailRow("Thời gian gửi", submittedAt)) +
                      InformationNote("Lưu ý", "Bạn có thể theo dõi yêu cầu sau khi đăng nhập. Không cần gửi lại cùng một nội dung.") +
                      ActionButton(actionUrl, "Xem yêu cầu hỗ trợ");
        var text = $"[SpendWise] Đã nhận yêu cầu hỗ trợ\n\nSpendWise đã nhận yêu cầu của bạn.\n" +
                   $"Mã yêu cầu: {ticketNumber}\nChủ đề: {subject}\nThời gian gửi: {submittedAt}\nTrạng thái: {status}\n\n" +
                   $"Xem yêu cầu sau khi đăng nhập: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Đã nhận yêu cầu hỗ trợ",
                $"Yêu cầu {ticketNumber} đã được ghi nhận",
                "TRUNG TÂM HỖ TRỢ",
                "Đã nhận yêu cầu hỗ trợ",
                content),
            text);
    }

    private static RenderedEmail SupportDecision(
        IReadOnlyDictionary<string, string?> values,
        bool resolved)
    {
        var ticketNumber = Value(values, "TicketNumber", string.Empty);
        var message = Value(values, "Message", string.Empty);
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var heading = resolved ? "Yêu cầu hỗ trợ đã được giải quyết" : "Cập nhật yêu cầu hỗ trợ";
        var status = resolved ? "Đã giải quyết" : "Không được chấp thuận";
        var messageLabel = resolved ? "Nội dung giải quyết" : "Lý do";
        var statusColor = resolved ? "#166534" : "#92400e";
        var statusBackground = resolved ? "#dcfce7" : "#fef3c7";
        var intro = resolved
            ? "Yêu cầu hỗ trợ của bạn đã được xử lý và chuyển sang trạng thái hoàn tất."
            : "Yêu cầu hỗ trợ của bạn đã được xem xét nhưng hiện chưa thể được chấp thuận.";
        var followUp = resolved
            ? "Nếu cần thêm trợ giúp, bạn có thể tạo yêu cầu mới trong SpendWise."
            : "Bạn có thể xem lý do bên dưới và gửi một yêu cầu mới với thông tin phù hợp nếu vẫn cần hỗ trợ.";
        var content = Intro(intro) +
                      StatusBadge(status, statusColor, statusBackground) +
                      DetailCard(
                          DetailRow("Mã yêu cầu", ticketNumber) +
                          DetailRow(messageLabel, message, multiline: true)) +
                      MutedNote(followUp) +
                      ActionButton(actionUrl, "Xem yêu cầu hỗ trợ");
        var text = $"[SpendWise] {heading}\n\nMã yêu cầu: {ticketNumber}\nTrạng thái: {status}\n" +
                   $"{messageLabel}: {message}\n\n{followUp}\n\nXem yêu cầu sau khi đăng nhập: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                $"SpendWise — {heading}",
                $"Trạng thái mới của yêu cầu {ticketNumber}: {status}",
                "TRUNG TÂM HỖ TRỢ",
                heading,
                content),
            text);
    }

    private static RenderedEmail SupportInfoRequest(IReadOnlyDictionary<string, string?> values)
    {
        var ticketNumber = Value(values, "TicketNumber", string.Empty);
        var subject = Value(values, "Subject", string.Empty);
        var requiredDocs = Value(values, "RequiredDocuments", "Sao kê hoặc chứng từ giao dịch");
        var note = Value(values, "Note", string.Empty);
        var actionUrlRaw = Value(values, "ActionUrl", "https://spendwise.local/support");
        var actionUrl = ValidActionUrl(actionUrlRaw);
        var content = Intro("Đội ngũ hỗ trợ SpendWise cần thêm thông tin/chứng từ để tiếp tục xử lý yêu cầu của bạn.") +
                      StatusBadge("Cần bổ sung chứng từ", "#c2410c", "#ffedd5") +
                      DetailCard(
                          DetailRow("Mã yêu cầu", ticketNumber) +
                          DetailRow("Chủ đề", subject) +
                          DetailRow("Chứng từ yêu cầu", requiredDocs, multiline: true) +
                          (string.IsNullOrWhiteSpace(note) ? "" : DetailRow("Ghi chú", note, multiline: true))) +
                      InformationNote("Hướng dẫn", "Vui lòng đăng nhập ứng dụng SpendWise và tải lên chứng từ được yêu cầu trong chi tiết yêu cầu hỗ trợ.") +
                      ActionButton(actionUrl, "Xem và bổ sung chứng từ");
        var text = $"[SpendWise] Yêu cầu bổ sung chứng từ cho yêu cầu {ticketNumber}\n\n" +
                   $"Mã yêu cầu: {ticketNumber}\nChủ đề: {subject}\nChứng từ cần bổ sung: {requiredDocs}\n" +
                   (string.IsNullOrWhiteSpace(note) ? "" : $"Ghi chú: {note}\n") +
                   $"\nXem chi tiết và tải lên chứng từ: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Yêu cầu bổ sung chứng từ",
                $"Yêu cầu {ticketNumber} cần bổ sung chứng từ",
                "TRUNG TÂM HỖ TRỢ",
                "Yêu cầu bổ sung chứng từ",
                content),
            text);
    }

    private static RenderedEmail SupportFundInvestigation(IReadOnlyDictionary<string, string?> values)
    {
        var ticketNumber = Value(values, "TicketNumber", string.Empty);
        var fundName = Value(values, "FundName", string.Empty);
        var currentPhase = Value(values, "CurrentPhase", "Rà soát đối chiếu sổ cái");
        var note = Value(values, "Note", string.Empty);
        var actionUrlRaw = Value(values, "ActionUrl", "https://spendwise.local/support");
        var actionUrl = ValidActionUrl(actionUrlRaw);
        var content = Intro("Hồ sơ quỹ liên quan đến yêu cầu hỗ trợ của bạn đang được đội ngũ quản trị tiến hành điều tra, đối soát.") +
                      StatusBadge("Đang rà soát đối chiếu", "#0369a1", "#e0f2fe") +
                      DetailCard(
                          DetailRow("Mã yêu cầu", ticketNumber) +
                          DetailRow("Tên quỹ", fundName) +
                          DetailRow("Giai đoạn hiện tại", currentPhase) +
                          (string.IsNullOrWhiteSpace(note) ? "" : DetailRow("Ghi chú", note, multiline: true))) +
                      InformationNote("Thông báo an toàn", "Mọi giao dịch và số dư liên quan đang được đối chiếu dựa trên sổ cái bất biến. Kết quả điều tra sẽ được cập nhật khi hoàn tất.") +
                      ActionButton(actionUrl, "Xem chi tiết yêu cầu");
        var text = $"[SpendWise] Tiến độ điều tra quỹ — Yêu cầu {ticketNumber}\n\n" +
                   $"Mã yêu cầu: {ticketNumber}\nTên quỹ: {fundName}\nGiai đoạn: {currentPhase}\n" +
                   (string.IsNullOrWhiteSpace(note) ? "" : $"Ghi chú: {note}\n") +
                   $"\nXem chi tiết: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Tiến độ điều tra quỹ",
                $"Cập nhật điều tra quỹ cho yêu cầu {ticketNumber}",
                "TRUNG TÂM HỖ TRỢ",
                "Tiến độ điều tra quỹ",
                content),
            text);
    }

    private static RenderedEmail WelcomeBack(IReadOnlyDictionary<string, string?> values)
    {
        var userName = Value(values, "UserName", "bạn");
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var content = Greeting(userName) +
                      Intro("Đã một thời gian kể từ lần đăng nhập trước. Chào mừng bạn quay trở lại SpendWise.") +
                      InformationNote("Tiếp tục từ nơi bạn dừng lại", "Xem lại ví, ngân sách và mục tiêu để giữ kế hoạch tài chính luôn rõ ràng.") +
                      ActionButton(actionUrl, "Mở SpendWise");
        var text = $"[SpendWise] Chào mừng bạn quay lại\n\nXin chào {userName},\n\n" +
                   "Đã một thời gian kể từ lần đăng nhập trước. Chào mừng bạn quay trở lại SpendWise.\n\n" +
                   "Xem lại ví, ngân sách và mục tiêu để tiếp tục kế hoạch tài chính của bạn.\n\n" +
                   $"Mở SpendWise: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Chào mừng trở lại",
                "Chào mừng bạn quay trở lại SpendWise",
                "RẤT VUI ĐƯỢC GẶP LẠI BẠN",
                "Chào mừng bạn quay trở lại SpendWise",
                content),
            text);
    }

    private static RenderedEmail NewDeviceLogin(IReadOnlyDictionary<string, string?> values)
    {
        var userName = Value(values, "UserName", "bạn");
        var loginTime = Value(values, "LoginTime", string.Empty);
        var deviceName = Value(values, "DeviceName", "Không có thông tin");
        var browserInfo = Value(values, "BrowserInfo", "Không có thông tin");
        var ipAddress = Value(values, "IpAddress", "Không có thông tin");
        var actionUrl = ValidActionUrl(Value(values, "ActionUrl", string.Empty));
        var content = Greeting(userName) +
                      Intro("SpendWise ghi nhận một lần đăng nhập thành công từ trình duyệt chưa được nhận diện trước đó.") +
                      DetailCard(
                          DetailRow("Thời gian đăng nhập", loginTime) +
                          DetailRow("Tên thiết bị do client cung cấp", deviceName) +
                          DetailRow("Trình duyệt", browserInfo) +
                          DetailRow("IP kết nối", ipAddress)) +
                      MutedNote("Thông tin kết nối chỉ hỗ trợ nhận biết hoạt động, không phải bằng chứng về danh tính thiết bị hoặc vị trí địa lý.") +
                      SecurityNote("Nếu đây không phải là bạn, hãy đổi mật khẩu, kiểm tra các phiên đăng nhập và liên hệ hỗ trợ SpendWise.") +
                      ActionButton(actionUrl, "Mở SpendWise");
        var text = $"[SpendWise] Cảnh báo đăng nhập từ thiết bị mới\n\nXin chào {userName},\n\n" +
                   "SpendWise ghi nhận một lần đăng nhập thành công từ trình duyệt chưa được nhận diện.\n" +
                   $"Thời gian đăng nhập: {loginTime}\nTên thiết bị do client cung cấp: {deviceName}\n" +
                   $"Thông tin trình duyệt: {browserInfo}\nIP kết nối: {ipAddress}\n\n" +
                   "Thông tin kết nối không phải bằng chứng về danh tính thiết bị hoặc vị trí địa lý. " +
                   "Nếu đây không phải là bạn, hãy đổi mật khẩu, kiểm tra phiên đăng nhập và liên hệ hỗ trợ.\n\n" +
                   $"Mở SpendWise: {actionUrl}\n\n{AutomatedFooter}";
        return new RenderedEmail(
            Layout(
                "SpendWise — Cảnh báo đăng nhập",
                "Phát hiện đăng nhập từ trình duyệt mới",
                "THÔNG BÁO BẢO MẬT",
                "Đăng nhập từ thiết bị mới",
                content),
            text);
    }

    private static RenderedEmail SecurityPinReset(IReadOnlyDictionary<string, string?> values)
    {
        var token = Value(values, "Token", string.Empty);
        var expiresAt = Value(values, "ExpiresAt", string.Empty);
        var link = Value(values, "ActionUrl", string.Empty);
        if (link.Length > 0) link = ValidActionUrl(link);
        var content = Intro("Use the one-time verification token below to continue resetting your SpendWise security PIN.") +
                      OtpCard("One-time reset token", token) +
                      DetailCard(DetailRow("Expires at (UTC)", expiresAt)) +
                      SecurityNote("Never share this token or your security PIN. SpendWise support will not ask you to disclose either one.") +
                      MutedNote("If you did not request a security PIN reset, ignore this email and review your account security.");
        if (link.Length > 0) content += ActionButton(link, "Reset security PIN");
        var text = "SpendWise Security PIN reset\n\n" +
                   (link.Length > 0 ? $"Reset security PIN: {link}\n\n" : string.Empty) +
                   $"One-time reset token: {token}\nExpires at (UTC): {expiresAt}\n\n" +
                   "Never share this token or your security PIN. SpendWise support will not ask for either one.\n\n" +
                   "If you did not request this reset, ignore this email and review your account security.\n\n" +
                   "This is an automated transactional email from SpendWise. Please do not reply.";
        return new RenderedEmail(
            Layout(
                "SpendWise — Security PIN reset",
                "Your one-time SpendWise security PIN reset token",
                "ACCOUNT SECURITY",
                "Reset your security PIN",
                content,
                "This is an automated transactional email from SpendWise, your personal finance management platform. Please do not reply."),
            text);
    }

    private static RenderedEmail Generic(IReadOnlyDictionary<string, string?> values)
    {
        var title = Value(values, "Title", "SpendWise");
        var heading = Value(values, "Heading", title);
        var body = Value(values, "Body", string.Empty);
        var footer = Value(values, "Footer", AutomatedFooter);
        var content = Intro(body);
        return new RenderedEmail(
            Layout(title, heading, "THÔNG BÁO SPENDWISE", heading, content, footer),
            $"{title}\n\n{body}\n\n{footer}");
    }

    private static string Layout(
        string title,
        string preheader,
        string eyebrow,
        string heading,
        string content,
        string footer = AutomatedFooter)
    {
        var encodedTitle = Encode(title);
        var encodedPreheader = Encode(preheader);
        var encodedEyebrow = Encode(eyebrow);
        var encodedHeading = Encode(heading);
        var encodedFooter = Encode(footer);
        return "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\">" +
               "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
               $"<meta name=\"x-apple-disable-message-reformatting\"><title>{encodedTitle}</title></head>" +
               "<body style=\"margin:0;padding:0;background-color:#eef5ff;color:#0f172a;font-family:Arial,'Helvetica Neue',sans-serif;-webkit-text-size-adjust:100%;\">" +
               $"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;\">{encodedPreheader}</div>" +
               "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" bgcolor=\"#eef5ff\">" +
               "<tr><td align=\"center\" style=\"padding:32px 12px;\">" +
               "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"max-width:620px;\">" +
               BrandHeader() +
               "<tr><td bgcolor=\"#ffffff\" style=\"padding:36px 40px 32px;border:1px solid #dbeafe;border-top:0;border-radius:0 0 14px 14px;box-shadow:0 8px 24px rgba(15,23,42,0.06);\">" +
               $"<p style=\"margin:0 0 10px;color:#0284c7;font-size:12px;line-height:18px;font-weight:700;letter-spacing:1.4px;\">{encodedEyebrow}</p>" +
               $"<h1 style=\"margin:0 0 22px;color:#0f172a;font-size:26px;line-height:34px;font-weight:700;\">{encodedHeading}</h1>" +
               content +
               "</td></tr>" +
               $"<tr><td align=\"center\" style=\"padding:22px 28px 0;color:#64748b;font-size:12px;line-height:18px;\">{encodedFooter}<br>© SpendWise</td></tr>" +
               "</table></td></tr></table></body></html>";
    }

    private static string BrandHeader() =>
        "<tr><td bgcolor=\"#0b2d6b\" style=\"padding:24px 40px;border-radius:14px 14px 0 0;border-bottom:4px solid #22d3ee;\">" +
        "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>" +
        "<td style=\"color:#ffffff;font-size:25px;line-height:30px;font-weight:700;letter-spacing:-0.4px;\">Spend<span style=\"color:#22d3ee;\">Wise</span></td>" +
        "<td align=\"right\" style=\"color:#bfdbfe;font-size:10px;line-height:14px;font-weight:700;letter-spacing:1.2px;\">FINANCE PLATFORM</td>" +
        "</tr></table></td></tr>";

    private static string Greeting(string userName) =>
        $"<p style=\"margin:0 0 16px;color:#334155;font-size:16px;line-height:25px;\">Xin chào <strong style=\"color:#0f172a;\">{Encode(userName)}</strong>,</p>";

    private static string Intro(string text) =>
        $"<p style=\"margin:0 0 22px;color:#334155;font-size:16px;line-height:25px;\">{Multiline(text)}</p>";

    private static string OtpCard(string label, string value) =>
        "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 22px;\">" +
        "<tr><td align=\"center\" bgcolor=\"#eff6ff\" style=\"padding:22px 16px;border:1px solid #bfdbfe;border-radius:10px;\">" +
        $"<p style=\"margin:0 0 9px;color:#475569;font-size:12px;line-height:18px;font-weight:700;letter-spacing:0.8px;text-transform:uppercase;\">{Encode(label)}</p>" +
        $"<p style=\"margin:0;color:#0b2d6b;font-family:'Courier New',monospace;font-size:32px;line-height:40px;font-weight:700;letter-spacing:5px;word-break:break-all;\">{Encode(value)}</p>" +
        "</td></tr></table>";

    private static string DetailCard(string rows) =>
        "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 22px;border:1px solid #e2e8f0;border-radius:10px;\">" +
        rows + "</table>";

    private static string DetailRow(string label, string value, bool multiline = false) =>
        "<tr><td width=\"36%\" valign=\"top\" style=\"padding:11px 14px;border-bottom:1px solid #e2e8f0;color:#64748b;font-size:13px;line-height:20px;\">" +
        Encode(label) +
        "</td><td valign=\"top\" style=\"padding:11px 14px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:14px;line-height:21px;font-weight:600;\">" +
        (multiline ? Multiline(value) : Encode(value)) + "</td></tr>";

    private static string StatusBadge(string status, string color, string background) =>
        $"<p style=\"margin:0 0 18px;\"><span style=\"display:inline-block;padding:6px 11px;border-radius:999px;background:{background};color:{color};font-size:12px;line-height:16px;font-weight:700;\">{Encode(status)}</span></p>";

    private static string SecurityNote(string text) =>
        Note("Bảo mật", text, "#fff7ed", "#fdba74", "#9a3412");

    private static string InformationNote(string title, string text) =>
        Note(title, text, "#ecfeff", "#67e8f9", "#155e75");

    private static string SuccessNote(string title, string text) =>
        Note(title, text, "#f0fdf4", "#86efac", "#166534");

    private static string Note(string title, string text, string background, string border, string color) =>
        $"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 22px;\"><tr><td bgcolor=\"{background}\" style=\"padding:15px 16px;border-left:4px solid {border};border-radius:8px;color:{color};font-size:14px;line-height:22px;\"><strong>{Encode(title)}:</strong> {Multiline(text)}</td></tr></table>";

    private static string MutedNote(string text) =>
        $"<p style=\"margin:0 0 22px;color:#64748b;font-size:13px;line-height:21px;\">{Multiline(text)}</p>";

    private static string ActionButton(string actionUrl, string actionText) =>
        "<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:4px 0 0;\"><tr>" +
        $"<td bgcolor=\"#0759c7\" style=\"border-radius:8px;\"><a href=\"{Encode(actionUrl)}\" style=\"display:inline-block;padding:13px 20px;color:#ffffff;text-decoration:none;font-size:14px;line-height:20px;font-weight:700;\">{Encode(actionText)}</a></td>" +
        "</tr></table>";

    private static string ValidActionUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            throw new InvalidOperationException("Email action URL must use HTTPS (or loopback HTTP in Development).");
        }
        return uri.AbsoluteUri;
    }

    private static string Multiline(string value) =>
        Encode(value).Replace("\r\n", "<br>").Replace("\n", "<br>");

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Value(IReadOnlyDictionary<string, string?> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}
