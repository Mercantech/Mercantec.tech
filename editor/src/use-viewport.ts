import { useEffect, useRef, useState } from 'react';
import { observeViewport } from './viewport-loading.mjs';

export function useViewport<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [active, setActive] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (element) return observeViewport(element, setActive);
  }, []);
  return [ref, active] as const;
}
