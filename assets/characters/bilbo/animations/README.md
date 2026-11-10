# Bilbos animationer

Hvert billedark viser fire tegnede tilstande. Læs felterne fra venstre mod højre, øverste række først. GIF’er og deres WebP-stillbilleder ligger i `web/wwwroot/images/characters/bilbo/animations/`.

Kør `pwsh -File assets/characters/bilbo/animations/Render-Animations.ps1` fra projektroden for at genskabe GIF’erne. Scriptet bruger FFmpeg og FFprobe til at klippe de fire felter ud og gemme hvert loop i 512 × 512 px.

Billedprompterne og de faglige kilder står i `../scenes-and-animations.json`.
