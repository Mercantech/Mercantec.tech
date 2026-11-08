"""Raymond's original organic/wood/air sound palette. No samples or external API.

Run once before local speech rendering, then again to package the five WAV lines.
"""
from pathlib import Path
import array
import hashlib
import json
import math
import random
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "audio"
RAW = Path(__file__).resolve().parent / "raw"
SR = 44100
TAU = math.tau
RNG = random.Random(20261108)
OUT.mkdir(parents=True, exist_ok=True)

PHRASES = [
    ("phrase_1_welcome", "welcome", "Jeg er Professor Codd, men du må gerne kalde mig Raymond."),
    ("phrase_2_start", "start", "Lad os finde forbindelsen — én række ad gangen."),
    ("phrase_3_success", "success", "Sådan! Nu hænger tabellerne sammen."),
    ("phrase_4_retry", "retry", "En lille fejl er bare et godt sted at blive klogere."),
    ("phrase_5_milestone", "milestone", "Tak for pladsen i din samling. Hvad skal vi undersøge først?"),
]


def silence(seconds):
    return [0.0] * round(seconds * SR)


def add(target, sound, offset=0.0, gain=1.0):
    first = round(offset * SR)
    for i, sample in enumerate(sound):
        index = first + i
        if 0 <= index < len(target):
            target[index] += sample * gain
    return target


def edge(t, duration, attack=.012, release=.09):
    if t < 0 or t >= duration:
        return 0.0
    a = min(1.0, t / max(attack, 1 / SR))
    r = min(1.0, (duration - t) / max(release, 1 / SR))
    return math.sin(a * math.pi / 2) ** 2 * math.sin(r * math.pi / 2) ** 2


def air(duration, gain=.5, cutoff=.045, decay=1.0, swell=False):
    samples, low, previous = [], 0.0, 0.0
    for i in range(round(duration * SR)):
        t = i / SR
        low += cutoff * (RNG.uniform(-1, 1) - low)
        # A little dry friction sits above the breath without a sharp electronic hiss.
        friction = low - previous
        previous = low
        shape = math.sin(math.pi * t / duration) ** 1.4 if swell else math.exp(-decay * t / duration)
        samples.append((low + friction * .18) * gain * shape * edge(t, duration, .018, .08))
    return samples


def wood(duration, frequency, gain=.3):
    partials = [(1, 1, 1), (2.756, .32, 1.7), (5.404, .13, 2.6), (8.93, .035, 4)]
    samples = []
    for i in range(round(duration * SR)):
        t = i / SR
        value = sum(math.sin(TAU * frequency * ratio * t) * level * math.exp(-t * speed * 6 / duration)
                    for ratio, level, speed in partials)
        samples.append(value * gain * edge(t, duration, .003, .04))
    return samples


def horn(duration, start, end=None, gain=.3, warmth=.15, tremolo=3):
    """Mellow elephant-trunk/horn resonance, never a square-wave robot oscillator."""
    end = start if end is None else end
    phase, samples = 0.0, []
    for i in range(round(duration * SR)):
        t = i / SR
        fraction = t / duration
        frequency = start * (end / start) ** fraction
        # Gentle natural drift and breath pulsing rather than ring modulation.
        frequency *= 1 + .003 * math.sin(TAU * 4.7 * t)
        phase += TAU * frequency / SR
        value = math.sin(phase) + .32 * math.sin(phase * 2) + .14 * math.sin(phase * 3) + warmth * .3 * math.sin(phase * 4)
        value *= .92 + .08 * math.sin(TAU * tremolo * t)
        samples.append(value * gain * edge(t, duration, min(.065, duration * .2), min(.2, duration * .3)))
    return samples


def thud(duration=.23, pitch=88, gain=.5):
    result = silence(duration)
    add(result, horn(duration, pitch * 1.3, pitch * .63, gain, .05, 1))
    add(result, air(min(duration, .16), gain * .7, .16, 6), .004)
    # A short soft sole/contact transient, with no metallic click.
    add(result, wood(min(duration, .11), 260, gain * .09), .007)
    return result


