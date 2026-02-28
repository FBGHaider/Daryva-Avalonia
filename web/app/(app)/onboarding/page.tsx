"use client";

import { useAuth } from "@clerk/nextjs";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { createInvite, createOrg, getMe, updateMe } from "@/lib/api";
import { useMe } from "@/lib/useMe";
import { cn } from "@/lib/utils";

type Step = "org" | "profile" | "invite";

export default function OnboardingPage() {
  const { isSignedIn, isLoaded: authLoaded, getToken } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { me, loading, refetch } = useMe();
  const [step, setStep] = useState<Step>("org");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastInviteLink, setLastInviteLink] = useState<string | null>(null);

  // Sync step from me or query
  useEffect(() => {
    if (!authLoaded) return;
    if (!isSignedIn) {
      router.replace("/sign-in");
      return;
    }
    const q = searchParams.get("step") as Step | null;
    if (q && ["org", "profile", "invite"].includes(q)) {
      setStep(q);
      return;
    }
    if (loading || !me) return;
    if (me.requiresOrgSetup) setStep("org");
    else if (me.requiresProfileSetup) setStep("profile");
    else setStep("invite");
  }, [authLoaded, isSignedIn, loading, me, router, searchParams]);

  useEffect(() => {
    if (authLoaded && !isSignedIn) return;
    if (!me || loading) return;
    if (!me.requiresOrgSetup && !me.requiresProfileSetup) {
      router.replace("/app");
    }
  }, [authLoaded, isSignedIn, me, loading, router]);

  if (!authLoaded || (isSignedIn && loading && !me)) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-[var(--text-muted)]">Loading…</p>
      </div>
    );
  }

  const handleOrgSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    const name = (e.currentTarget.elements.namedItem("orgName") as HTMLInputElement)?.value?.trim();
    if (!name) {
      setError("Organisation name is required.");
      return;
    }
    setBusy(true);
    try {
      const token = await getToken();
      await createOrg(token ?? null, { name });
      await refetch();
      setStep("profile");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create organisation.");
    } finally {
      setBusy(false);
    }
  };

  const handleProfileSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    const form = e.currentTarget;
    const displayName = (form.elements.namedItem("displayName") as HTMLInputElement)?.value?.trim() || null;
    const phone = (form.elements.namedItem("phone") as HTMLInputElement)?.value?.trim() || null;
    const timeZoneId = (form.elements.namedItem("timeZoneId") as HTMLInputElement)?.value?.trim() || null;
    setBusy(true);
    try {
      const token = await getToken();
      await updateMe(token ?? null, { displayName, phone, timeZoneId });
      await refetch();
      router.push("/app");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update profile.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md px-4 py-12">
      <h1 className="mb-8 font-heading text-2xl font-semibold">Get started</h1>
      <div className="mb-6 flex gap-2">
        {(["org", "profile", "invite"] as Step[]).map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => setStep(s)}
            className={cn(
              "rounded px-3 py-1.5 text-sm font-medium",
              step === s
                ? "bg-[var(--accent)] text-white"
                : "bg-[var(--border)] text-[var(--text-muted)]"
            )}
          >
            {s === "org" ? "Organisation" : s === "profile" ? "Profile" : "Invite"}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4 rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          {error}
        </div>
      )}

      {step === "org" && (
        <form onSubmit={handleOrgSubmit} className="space-y-4">
          <div>
            <label htmlFor="orgName" className="mb-1 block text-sm font-medium">
              Organisation name
            </label>
            <input
              id="orgName"
              name="orgName"
              type="text"
              required
              maxLength={256}
              className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
              placeholder="e.g. Acme Properties"
            />
          </div>
          <button
            type="submit"
            disabled={busy}
            className="w-full rounded bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-60"
          >
            {busy ? "Creating…" : "Continue"}
          </button>
        </form>
      )}

      {step === "profile" && (
        <form onSubmit={handleProfileSubmit} className="space-y-4">
          <div>
            <label htmlFor="displayName" className="mb-1 block text-sm font-medium">
              Display name
            </label>
            <input
              id="displayName"
              name="displayName"
              type="text"
              maxLength={256}
              defaultValue={me?.user.displayName ?? undefined}
              className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
              placeholder="Your name"
            />
          </div>
          <div>
            <label htmlFor="phone" className="mb-1 block text-sm font-medium">
              Phone (optional)
            </label>
            <input
              id="phone"
              name="phone"
              type="tel"
              maxLength={50}
              defaultValue={me?.user.phone ?? undefined}
              className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
              placeholder="+44 …"
            />
          </div>
          <div>
            <label htmlFor="timeZoneId" className="mb-1 block text-sm font-medium">
              Time zone (optional)
            </label>
            <input
              id="timeZoneId"
              name="timeZoneId"
              type="text"
              maxLength={128}
              defaultValue={me?.user.timeZoneId ?? undefined}
              className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
              placeholder="e.g. GMT Standard Time"
            />
          </div>
          <button
            type="submit"
            disabled={busy}
            className="w-full rounded bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-60"
          >
            {busy ? "Saving…" : "Finish"}
          </button>
        </form>
      )}

      {step === "invite" && (
        <div className="space-y-4">
          <p className="text-sm text-[var(--text-muted)]">
            Invite members (optional). Send the link below or skip and do it later from the app.
          </p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              setError(null);
              const form = e.currentTarget;
              const email = (form.elements.namedItem("inviteEmail") as HTMLInputElement)?.value?.trim() || undefined;
              const role = (form.elements.namedItem("inviteRole") as HTMLSelectElement)?.value || "Member";
              const orgId = me?.organisations?.[0]?.id;
              if (!orgId) {
                setError("No organisation found.");
                return;
              }
              setBusy(true);
              try {
                const token = await getToken();
                const res = await createInvite(token ?? null, orgId, {
                  email: email || null,
                  role,
                  expiresInDays: 7,
                });
                const base = typeof window !== "undefined" ? window.location.origin : "";
                setLastInviteLink(`${base}/join?token=${encodeURIComponent(res.token)}`);
              } catch (err) {
                setError(err instanceof Error ? err.message : "Failed to create invite.");
              } finally {
                setBusy(false);
              }
            }}
            className="space-y-4"
          >
            <div>
              <label htmlFor="inviteEmail" className="mb-1 block text-sm font-medium">
                Email (optional)
              </label>
              <input
                id="inviteEmail"
                name="inviteEmail"
                type="email"
                className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
                placeholder="colleague@example.com"
              />
            </div>
            <div>
              <label htmlFor="inviteRole" className="mb-1 block text-sm font-medium">
                Role
              </label>
              <select
                id="inviteRole"
                name="inviteRole"
                className="w-full rounded border border-[var(--border)] bg-white px-3 py-2 text-[var(--text-primary)]"
              >
                <option value="Member">Member</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <button
              type="submit"
              disabled={busy}
              className="w-full rounded bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-60"
            >
              {busy ? "Creating…" : "Create invite link"}
            </button>
          </form>
          {lastInviteLink && (
            <div className="rounded border border-[var(--border)] bg-[var(--card)] p-3">
              <p className="mb-2 text-xs font-medium text-[var(--text-muted)]">Invite link (copy and share)</p>
              <div className="flex gap-2">
                <input
                  type="text"
                  readOnly
                  value={lastInviteLink}
                  className="flex-1 rounded border border-[var(--border)] bg-[var(--background)] px-2 py-1.5 text-sm"
                />
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard?.writeText(lastInviteLink);
                  }}
                  className="rounded border border-[var(--border)] px-3 py-1.5 text-sm font-medium"
                >
                  Copy
                </button>
              </div>
            </div>
          )}
          <button
            type="button"
            onClick={() => router.push("/app")}
            className="w-full rounded border border-[var(--border)] px-4 py-2 font-medium text-[var(--text-primary)]"
          >
            Go to app
          </button>
        </div>
      )}
    </div>
  );
}
