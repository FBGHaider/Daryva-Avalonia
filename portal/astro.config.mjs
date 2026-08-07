import { defineConfig } from 'astro/config';
import node from '@astrojs/node';

export default defineConfig({
  site: 'https://portal.daryva.com',
  output: 'server',
  adapter: node({
    mode: 'standalone',
  }),
  security: {
    // Nginx terminates TLS and proxies plain HTTP to this Node process, so without this,
    // Astro's CSRF Origin check sees the browser's real https://portal.daryva.com Origin
    // header against what it thinks is an http:// request and rejects every form POST as
    // cross-site. This tells Astro to trust the X-Forwarded-* headers when they resolve to
    // our actual production domain - narrower than disabling checkOrigin outright.
    allowedDomains: [{ hostname: 'portal.daryva.com', protocol: 'https' }],
  },
});
