namespace SpendWise.Domain.Common;

// BaseEntity cung cấp Id cùng thời điểm tạo/cập nhật chung cho các entity.

public abstract class BaseEntity
{
    // Id được tạo ở ứng dụng để các entity liên quan có thể tham chiếu trước khi SaveChanges.
    public Guid Id { get; set; } = Guid.NewGuid();
    // CreatedAt và UpdatedAt được DbContext cập nhật tự động theo UTC.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class Money
{
    // Hệ thống hiện chỉ posting bằng VND với độ chính xác thống nhất.
    public const string DefaultCurrency = "VND";
    public const int Precision = 18;
    public const int Scale = 2;
}
