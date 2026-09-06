import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ command }) => {
  // The React Compiler memoises components and hooks at build time, which is what keeps twenty
  // unchanged result cards from re-rendering on every keystroke. The repo already lints with
  // eslint-plugin-react-hooks v7, whose rules exist to keep code compiler-safe; running those
  // rules with the compiler switched off would be half the deal.
  const plugins = [react({ compiler: true })];

  // Only a local server proxies. A production build is served by nginx, which does the same
  // forwarding, so `vite build` must not need the variable at all.
  if (command !== 'serve') {
    return { plugins };
  }

  // Read against this file, not the cwd, so `vite` started from the repo root still finds it --
  // and always from .env.development, which is where a local target belongs whatever mode the
  // local server runs in (`preview` runs a server in production mode).
  const target = loadEnv('development', import.meta.dirname, 'VITE_').VITE_API_PROXY_TARGET;

  // No default: an unset target should fail here rather than proxy nowhere and surface as an
  // empty result list.
  if (!target) {
    throw new Error('VITE_API_PROXY_TARGET is not set.');
  }

  // The app always calls /api on its own origin -- locally through this proxy, in Compose
  // through nginx. One code path, and no CORS preflight in either.
  const proxy = { '/api': { target, changeOrigin: true } };

  return { plugins, server: { port: 5173, proxy }, preview: { port: 4173, proxy } };
});
