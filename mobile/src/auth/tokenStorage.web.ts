export type StoredTokens = { accessToken: string | null; refreshToken: string | null; deviceTrustToken: string | null };

const emptyTokens = (): StoredTokens => ({ accessToken: null, refreshToken: null, deviceTrustToken: null });
let memoryTokens = emptyTokens();

// Browser preview intentionally does not persist credentials. Native targets use SecureStore.
export const tokenStorage = {
  async read(): Promise<StoredTokens> { return { ...memoryTokens }; },
  async save(tokens: StoredTokens) { memoryTokens = { ...tokens }; },
  async clear() { memoryTokens = emptyTokens(); },
};
