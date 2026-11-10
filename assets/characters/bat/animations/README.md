# Næsebladets animationer

Hvert billedark viser fire tegnede tilstande. Læs felterne fra venstre mod højre, øverste række først. GIF’er og deres WebP-stillbilleder ligger i `web/wwwroot/images/characters/bat/animations/`.

Kør `pwsh -File assets/characters/bat/animations/Render-Animations.ps1` fra projektroden for at genskabe GIF’erne. Scriptet bruger FFmpeg og FFprobe til at klippe de fire felter ud og gemme loopet i 512 × 512 px.

Billedprompterne står i `../scenes-and-animations.json`.