def notes(values, spacing=.15, duration=.4, total=None, gain=.25):
    result = silence(total or (len(values) - 1) * spacing + duration + .05)
    for index, frequency in enumerate(values):
        add(result, wood(duration, frequency, gain), index * spacing)
    return result


def cloth(duration=.3, descending=False, gain=.5):
    result = air(duration, gain, .075, 1, swell=True)
    add(result, air(duration * .6, gain * .24, .2, 3), duration * .18)
    if descending:
        add(result, horn(duration * .6, 155, 88, gain * .025), duration * .25)
    return result


clips, labels, peaks, descriptions = {}, {}, {}, {}


def clip(key, label, data, peak, description):
    clips[key], labels[key], peaks[key], descriptions[key] = data, label, peak, description


clip("idle", "Rolig vejrtrækning", air(.72, .35, .022, .15, True), .13, "En enkelt blød udånding. Ingen automatisk baggrundslyd.")
clip("blink", "Lille bevægelse", cloth(.105, gain=.25), .12, "Meget kort, afrundet tekstil-/luftbevægelse uden elektronisk bip.")
a = silence(.68); add(a, cloth(.5, gain=.35)); add(a, notes([261.63, 329.63], .16, .27, .55, .12), .07)
clip("wave", "Venlig hilsen", a, .38, "Cardiganbevægelse og to varme trætoner.")
a = silence(.95); add(a, notes([220, 293.66, 261.63], .23, .36, .92, .16)); add(a, air(.65, .1, .025, 1, True), .14)
clip("think", "Tankepause", a, .29, "Tre lave, eftertænksomme træresonanser.")
a = silence(.72)
for at, f, d in [(0, 145, .17), (.22, 164, .15), (.43, 153, .23)]:
    add(a, horn(d, f, f * 1.04, .1, .1, 3), at); add(a, air(d, .1, .05), at)
