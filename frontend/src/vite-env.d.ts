/// <reference types="vite/client" />

/**
 * Vite environment variable type declarations for SpendWise frontend.
 *
 * ALLOWED frontend env vars:
 *   - VITE_API_BASE_URL   — SpendWise backend base URL (non-secret, required)
 *   - VITE_API_TIMEOUT_MS — Optional request timeout override
 *
 * NEVER declare here:
 *   - AI provider keys (OpenAI, Gemini, Claude)
 *   - SePay API key / webhook secret
 *   - JWT signing secret
 *   - Database credentials
 *   - SMTP credentials
 *   - Any server-side secret
 */
interface ImportMetaEnv {
  /** SpendWise backend REST API base URL. No trailing slash. */
  readonly VITE_API_BASE_URL: string;

  /** Optional: request timeout override in milliseconds. Defaults to 30 000. */
  readonly VITE_API_TIMEOUT_MS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
