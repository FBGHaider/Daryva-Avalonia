import type { APIRoute } from 'astro';
import { clearAuthCookies, getRefreshToken } from '../lib/api';

const API_BASE_URL = import.meta.env.API_BASE_URL || 'http://localhost:5000';

export const POST: APIRoute = async ({ cookies, redirect }) => {
  const refreshToken = getRefreshToken(cookies);

  if (refreshToken) {
    try {
      await fetch(`${API_BASE_URL}/api/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      // Best-effort revocation - the cookies are cleared either way below, so the
      // portal session ends locally even if the API call itself fails.
    }
  }

  clearAuthCookies(cookies);
  return redirect('/login');
};
