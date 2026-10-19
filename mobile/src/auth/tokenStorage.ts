export type StoredTokens = { accessToken: string | null; refreshToken: string | null; deviceTrustToken: string | null };

let memoryTokens: StoredTokens = { accessToken: null, refreshToken: null, deviceTrustToken: null };

// TypeScript fallback; Metro selects tokenStorage.native.ts or tokenStorage.web.ts at runtime.
export const tokenStorage = {
  async read(): Promise<StoredTokens> { return { ...memoryTokens }; },
  async save(tokens: StoredTokens) { memoryTokens = { ...tokens }; },
  async clear() { memoryTokens = { accessToken: null, refreshToken: null, deviceTrustToken: null }; },
};
