/**
 * App shell layout: sign-in, sign-up, onboarding, /app.
 * No marketing header/footer.
 */
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-[var(--background)] text-[var(--text-primary)]">
      {children}
    </div>
  );
}
