/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Absolute API origin. Empty in dev and in Compose, where /api is proxied to the same origin. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
