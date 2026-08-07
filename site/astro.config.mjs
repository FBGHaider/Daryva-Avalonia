import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

export default defineConfig({
  site: 'https://daryva.com',
  compressHTML: true,
  integrations: [sitemap()],
});
