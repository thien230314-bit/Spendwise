import { apiRequest } from './client';
import type { UserProfile, EmailVerificationPendingResponse } from '@/types/auth';

export const userApi = {
  getProfile: () => apiRequest<UserProfile>('/api/users/me', { authenticated: true }),

  uploadAvatar: async (file: { uri: string; name: string; type: string } | Blob | File) => {
    const formData = new FormData();
    if ('uri' in file) {
      formData.append('file', {
        uri: file.uri,
        name: file.name,
        type: file.type,
      } as any);
    } else {
      formData.append('file', file);
    }
    return apiRequest<UserProfile>('/api/users/me/avatar', {
      method: 'POST',
      body: formData,
      authenticated: true,
    });
  },

  requestEmailChange: (newEmail: string) =>
    apiRequest<EmailVerificationPendingResponse>('/api/users/me/email/request', {
      method: 'POST',
      body: JSON.stringify({ newEmail }),
      authenticated: true,
    }),

  confirmEmailChange: (otp: string) =>
    apiRequest<UserProfile>('/api/users/me/email/confirm', {
      method: 'POST',
      body: JSON.stringify({ otp }),
      authenticated: true,
    }),
};
