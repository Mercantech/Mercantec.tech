# GruppeRoulette reference

Reference: https://github.com/Mercantech/GruppeRoulette (reviewed 6 November 2026).

The original presents group formation as a purple and gold slot machine with cycling name reels, a draggable lever, bulb strips, playful compound team names, sound, confetti, a full-class overview and multiple rounds avoiding previous partners.

The LUP classroom presentation follows that interaction and visual direction. Its Blazor markup, CSS and JavaScript are an original adaptation; the source repository does not currently declare a license, so its original files have not been vendored. The page includes a visible attribution link.

LUP-specific behavior:

- Only an authenticated managing teacher can load the hold's roster and group history.
- The primary classroom action opens the standalone presentation in the same tab, preserving the existing session-scoped login. Fullscreen is available from the stage; finishing returns to the hold.
- All selected pupils are assigned in balanced groups; no remainder pupils are dropped.
- A complete valid round is planned before the first reveal, then groups are revealed one at a time.
- Saved group-set history can be selected for avoiding previous partners. A bounded search reports inability to find a plan rather than claiming mathematical impossibility.
- Generated rounds stay local until the teacher explicitly saves them in a named group set.
- The latest local rounds are stored by account and hold using membership IDs. Names are resolved against the currently authorized roster on restoration.
- Sounds use local Web Audio oscillators; pupil names are not sent to a speech service.
- Reduced-motion preference disables cycling and confetti.
