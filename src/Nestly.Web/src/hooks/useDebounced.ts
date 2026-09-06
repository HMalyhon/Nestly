import { useEffect, useState } from 'react';

/** The value as it was `delayMs` ago, once it stops changing. */
export function useDebounced<T>(value: T, delayMs: number): T {
  const [settled, setSettled] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => { setSettled(value); }, delayMs);

    return () => { clearTimeout(timer); };
  }, [value, delayMs]);

  return settled;
}
