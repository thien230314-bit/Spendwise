namespace SpendWise.Application.Common.Exceptions;

// Các exception này chuẩn hóa mã lỗi và HTTP status trả về từ nghiệp vụ.

public class AppException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string code, string message) : AppException(code, message, 404);
public sealed class ForbiddenException(string code = "forbidden", string message = "You are not allowed to perform this operation.") : AppException(code, message, 403);
public sealed class ConflictException(string code, string message) : AppException(code, message, 409);
