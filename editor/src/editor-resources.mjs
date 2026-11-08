export async function ensureEditorResources(session) {
  if (session.destroyed || session.context.domain === 'notebook' || session.context.resourcesLoaded !== false) return;
  if (!session.callbacks) throw new Error('Holdets ressourcer kunne ikke hentes. Prøv igen.');
  if (!session.resourcesTask) {
    session.resourcesTask = (async () => {
      const result = await session.callbacks.invokeMethodAsync('EnsureNotionResources');
      if (!session.destroyed && result === false) throw new Error('Holdets ressourcer kunne ikke hentes. Prøv igen.');
    })().finally(() => { session.resourcesTask = undefined; });
  }
  await session.resourcesTask;
}
