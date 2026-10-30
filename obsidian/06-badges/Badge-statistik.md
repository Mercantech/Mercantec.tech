---
type: kontekst
status: active
tags:
  - badges
  - statistik
---

# Badge-statistik (% af brugere)

Hvert badge skal kunne vise **hvor stor en andel af brugerne der har det**.

## Regel

```
procent = (antal brugere med badge / antal relevante brugere) × 100
```

**Relevant population (v1-forslag):** alle aktive brugere på platformen.  
Senere kan I snævre ind til “på dette forløb/hold” for emne-badges — dokumentér i badge-noten hvis det afviger.

## UI

- Vis **procent** (fx `12%`) på badge-detalje / tooltip / væg.
- Gerne også absolut tal (`23 / 190`) for undervisere.
- Opdateres når badge tildeles/fjernes (cache OK, må gerne være near-realtime).

## Privacy

- Kun **aggregat** — ikke liste over hvem der har badge i elev-view (underviser kan have separat hold-overblik senere).
- Undgå at eksponere enkeltpersoner via “kun 1 har den” + holdkontekst uden omtanke (GDPR).

## Acceptkriterier (platform)

- [ ] API/endpoint eller felt: `holdersCount`, `eligibleCount`, `percent`
- [ ] Elev/underviser kan se % på badge
- [ ] Gælder alle badge-typer (emne, særlig, …) inkl. [[Hawaii-skjorte]] og [[Fancy-Friday]]

## Relateret

- [[Badge-oversigt]]
- [[Levels-XP-Badges]]
- [[Karakter-og-certifikatvæg]]
- [[Ansvarsområder]] — Sikkerhed / GDPR
