import type { AstroCookies } from 'astro';

// Server-side only, and deliberately process.env not import.meta.env: Vite statically
// inlines import.meta.env.X at build time, so in a Docker deployment where the build
// stage never has API_BASE_URL set, "import.meta.env.API_BASE_URL || 'default'" gets
// baked in as the literal default string, permanently ignoring whatever the container
// is actually given at runtime via docker-compose. process.env stays a genuine runtime
// lookup. In production this is Docker Compose's internal service DNS (http://api:8080);
// the localhost fallback is for `astro dev` against a locally-run Daryva.Api.
export const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:5000';

const ACCESS_TOKEN_COOKIE = 'daryva_access_token';
const REFRESH_TOKEN_COOKIE = 'daryva_refresh_token';
const EXPIRES_AT_COOKIE = 'daryva_expires_at';
const ORG_ID_COOKIE = 'daryva_org_id';

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
  cookies.delete(ORG_ID_COOKIE, { path: '/' });
}

/**
 * Which org apiFetch should send as X-Org-Id. Needed because a tenant portal login can also
 * belong to other orgs (e.g. as a landlord elsewhere, or a tenant of more than one landlord) --
 * without this, Daryva.Api's TenantContextMiddleware can't auto-select a single org and 400s.
 * Set once from GET /api/me/tenant-access's TenantOrgId right after login/accept-invite.
 */
export function setOrgIdCookie(cookies: AstroCookies, orgId: string): void {
  cookies.set(ORG_ID_COOKIE, orgId, COOKIE_OPTIONS);
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
 * Confirms a just-issued access token actually has a Tenant identity somewhere, not just any
 * valid login -- and which org it's in. Daryva.Api's shared endpoints (e.g. /api/tenancies)
 * intentionally return org-wide data to a Landlord caller -- that's correct for the desktop
 * app, but the portal must not render that response as if it were the caller's own personal
 * tenancy. Call this once right after login/accept-invite, before granting a session, rather
 * than on every page load.
 */
export async function checkTenantAccess(
  accessToken: string,
): Promise<{ isTenant: boolean; tenantOrgId: string | null }> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/me/tenant-access`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) return { isTenant: false, tenantOrgId: null };
    const result = (await response.json()) as { isTenant: boolean; tenantOrgId: string | null };
    return { isTenant: result.isTenant === true, tenantOrgId: result.tenantOrgId ?? null };
  } catch {
    return { isTenant: false, tenantOrgId: null };
  }
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

  const orgId = cookies.get(ORG_ID_COOKIE)?.value;
  if (orgId) headers.set('X-Org-Id', orgId);

  return fetch(`${API_BASE_URL}${path}`, { ...init, headers });
}
