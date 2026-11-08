# AI i dokumenteditoren

Undervisere bruger **AI**, `/ai`, tekstmarkering eller blokmenuen til at arbejde med den aktive holdside eller teoriside. Et skjulbart opgavepanel viser instruktion, fremdrift og kilder. Forslag vises ved teksten med **Godkend**, **Afvis** og **Justér**. Kilder kan udelukkes fra næste justering.

Forslag er private kladder i 30 dage. De ligger uden for dokumentets samarbejdsindhold, indtil underviseren godkender dem. Skjules panelet, eller åbnes en anden side, fortsætter serveren arbejdet. En afbrudt opgave kan fortsættes. Sletning af en opgave fjerner dens private kladde.

## Godkendelse

Editoren sammenligner kun ændringens mål med udgangspunktet. En redigering i et andet afsnit forhindrer derfor ikke godkendelse. Ændres eller slettes målteksten, vises en konflikt; brug **Justér** for et nyt forslag mod den aktuelle tekst. **Godkend alle** validerer alle områder, før den første ændring anvendes.

En godkendelse bruger editorens normale transaktion og kan fortrydes. Et stabilt ændrings-id og en kvittering gemmes sammen med ændringen, så et forsinket serversvar eller en genindlæsning ikke indsætter det samme to gange. Eksisterende medier, tegninger og særlige widgets skal bevares uændret, hvis AI ikke understøtter redigering af dem.

## Kilder og dækning

AI får et struktureret snapshot af den åbne side, også tekst som endnu ikke er gemt. Serveren søger efter relevante undervisningsuddrag med PostgreSQL-fuldtekstsøgning. Adgang kontrolleres før rangering og igen før udlevering/genoptagelse. Elevnoter, afleveringer og personkataloger er ikke automatiske kilder.

Tekst-/kodefiler samt tekstlag i PDF og Word behandles i den isolerede konvertering. Resultatet caches efter filversion. Skanninger uden tekstlag samt billeder, lyd og video får en synlig status; OCR og transskription indgår ikke. Hvis en stor side kun er delvist læst, vises dækningen i panelet.

## Drift

Den nye klient bruger `/api/ai/editor/runs`. Det tidligere `/api/ai/editor` findes fortsat for åbne klienter. Run-endpoints kræver lærerrolle, ejerskab og aktuel redigeringsadgang. PostgreSQL gemmer runs, ændringer og events; .NET-baggrundsarbejdet genoptager fra færdige ændringer. SSE understøtter cursor, heartbeat og genforbindelse. Nginx-buffering er slået fra for dette endpoint.

Docker-konfigurationen adskiller editormodellen fra emoji-analysen:

| Variabel | Standard |
| --- | --- |
| `OPENAI_EDITOR_MODEL` | `gpt-6.1-sol` |
| `OPENAI_EDITOR_REASONING_EFFORT` | `high` |
| `OPENAI_EDITOR_RESPONSE_TOKENS` | `32768` |
| `OPENAI_EDITOR_RUN_TOKENS` | `131072` |
| `OPENAI_EDITOR_RUN_MINUTES` | `10` |
| `OPENAI_EDITOR_MAX_BATCHES` | `128` |
| `OPENAI_EDITOR_WORKER_CONCURRENCY` | `2` |
| `OPENAI_EDITOR_RETENTION_DAYS` | `30` |

Nøglen genbruges fra serverens `OPENAI_API_KEY`. Den sendes aldrig til browseren. Indhold må ikke klippes for at passe gamle blok-/tekst-/tabelgrænser; ugyldige ændringer afvises med en forklaring. Færdige ændringer bevares ved rate limits, afbrudt streaming og budgetpause.

De faste danske evalopgaver findes i [ai-editor-evals.da.json](ai-editor-evals.da.json). De skal supplere automatiske tests med en faglig gennemgang af modeloutput og en godkendelse gennem den faktiske produktionsproxy.
