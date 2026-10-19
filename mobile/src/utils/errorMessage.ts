import { ApiError } from '@/api/ApiError';

export function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.kind === 'unauthorized') return 'Thông tin đăng nhập hoặc phiên xác thực không hợp lệ.';
    if (error.kind === 'forbidden') return 'Tài khoản không có quyền thực hiện thao tác này.';
    if (error.kind === 'validation' && error.validationErrors) return Object.values(error.validationErrors).flat()[0] ?? error.message;
    return error.message;
  }
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi. Vui lòng thử lại.';
}