clip("talk", "Et lille professor-hm", a, .3, "Tre bløde snabelresonanser som gestuslyd. Ikke erstatning for de rigtige talereplikker.")
a = silence(.34); add(a, cloth(.22, gain=.3)); add(a, wood(.25, 392, .2), .07)
clip("point", "Se her", a, .36, "Let stofbevægelse efterfulgt af en tør træmarkering.")
a = silence(1.22); add(a, notes([261.63, 329.63, 392, 523.25], .14, .58, 1.2, .18)); add(a, horn(.55, 174.61, 261.63, .08), .42)
clip("celebrate", "Glad professorjubel", a, .6, "Varm C-dur-figur med et diskret, venligt snabelhorn.")
a = notes([196, 261.63, 329.63], .22, .45, 1.0, .19); add(a, cloth(.6, gain=.12), .08)
clip("encourage", "Opmuntring", a, .42, "En roligt stigende træklang uden fanfare eller maskinlyde.")
a = silence(.46); add(a, air(.35, .4, .065, .3, True)); add(a, horn(.23, 152, 235, .1), .11)
clip("surprised", "Mild overraskelse", a, .39, "Kort indånding og et lille løft i snabelklangen.")
a = silence(1.48); add(a, notes([196, 261.63, 329.63, 392, 523.25], .14, .65, 1.45, .18)); add(a, cloth(.95, gain=.13), .05); add(a, horn(.7, 130.81, gain=.08), .45)
clip("unlock", "Raymond er låst op", a, .64, "En varm, afsluttet træ-/hornakkord til en ny ven i samlingen.")
clip("walk", "Et tungt, blødt fodtrin", thud(.24, 78, .55), .55, "Ét jordnært elefantfodtrin pr. fodkontakt, ikke et helt loop.")
clip("run", "Et hurtigt elefantfodtrin", thud(.18, 94, .6), .58, "Ét kortere, fastere fodtrin pr. løbekontakt.")
a = silence(.48); add(a, thud(.2, 88, .24)); add(a, air(.4, .6, .04, .2, True), .06); add(a, horn(.29, 105, 220, .08), .1)
clip("jump", "Afsæt", a, .52, "Blødt afsæt, luftbevægelse og et lille organisk løft.")
a = silence(.42); add(a, thud(.35, 61, .7)); add(a, cloth(.32, gain=.23), .04)
clip("land", "Blød landing", a, .68, "Vægtig, polstret landing med efterfølgende tøjbevægelse.")
a = silence(.57); add(a, cloth(.5, True, .5)); add(a, thud(.2, 71, .15), .28)
clip("sit", "Sætter sig til rette", a, .35, "Cardigan og en rolig tyngdebevægelse. Ingen bestemt stol eller genstand.")
a = silence(1.6); add(a, air(1.5, .3, .018, .15, True)); add(a, horn(1.2, 76, 68, .025, .02, 1), .12)
clip("sleep", "Søvnig udånding", a, .22, "En enkelt lav, blød snorke-/åndedrætsgestus, aldrig automatisk i et vedvarende loop.")
a = silence(.4); add(a, cloth(.27, gain=.25)); add(a, wood(.28, 349.23, .13), .09)
clip("pickup", "Samler noget op", a, .36, "Neutral hånd-/tøjbevægelse og let træaccent, uden en bestemt mønt eller genstand.")
a = silence(.38); add(a, wood(.16, 293.66, .16)); add(a, wood(.22, 392, .13), .11); add(a, cloth(.25, gain=.12), .04)
clip("interact", "Undersøger og aktiverer", a, .34, "To taktile, bløde markeringer til en generisk interaktion.")
a = silence(.34); add(a, thud(.25, 111, .42)); add(a, air(.22, .25, .1, 3), .05)
clip("hit", "Ramt", a, .63, "En polstret slaglyd. Ingen skrig, metaldele eller voldsom skade.")
a = silence(1.12); add(a, cloth(.85, True, .4)); add(a, horn(.65, 145, 72, .1), .08); add(a, thud(.29, 62, .18), .66)
clip("knockout", "Midlertidigt ude af spil", a, .53, "Tøjbevægelse, faldende organisk klang og en blød afslutning.")
a = silence(1.25); add(a, air(.95, .4, .027, .1, True)); add(a, notes([196, 261.63, 392], .22, .58, 1.2, .17), .05)
clip("respawn", "Tilbage igen", a, .58, "Luftigt tilbageblik efterfulgt af tre venlige, stigende trætoner.")
a = silence(.72); add(a, horn(.47, 112, 76, .45, .1, 3)); add(a, air(.48, .8, .08, 1), .04); add(a, thud(.25, 72, .2), .1)
clip("ability_1_pulse", "Snabelpuls", a, .73, "Et kort, varmt snabelhorn med trykbølge. Selve projektilet er en separat sprite.")
a = silence(1.13); add(a, air(.9, .7, .028, .1, True)); add(a, horn(.85, 235, 88, .17), .02)
for at, f in [(.12, 392), (.4, 329.63), (.68, 261.63)]: add(a, wood(.38, f, .17), at)
add(a, thud(.21, 84, .2), .86)
clip("ability_2_magnet", "Relationsgreb · JOIN", a, .65, "To klanglag trækkes ind mod et fælles, lavt anslag; et organisk relationsgreb.")
a = silence(1.3); add(a, horn(1.05, 98, 130.81, .16), .04); add(a, air(.74, .4, .025, .2, True))
for at, f in [(.08, 261.63), (.18, 329.63), (.28, 392), (.38, 523.25)]: add(a, wood(.83, f, .13), at)
clip("ability_3_shield", "Tryghedsskjold", a, .66, "En varm beskyttende akkord åbner sig omkring en rolig basresonans.")
a = silence(.76); add(a, thud(.23, 65, .46)); add(a, air(.62, .85, .065, .65, True), .08); add(a, horn(.46, 100, 165, .12), .06)
clip("ability_4_dash", "Elefantstorm", a, .72, "Et tungt afsæt efterfulgt af en hurtig, bred luftstrøm.")
a = silence(1.62); add(a, notes([196, 261.63, 329.63, 392, 523.25], .23, .65, 1.6, .16)); add(a, horn(1.1, 98, 130.81, .06), .2); add(a, air(1.3, .12, .02, .1, True), .1)
clip("ability_5_repair", "Gendannelse", a, .61, "Fem stigende træklange, rolig ånde og en fuldendt, blød afslutning til genopretning.")
a = silence(2.15); add(a, horn(1.98, 65.41, 130.81, .18, .12, 2)); add(a, air(1.9, .32, .025, .05, True), .12)
for j in range(8): add(a, wood(.33, [130.81,196,261.63,329.63][j%4], .1+j*.008), .12+j*.225)
clip("ultimate_charge", "Den store JOIN · opladning", a, .7, "En voksende, dyb snabelresonans med rytmiske træpulser og samlet lufttryk.")
a = silence(2.65); add(a, thud(.52, 52, .52)); add(a, horn(1.5, 130.81, 98, .25, .14, 2), .02); add(a, air(1.65, .85, .045, 1.6), .04)
for at, f in [(.22, 261.63), (.34, 329.63), (.46, 392), (.58, 523.25)]: add(a, wood(1.7, f, .16), at)
clip("ultimate_release", "Den store JOIN · udløsning", a, .76, "En majestætisk men venlig elefant-/hornbølge med lavt impact og varm durklang. Ingen robotlaser.")


