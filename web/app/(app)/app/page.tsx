"use client";

import { useAuth } from "@clerk/nextjs";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useMe } from "@/lib/useMe";

export default function AppShellPage() {
  const { isSignedIn, isLoaded: authLoaded } = useAuth();
  const router = useRouter();
  const { me, loading } = useMe();

  useEffect(() => {
    if (!authLoaded) return;
    if (!isSignedIn) {
      router.replace("/sign-in");
      return;
    }
    if (loading || !me) return;
    if (me.requiresOrgSetup || me.requiresProfileSetup) {
      router.replace("/onboarding");
      return;
    }
  }, [authLoaded, isSignedIn, loading, me, router]);

  if (!authLoaded || !isSignedIn) {
    return null;
  }
  if (loading || !me) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-[var(--text-muted)]">Loading…</p>
      </div>
    );
  }
  if (me.requiresOrgSetup || me.requiresProfileSetup) {
    return null;
  }

  return (
    <div className="min-h-screen p-6">
      <header className="mb-6 border-b border-[var(--border)] pb-4">
        <h1 className="font-heading text-xl font-semibold">Daryva</h1>
        <p className="mt-1 text-sm text-[var(--text-muted)]">
          {me.user.displayName || me.user.email} · {me.organisations.length} organisation(s)
        </p>
      </header>
      <main>
        <p className="text-[var(--text-muted)]">App shell. Organisations and dashboard coming next.</p>
      </main>
    </div>
  );
}
