/**
 * Analytics hook placeholders.
 * TODO: Replace with your provider (e.g. Vercel Analytics, Google Analytics, Plausible).
 */

export function trackPageView(_path: string) {
  if (typeof window !== "undefined") {
    // window.gtag?.("event", "page_view", { page_path: path });
  }
}

export function trackEvent(_name: string, _params?: Record<string, unknown>) {
  if (typeof window !== "undefined") {
    // window.gtag?.("event", name, params);
  }
}
