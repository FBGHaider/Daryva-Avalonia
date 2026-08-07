import type { AstroCookies } from 'astro';

// Server-side only (no PUBLIC_ prefix) - never sent to the client bundle, since every
// API call the portal makes happens in SSR code, not browser JS. In production this is
// set to Docker Compose's internal service DNS (http://api:8080); the localhost fallback
// is for `astro dev` against a locally-run Daryva.Api.
const API_BASE_URL = import.meta.env.API_BASE_URL || 'http://localhost:5000';

const ACCESS_TOKEN_COOKIE = 'daryva_access_token';
const REFRESH_TOKEN_COOKIE = 'daryva_refresh_token';
const EXPIRES_AT_COOKIE = 'daryva_expires_at';

// httpOnly so client JS (and any XSS) can never read these; secure only in production
// since local dev runs over plain http. Never sent to api.daryva.com - scoped to this
// site only, and the portal always attaches the Bearer header itself.
const COOKIE_OPTIONS = {
  httpOnly: true,
  secure: import.meta.env.PROD,
  sameSite: 'lax' as const,
  path: '/',
};

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

export function setAuthCookies(cookies: AstroCookies, tokens: AuthTokens): void {
  cookies.set(ACCESS_TOKEN_COOKIE, tokens.accessToken, COOKIE_OPTIONS);
  cookies.set(REFRESH_TOKEN_COOKIE, tokens.refreshToken, COOKIE_OPTIONS);
  cookies.set(EXPIRES_AT_COOKIE, tokens.accessTokenExpiresAt, COOKIE_OPTIONS);
}

export function clearAuthCookies(cookies: AstroCookies): void {
  cookies.delete(ACCESS_TOKEN_COOKIE, { path: '/' });
  cookies.delete(REFRESH_TOKEN_COOKIE, { path: '/' });
  cookies.delete(EXPIRES_AT_COOKIE, { path: '/' });
}

export function getRefreshToken(cookies: AstroCookies): string | undefined {
  return cookies.get(REFRESH_TOKEN_COOKIE)?.value;
}

const REFRESH_BUFFER_MS = 60_000;

/**
 * Returns a valid access token, transparently refreshing it first if it's expired or
 * expiring within the next minute. Returns null (caller should redirect to /login) if
 * there's no session at all, or the refresh token itself is no longer valid.
 */
export async function getValidAccessToken(cookies: AstroCookies): Promise<string | null> {
  const accessToken = cookies.get(ACCESS_TOKEN_COOKIE)?.value;
  const refreshToken = cookies.get(REFRESH_TOKEN_COOKIE)?.value;
  const expiresAt = cookies.get(EXPIRES_AT_COOKIE)?.value;

  if (!accessToken || !refreshToken) return null;

  const expiresAtMs = expiresAt ? Date.parse(expiresAt) : NaN;
  if (Number.isFinite(expiresAtMs) && expiresAtMs - REFRESH_BUFFER_MS > Date.now()) {
    return accessToken;
  }

  const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) {
    clearAuthCookies(cookies);
    return null;
  }

  const auth = (await response.json()) as {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
  };
  setAuthCookies(cookies, auth);

  return auth.accessToken;
}

/**
 * Some Daryva.Api endpoints return a bare primitive (ActionResult<decimal>/<string>),
 * e.g. GET /api/payments/status/rent/{id}. ASP.NET Core's content negotiation picks a
 * text/plain formatter for bare strings when no `Accept: application/json` header is
 * sent, so the body can be an unquoted raw string ("Unpaid") rather than valid JSON
 * ("\"Unpaid\""). This tolerates both: parse as JSON if possible, otherwise use the raw
 * text as-is (which already is the value we want in the unquoted case).
 */
export async function readJsonLoose<T = unknown>(response: Response): Promise<T> {
  const text = await response.text();
  try {
    return JSON.parse(text) as T;
  } catch {
    return text as T;
  }
}

/**
 * Authenticated fetch against Daryva.Api. Returns null if there's no valid session -
 * caller should redirect to /login rather than treat this as an API error.
 */
export async function apiFetch(
  cookies: AstroCookies,
  path: string,
  init: RequestInit = {},
): Promise<Response | null> {
  const accessToken = await getValidAccessToken(cookies);
  if (!accessToken) return null;

  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${accessToken}`);

  return fetch(`${API_BASE_URL}${path}`, { ...init, headers });
}
