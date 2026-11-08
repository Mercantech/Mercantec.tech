const messages: Record<string, string> = {
  openai_not_configured: 'AI er endnu ikke konfigureret. Kontakt en administrator.',
  run_in_progress: 'Opgaven er i gang. Vent på de færdige forslag, eller stop opgaven.',
  hunk_being_adjusted: 'Forslaget bliver justeret. Vent til den nye version er klar.',
  hunk_version_changed: 'Forslaget har fået en ny version. Åbn opgaven igen.',
  hunk_already_reviewed: 'Forslaget er allerede gennemgået. Åbn opgaven igen for at se status.',
  instruction_too_large: 'Instruktionen er for lang. Del opgaven op i mindre trin.',
  document_too_large: 'Siden er for stor til én opgave. Markér et mindre afsnit og prøv igen.',
  source_access_changed: 'Adgangen til en kilde er ændret. Start en ny opgave uden den kilde.',
  invalid_change: 'Et forslag kunne ikke valideres. De færdige forslag er gemt. Prøv at fortsætte eller justere opgaven.',
  provider_unavailable: 'AI kunne ikke nås. Dine færdige forslag er gemt. Prøv at fortsætte om lidt.',
  generation_interrupted: 'Arbejdet blev afbrudt. Dine færdige forslag er gemt. Fortsæt for at skrive videre.',
  rate_limit: 'AI har travlt lige nu. Dine færdige forslag er gemt. Prøv at fortsætte om lidt.',
  rate_limited: 'AI har travlt lige nu. Dine færdige forslag er gemt. Prøv at fortsætte om lidt.',
  run_changed: 'Opgaven er opdateret. Åbn den igen for at se de nyeste forslag.',
  selection_required: 'Markér teksten, der skal ændres, og prøv igen.',
  selection_target_changed: 'Markeringen er ændret. Vælg området igen og justér forslaget.',
};
export function aiNativeError(value: unknown, fallback = 'Opgaven kunne ikke gennemføres. Prøv igen.') {
  if (typeof value !== 'string' || !value.trim()) return fallback;
  if (messages[value]) return messages[value];
  // Human validation messages may pass through; internal error identifiers do not.
  return /^[a-z][a-z0-9_]+$/.test(value) ? fallback : value;
}
