const rawApiBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL?.trim() ?? '';

export const environment = {
  apiBaseUrl: rawApiBaseUrl.replace(/\/+$/, ''),
  isApiConfigured: rawApiBaseUrl.length > 0,
  requestTimeoutMs: 12_000,
} as const;
