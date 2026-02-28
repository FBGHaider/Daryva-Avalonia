/**
 * API client for api.daryva.com (or NEXT_PUBLIC_API_URL).
 * All calls require a JWT (e.g. from Clerk getToken()).
 */

const getBaseUrl = () =>
  typeof window !== "undefined"
    ? (process.env.NEXT_PUBLIC_API_URL || "").replace(/\/$/, "") || "http://localhost:5000"
    : "";

// --- Types (match API DTOs) ---

export interface MeUserDto {
  id: string;
  email: string;
  displayName: string | null;
  phone: string | null;
  timeZoneId: string | null;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface OrganizationResponse {
  id: string;
  name: string;
  createdAt: string;
  currentUserRole: string | null;
}

export interface MeResponseDto {
  user: MeUserDto;
  organisations: OrganizationResponse[];
  requiresOrgSetup: boolean;
  requiresProfileSetup: boolean;
}

export interface UpdateMeRequest {
  displayName?: string | null;
  phone?: string | null;
  timeZoneId?: string | null;
}

export interface CreateOrganizationRequest {
  name: string;
}

// --- Invites (POST /api/orgs/{orgId}/invites, POST /api/orgs/join/invite) ---

export interface CreateOrgInviteRequest {
  email?: string | null;
  role?: string;
  expiresInDays?: number;
}

export interface CreateOrgInviteResponse {
  inviteId: string;
  organizationId: string;
  token: string;
  email: string | null;
  role: string;
  expiresAt: string;
}

export interface AcceptOrgInviteRequest {
  token: string;
}

export interface JoinOrganizationResponse {
  organizationId: string;
  organizationName: string;
  role: string;
  alreadyMember: boolean;
}

// --- Fetch helper ---

export async function apiFetch<T>(
  token: string | null,
  path: string,
  options: RequestInit & { method?: string; body?: unknown } = {}
): Promise<T> {
  const base = getBaseUrl();
  const url = path.startsWith("http") ? path : `${base}${path.startsWith("/") ? "" : "/"}${path}`;
  const { body, ...rest } = options;
  const headers: HeadersInit = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  const res = await fetch(url, {
    ...rest,
    headers,
    body: body !== undefined ? JSON.stringify(body) : options.body,
  });
  if (!res.ok) {
    const text = await res.text();
    let err: Error & { status?: number } = new Error(text || `HTTP ${res.status}`);
    (err as Error & { status: number }).status = res.status;
    throw err;
  }
  if (res.status === 204 || res.headers.get("content-length") === "0") return undefined as T;
  return res.json() as Promise<T>;
}

// --- /api/me ---

export async function getMe(token: string | null): Promise<MeResponseDto> {
  return apiFetch<MeResponseDto>(token, "/api/me");
}

export async function updateMe(
  token: string | null,
  body: UpdateMeRequest
): Promise<MeUserDto> {
  return apiFetch<MeUserDto>(token, "/api/me", { method: "PUT", body });
}

// --- /api/orgs ---

export async function getOrgs(token: string | null): Promise<OrganizationResponse[]> {
  const data = await apiFetch<OrganizationResponse[]>(token, "/api/orgs");
  return Array.isArray(data) ? data : [];
}

export async function createOrg(
  token: string | null,
  req: CreateOrganizationRequest
): Promise<OrganizationResponse> {
  return apiFetch<OrganizationResponse>(token, "/api/orgs", { method: "POST", body: req });
}

export async function createInvite(
  token: string | null,
  orgId: string,
  req: CreateOrgInviteRequest
): Promise<CreateOrgInviteResponse> {
  return apiFetch<CreateOrgInviteResponse>(token, `/api/orgs/${orgId}/invites`, {
    method: "POST",
    body: req,
    headers: { "X-Org-Id": orgId },
  });
}

export async function acceptInvite(
  token: string | null,
  req: AcceptOrgInviteRequest
): Promise<JoinOrganizationResponse> {
  return apiFetch<JoinOrganizationResponse>(token, "/api/orgs/join/invite", {
    method: "POST",
    body: req,
  });
}
