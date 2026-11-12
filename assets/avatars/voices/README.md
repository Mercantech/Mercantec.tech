# Danske karakterstemmer i LUP

De fem replikker pr. figur er forudgenererede WAV-filer. De er indtalt med den lokale danske [Røst-model til Chatterbox](https://huggingface.co/CoRal-project/roest-v3-chatterbox-500m) og afspilles i avatarsamlingen efter et klik.

| Figur | Stemmeprofil |
| --- | --- |
| BIP-01 | Præcis robotstemme med en kort metallisk resonans |
| Raymond | Dyb, rund og rolig stemme |
| Julemanden | Varm og fyldig stemme med let rumklang |
| LUPP | Lys, let og legende stemme |

`references/` indeholder figurernes fire syntetiske stemmereferencer. De er lavet af tale genereret med modellens indbyggede stemme. Rendereren bruger én reference til alle fem replikker for den samme figur. Efterbehandlingen tilpasser klang og tempo.

Den aktuelle `speech-provenance.json` ligger ved siden af hver figurs WAV-filer i `web/wwwroot/assets/avatars/<figur>/audio/`. Den angiver modelrevision, den anvendte reference, behandling, tekst og SHA-256 for hvert klip. `audio-manifest.json` har de færdige længder. Runtime er mono PCM16 ved 44.100 Hz.

Til ny eksport kan du bruge `render_avatar_voices.py` med Python fra den [lokale Røst-installation](../../../docs/local-rost-speech.md) og FFmpeg i PATH. Angiv checkout med `--repo`, modelmappen med `--model`, BIP-01's nuværende lydmanifest med `--bip-manifest` og en ny eksportmappe med `--output`. Rendereren bevarer replikteksterne og skriver resultatet til eksportmappen. Verificér afspilning og opdatér versionsmærket i `lup-avatars.js`, når replikfilerne udskiftes.
