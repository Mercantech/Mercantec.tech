# Gummiandens animationer

Hvert billedark indeholder fire tegnede tilstande med den samme and og opstilling. Læs felterne fra venstre mod højre, øverste række først. Animerede GIF'er og deres WebP-stillbilleder ligger i `web/wwwroot/images/characters/gummianden/animations/`.

Kør `pwsh -File assets/characters/gummianden/animations/Render-Animations.ps1` fra projektroden for at samle GIF'erne igen. Scriptet bruger FFmpeg og FFprobe til at klippe felterne ud, holde hver tilstand kort på skærmen og gemme loopet i 512 × 512 px.

Billedprompterne og kilderne til de enkelte figurer står i `../scenes-and-animations.json`.