def write_wav(path, data, peak=.7):
    if not data or not any(abs(v) > .000001 for v in data):
        raise ValueError(f"Empty audio: {path}")
    dc = sum(data) / len(data)
    data = [v - dc for v in data]
    fade = min(round(.012 * SR), len(data) // 4)
    for i in range(fade):
        gain = .5 - .5 * math.cos(math.pi * i / fade)
        data[i] *= gain
        data[-i-1] *= gain
    scale = peak / max(abs(v) for v in data)
    pcm = array.array('h', (round(max(-.999, min(.999, v * scale)) * 32767) for v in data))
    with wave.open(str(path), 'wb') as target:
        target.setnchannels(1); target.setsampwidth(2); target.setframerate(SR)
        target.writeframes(pcm.tobytes())
    return round(len(data) / SR, 4)


def read_wav(path):
    with wave.open(str(path), 'rb') as source:
        if source.getsampwidth() != 2 or source.getnchannels() != 1 or source.getframerate() != SR:
            raise ValueError(f"Expected mono PCM16/44100: {path}")
        samples = array.array('h'); samples.frombytes(source.readframes(source.getnframes()))
    return [sample / 32768 for sample in samples]


def package_speech():
    voice = json.loads((RAW / 'voice.json').read_text(encoding='utf-8-sig')) if (RAW / 'voice.json').exists() else {}
    for key, _, _ in PHRASES:
        source = RAW / (key + '.wav')
        if not source.exists():
            continue
        intermediate = RAW / (key + '-44100.wav')
        # A high-quality resample plus restrained warm EQ; no robot/vocoder/ring processing.
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-af', 'aresample=44100:resampler=soxr:precision=24,highpass=f=60,equalizer=f=220:t=q:w=0.8:g=1.8,lowpass=f=10500',
                        '-ac', '1', '-c:a', 'pcm_s16le', str(intermediate)], check=True)
        data = read_wav(intermediate)
        audible = [i for i, v in enumerate(data) if abs(v) > .0025]
        if not audible:
            raise ValueError('Silent speech: ' + key)
        data = data[max(0, audible[0] - round(.07 * SR)):min(len(data), audible[-1] + round(.16 * SR))]
        write_wav(OUT / (key + '.wav'), silence(.10) + data + silence(.14), .70)
    return voice


