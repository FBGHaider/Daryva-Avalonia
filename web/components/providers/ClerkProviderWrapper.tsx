"use client";

import { ClerkProvider as Clerk } from "@clerk/nextjs";

export function ClerkProviderWrapper({ children }: { children: React.ReactNode }) {
  const key = process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY;
  if (!key) return <>{children}</>;
  return <Clerk publishableKey={key}>{children}</Clerk>;
}
