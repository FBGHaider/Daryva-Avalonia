"use client";

import { useAuth } from "@clerk/nextjs";
import { useEffect, useState } from "react";
import { getMe, type MeResponseDto } from "./api";

export function useMe(): {
  me: MeResponseDto | null;
  loading: boolean;
  error: Error | null;
  refetch: () => Promise<void>;
} {
  const { getToken, isLoaded: clerkLoaded, isSignedIn } = useAuth();
  const [me, setMe] = useState<MeResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const fetchMe = async () => {
    if (!clerkLoaded) return;
    if (!isSignedIn) {
      setMe(null);
      setLoading(false);
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const token = await getToken();
      const data = await getMe(token);
      setMe(data);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
      setMe(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchMe();
  }, [clerkLoaded, isSignedIn]);

  return { me, loading, error, refetch: fetchMe };
}