def main():
    sounds = []
    for key, data in clips.items():
        duration = write_wav(OUT / (key + '.wav'), data, peaks[key])
        sounds.append(dict(id=key, label=labels[key], file=key+'.wav', path='audio/'+key+'.wav',
                           duration=duration, durationSeconds=duration, loop=False, kind='sfx',
                           source='original-local-organic-procedural-synthesis', gain=.65, description=descriptions[key]))
    voice = package_speech()
    phrases = []
    for key, event, text in PHRASES:
        file = OUT / (key + '.wav')
        exists = file.exists()
        duration = round(len(read_wav(file)) / SR, 4) if exists else 0
        phrases.append(dict(id=key, event=event, text=text, language='da-DK', file=key+'.wav' if exists else None,
                            path='audio/'+key+'.wav' if exists else None, available=exists,
                            duration=duration, durationSeconds=duration,
                            source='local-Windows-OneCore-da-DK-warm-professor-no-robot-filter' if exists else None))
    ready = all(item['available'] for item in phrases)
    manifest = dict(schemaVersion=1, avatarId='raymond', name='Raymond / Professor Codd', sampleRate=SR,
                    channels=1, format='PCM16 WAV', sfx=sounds, sounds={s['id']: s for s in sounds}, phrases=phrases,
                    speech=dict(available=ready, language='da-DK', voice=voice.get('voice'),
                                speakingRate=voice.get('speakingRate'), audioPitch=voice.get('audioPitch'),
                                status='ready' if ready else 'local-speech-not-yet-rendered', processing='warm EQ, high-quality resample; no robot effect'),
                    playback=dict(defaultMuted=True, maxConcurrentSfx=3, phraseCooldownSeconds=18,
                                  stopPreviousPhrase=True, idleSoundsAutomatic=False, footsteps='one clip per foot-contact event'),
                    license=dict(proceduralSfx='Original project assets, no sampled recordings.',
                                 speech='Synthesized locally with an installed Danish Windows OneCore voice. No API, network call or voice cloning.'))
    (OUT / 'audio-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    (OUT / 'phrases.json').write_text(json.dumps(phrases, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    full, full_cues = silence(.2), []
    for key in clips:
        full_cues.append(dict(id=key, label=labels[key], startSeconds=round(len(full)/SR, 3)))
        full += read_wav(OUT / (key+'.wav')) + silence(.3)
    write_wav(OUT / 'all-sfx-preview.wav', full, .76)
    (OUT / 'all-sfx-preview-cues.json').write_text(json.dumps(full_cues, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    demo, cues = silence(.2), []
    for key in ['wave', 'walk', 'run', 'jump', 'land', 'ability_1_pulse', 'ability_2_magnet', 'ability_3_shield',
                'ability_4_dash', 'ability_5_repair', 'ultimate_charge', 'ultimate_release', 'celebrate']:
        cues.append(dict(id=key, startSeconds=round(len(demo)/SR, 3)))
        demo += read_wav(OUT / (key+'.wav')) + silence(.32)
    for item in phrases:
        if item['available']:
            cues.append(dict(id=item['id'], startSeconds=round(len(demo)/SR, 3), text=item['text']))
            demo += read_wav(OUT / item['file']) + silence(.45)
    write_wav(OUT / 'audio-demo.wav', demo, .76)
    (OUT / 'audio-demo-cues.json').write_text(json.dumps(cues, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(json.dumps(dict(sfx=len(sounds), spoken=sum(x['available'] for x in phrases), demoSeconds=round(len(demo)/SR, 2)), ensure_ascii=False))


if __name__ == '__main__':
    main()
