// Local state for the standalone design reference. No account or server required.
(() => {
  'use strict';
  const key = 'lup-local-state-v1';
  let restored = null;
  try {
    restored = JSON.parse(localStorage.getItem(key) || 'null');
  } catch {
    // A blocked or cleared storage area must not prevent opening LUP.
  }

  window.lupState = {
    widgetState: restored,
    async setWidgetState(snapshot) {
      this.widgetState = snapshot;
      try {
        localStorage.setItem(key, JSON.stringify(snapshot));
      } catch {
        // Continue with in-memory state if storage is unavailable or full.
      }
    }
  };
})();
