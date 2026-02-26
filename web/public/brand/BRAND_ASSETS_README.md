# Brand assets

Place the following files in this folder for full branding:

| File | Size / format | Notes |
|------|----------------|--------|
| `logo-icon.png` | Icon only, transparent PNG (e.g. 64×64 or 128×128) | Used in header when available; lockup SVG can reference it |
| `logo-lockup.svg` | Icon + "Daryva" wordmark | Replace current placeholder with your designed lockup |
| `favicon.ico` | 32×32 (and optionally 16×16) | Currently a copy of `Daryva_icon.ico` |
| `apple-touch-icon.png` | 180×180 PNG | For iOS home screen; no transparency |
| `og-image.png` | 1200×630 PNG | Open Graph / Twitter: logo + tagline on neutral background (#F7F8FA) |

Metadata and layout already point to these paths. After adding or replacing files, no code changes are required unless you change filenames (then update `lib/brand.ts`).
