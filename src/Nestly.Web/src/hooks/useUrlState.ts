import { useCallback, useMemo, useSyncExternalStore } from 'react';

const NAVIGATED = 'nestly:navigated';

function subscribe(onChange: () => void): () => void {
  // popstate covers the back button; pushState does not raise it, so writes announce themselves.
  window.addEventListener('popstate', onChange);
  window.addEventListener(NAVIGATED, onChange);

  return () => {
    window.removeEventListener('popstate', onChange);
    window.removeEventListener(NAVIGATED, onChange);
  };
}

function getSnapshot(): string {
  return window.location.search;
}

// A string, not a parsed object: getSnapshot must return the same value until something actually
// changes, and a fresh object every call is the classic way to loop this hook forever.
function getServerSnapshot(): string {
  return '';
}

export type WriteMode = 'push' | 'replace';

/**
 * The query string as state. Every search parameter lives in the URL, so a search is a link and
 * the back button walks it.
 */
// A router would bring this hook and nothing else -- the app has one route.
export function useUrlState(): [URLSearchParams, (next: URLSearchParams, mode: WriteMode) => void] {
  const search = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  // Stable while the URL is: a caller memoising on these params should get a hit, not a miss.
  const params = useMemo(() => new URLSearchParams(search), [search]);

  const write = useCallback((next: URLSearchParams, mode: WriteMode) => {
    const query = next.toString();
    const url = query ? `${window.location.pathname}?${query}` : window.location.pathname;

    if (url === window.location.pathname + window.location.search) {
      return;
    }

    try {
      // Typing replaces, choosing pushes: a settled query per keystroke would otherwise bury the
      // previous page under a stack of near-identical entries.
      if (mode === 'push') {
        window.history.pushState(null, '', url);
      } else {
        window.history.replaceState(null, '', url);
      }
    } catch {
      // Safari throws above roughly 100 history writes in 30 seconds. The input is controlled by
      // the URL, so an escaping error would freeze typing outright; dropping one URL update is
      // the far cheaper failure.
      return;
    }

    window.dispatchEvent(new Event(NAVIGATED));
  }, []);

  return [params, write];
}
