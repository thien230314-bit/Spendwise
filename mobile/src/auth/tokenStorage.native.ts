import * as SecureStore from 'expo-secure-store';

const keys = {
  accessToken: 'spendwise.accessToken',
  refreshToken: 'spendwise.refreshToken',
  deviceTrustToken: 'spendwise.deviceTrustToken',
} as const;

export type StoredTokens = { accessToken: string | null; refreshToken: string | null; deviceTrustToken: string | null };

export const tokenStorage = {
  async read(): Promise<StoredTokens> {
    const [accessToken, refreshToken, deviceTrustToken] = await Promise.all([
      SecureStore.getItemAsync(keys.accessToken),
      SecureStore.getItemAsync(keys.refreshToken),
      SecureStore.getItemAsync(keys.deviceTrustToken),
    ]);
    return { accessToken, refreshToken, deviceTrustToken };
  },
  async save(tokens: StoredTokens) {
    await Promise.all([
      saveOrDelete(keys.accessToken, tokens.accessToken),
      saveOrDelete(keys.refreshToken, tokens.refreshToken),
      saveOrDelete(keys.deviceTrustToken, tokens.deviceTrustToken),
    ]);
  },
  async clear() { await Promise.all(Object.values(keys).map((key) => SecureStore.deleteItemAsync(key))); },
};

function saveOrDelete(key: string, value: string | null) {
  return value ? SecureStore.setItemAsync(key, value) : SecureStore.deleteItemAsync(key);
}
