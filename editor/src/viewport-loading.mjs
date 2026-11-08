// One observer for the document; offscreen blocks and background tabs do no work.
const targets = new Map();
let observer;
let listening = false;

function publish(target) {
  const active = target.near && document.visibilityState !== 'hidden';
  for (const subscription of target.subscriptions) {
    if (subscription.value === active) continue;
    subscription.value = active;
    subscription.callback(active);
  }
}

function visibilityChanged() {
  for (const target of targets.values()) publish(target);
}

export function observeViewport(element, callback) {
  if (!listening) {
    document.addEventListener('visibilitychange', visibilityChanged);
    listening = true;
  }
  if (!observer && typeof IntersectionObserver !== 'undefined') {
    observer = new IntersectionObserver(entries => {
      for (const entry of entries) {
        const target = targets.get(entry.target);
        if (!target) continue;
        target.near = entry.isIntersecting;
        publish(target);
      }
    }, { rootMargin: '240px 0px', threshold: 0 });
  }
  let target = targets.get(element);
  if (!target) {
    target = { near: !observer, subscriptions: new Set() };
    targets.set(element, target);
    observer?.observe(element);
  }
  const subscription = { callback, value: undefined };
  target.subscriptions.add(subscription);
  publish(target);
  return () => {
    target.subscriptions.delete(subscription);
    if (!target.subscriptions.size) {
      targets.delete(element);
      observer?.unobserve(element);
    }
    if (!targets.size) {
      observer?.disconnect();
      observer = undefined;
      document.removeEventListener('visibilitychange', visibilityChanged);
      listening = false;
    }
  };
}
