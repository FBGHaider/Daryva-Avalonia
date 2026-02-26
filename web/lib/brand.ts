/**
 * Brand asset paths and metadata.
 *
 * TODO — Image exports needed (replace placeholders in /public/brand/):
 * - logo-icon.png: Icon only, transparent background (e.g. 64x64 or 128x128).
 * - favicon.ico: 32x32 (and optionally 16x16). Can copy from Daryva_icon.ico if already multi-size.
 * - apple-touch-icon.png: 180x180, no transparency for iOS.
 * - og-image.png: 1200x630, logo + tagline on neutral background (#F7F8FA), for Open Graph / Twitter cards.
 *
 * To swap the logo later: replace files in /public/brand/ and ensure logo-lockup.svg
 * (or the header component’s icon + wordmark) points to your updated icon asset.
 */
import { site } from "./site";

const brandBase = "/brand";

export const brand = {
  /** Source icon (copied from Daryva.UI assets). Use for favicon or header when logo-icon.png not yet available. */
  iconSource: `${brandBase}/Daryva_icon.ico`,
  /** Icon only, transparent PNG. Prefer for header/lockup when available. */
  logoIcon: `${brandBase}/logo-icon.png`,
  /** SVG lockup: icon + "Daryva" wordmark (fallback). */
  logoLockupSvg: `${brandBase}/logo-lockup.svg`,
  /** PNG lockup: use this for header/footer so the logo displays reliably. */
  logoLockup: `${brandBase}/logo-lockup.png`,
  /** Favicon for browser tab. */
  favicon: `${brandBase}/favicon.ico`,
  /** Apple touch icon, 180x180. */
  appleTouchIcon: `${brandBase}/apple-touch-icon.png`,
  /** Open Graph image, 1200x630. */
  ogImage: `${brandBase}/og-image.png`,
} as const;

export const defaultTitle = `${site.name} — Modern property management`;
export const titleTemplate = `%s | ${site.name}`;
