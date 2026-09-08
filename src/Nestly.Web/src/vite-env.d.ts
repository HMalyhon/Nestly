/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Absolute API origin. Empty in dev and in Compose, where /api is proxied to the same origin. */
  readonly VITE_API_BASE_URL?: string;

  /** Raster tile template, and the credit that must travel with it. Set in .env.development and
      .env.production, so both are always present in a build. */
  readonly VITE_MAP_TILE_URL: string;
  readonly VITE_MAP_TILE_ATTRIBUTION: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
