/// <reference path="../.astro/types.d.ts" />

interface ImportMetaEnv {
  readonly API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
