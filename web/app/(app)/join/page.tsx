"use client";

import { useAuth } from "@clerk/nextjs";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { acceptInvite } from "@/lib/api";

export default function JoinPage() {
  const { isSignedIn, isLoaded: authLoaded, getToken } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!authLoaded) return;
    if (!isSignedIn) {
      const returnUrl = typeof window !== "undefined"
        ? `${window.location.pathname}${window.location.search}`
        : "/join";
      router.replace(`/sign-in?redirect_url=${encodeURIComponent(returnUrl)}`);
      return;
    }
    if (!token?.trim()) {
      setStatus("error");
      setMessage("Missing invite token.");
      return;
    }
    let cancelled = false;
    setStatus("loading");
    getToken()
      .then((jwt) => acceptInvite(jwt ?? null, { token: token.trim() }))
      .then((res) => {
        if (cancelled) return;
        setStatus("success");
        setMessage(`You've joined ${res.organizationName}.`);
        router.replace("/app");
      })
      .catch((err) => {
        if (cancelled) return;
        setStatus("error");
        setMessage(err instanceof Error ? err.message : "Invalid or expired invite.");
      });
    return () => {
      cancelled = true;
    };
  }, [authLoaded, isSignedIn, token, router, getToken]);

  if (!authLoaded) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-[var(--text-muted)]">Loading…</p>
      </div>
    );
  }
  if (!isSignedIn) return null;
  if (status === "loading") {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-[var(--text-muted)]">Joining organisation…</p>
      </div>
    );
  }
  if (status === "error") {
    return (
      <div className="mx-auto max-w-md px-4 py-12">
        <div className="rounded border border-red-200 bg-red-50 px-4 py-3 text-red-800">
          {message}
        </div>
        <button
          type="button"
          onClick={() => router.push("/app")}
          className="mt-4 rounded bg-[var(--accent)] px-4 py-2 font-medium text-white"
        >
          Go to app
        </button>
      </div>
    );
  }
  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-[var(--text-muted)]">{message ?? "Redirecting…"}</p>
    </div>
  );
}
