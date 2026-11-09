"""Original Santa bell/sleigh/snow/cloth sounds. No sampled audio or external API.

Run once to create phrases.json, render_danish_speech.ps1, then run again.
The only reused code is the PCM/export architecture; every score is newly authored.
"""
from pathlib import Path
import array
import json
import math
import random
import shutil
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'audio'
RAW = Path(__file__).resolve().parent / 'raw'
SR = 44100
TAU = math.tau
RNG = random.Random(20261109)
OUT.mkdir(parents=True, exist_ok=True)

PHRASES = [
    ('phrase_1_welcome', 'welcome', 'Ho ho ho! Er du klar til lidt julemagi?'),
    ('phrase_2_start', 'start', 'Selv den største opgave løses ét skridt ad gangen.'),
    ('phrase_3_success', 'success', 'Godt klaret! Det fortjener en plads på den gode liste.'),
    ('phrase_4_retry', 'retry', 'En lille pause, og så prøver vi igen.'),
    ('phrase_5_milestone', 'milestone', 'Sammen får vi magien til at virke!'),
]


def silence(seconds):
    return [0.0] * round(seconds * SR)


def add(target, sound, offset=0, gain=1):
    first = round(offset * SR)
    for index, value in enumerate(sound):
        at = first + index
        if 0 <= at < len(target):
            target[at] += value * gain
    return target


def edge(t, duration, attack=.006, release=.065):
    if not 0 <= t < duration:
        return 0
    a = min(1, t / max(attack, 1 / SR))
    r = min(1, (duration - t) / max(release, 1 / SR))
    return math.sin(a * math.pi / 2) ** 2 * math.sin(r * math.pi / 2) ** 2


def wind(duration, gain=.3, smooth=.04, decay=1, swell=False):
    values, low, last = [], 0, 0
    for index in range(round(duration * SR)):
        t = index / SR
        low += smooth * (RNG.uniform(-1, 1) - low)
        dry = low - last
        last = low
        shape = math.sin(math.pi * t / duration) ** 1.3 if swell else math.exp(-decay * t / duration)
        values.append((low + .45 * dry) * gain * shape * edge(t, duration, .012, .075))
    return values


def bell(duration, frequency, gain=.22, bright=.65):
    """Rounded metal resonances, with fixed inharmonic ratios and decaying overtones."""
    partials = [(1, 1, 1), (2.01, .28 * bright, 1.25), (2.76, .22 * bright, 1.6),
                (4.07, .12 * bright, 2), (5.43, .07 * bright, 2.7)]
    return [sum(math.sin(TAU * frequency * ratio * (i / SR)) * level *
                math.exp(-4.8 * speed * i / SR / duration) for ratio, level, speed in partials)
            * gain * edge(i / SR, duration, .003, .055)
            for i in range(round(duration * SR))]


def glow(duration, start, end=None, gain=.15):
    """Soft warm air resonance; no square-wave oscillator, laser or robot vocoder."""
    end = start if end is None else end
    phase, values = 0, []
    for i in range(round(duration * SR)):
        t = i / SR
        phase += TAU * start * (end / start) ** (t / duration) / SR
        value = math.sin(phase) + .17 * math.sin(2 * phase) + .045 * math.sin(3 * phase)
        values.append(value * gain * edge(t, duration, .06, min(.24, duration * .3)))
    return values


def cloth(duration=.3, gain=.5):
    result = wind(duration, gain, .07, .2, True)
    add(result, wind(duration * .6, gain * .18, .2, 2), duration * .12)
    return result


def snowstep(duration=.23, pitch=75, gain=.3):
    result = silence(duration)
    add(result, glow(duration, pitch * 1.25, pitch * .7, gain))
    add(result, wind(duration * .8, gain * 1.8, .18, 3), .006)
    # Two independently randomized, very short snow-compression frictions.
    add(result, wind(duration * .35, gain * .5, .5, 2), .025)
    add(result, wind(duration * .3, gain * .4, .3, 3), duration * .28)
    return result


def jingle(duration=.55, strikes=5, gain=.14, seed_pitch=1050):
    result = silence(duration)
    for index in range(strikes):
        at = .015 + (duration * .55 / max(1, strikes - 1)) * index
        frequency = seed_pitch * [1, 1.3348, 1.1224, 1.4983, 1.2599][index % 5]
        add(result, bell(duration * .42, frequency, gain * RNG.uniform(.65, 1), .8), at)
    return result


def melody(frequencies, spacing=.16, duration=.55, total=None, gain=.17):
    result = silence(total or (len(frequencies) - 1) * spacing + duration + .05)
    for i, frequency in enumerate(frequencies):
        add(result, bell(duration, frequency, gain, .6), i * spacing)
    return result


clips, labels, peaks, descriptions = {}, {}, {}, {}


def clip(key, label, samples, peak, description):
    clips[key], labels[key], peaks[key], descriptions[key] = samples, label, peak, description


a = silence(.82); add(a, cloth(.72, .15)); add(a, wind(.65, .16, .018, .1, True), .08)
clip('idle', 'Varm vinteråndedræt', a, .13, 'En enkelt rolig udånding og blødt pelskantet stof; aldrig automatisk vedvarende lyd.')
clip('blink', 'Et lille blink', cloth(.115, .15), .12, 'Et diskret, afrundet pels-/luftstrøg uden bip.')
a = silence(.75); add(a, cloth(.5, .2)); add(a, jingle(.58, 3, .07, 1174.66), .08)
clip('wave', 'Julehilsen', a, .4, 'Ærmet bevæger sig med tre lette klokkeanslag.')
a = melody([293.66, 369.99, 329.63], .26, .43, 1.08, .11); add(a, cloth(.4, .1), .08)
clip('think', 'En lun tanke', a, .3, 'Tre rolige, dæmpede celestaagtige klokketoner.')
a = silence(.82)
for at, f, d in [(0, 103.83, .19), (.25, 110, .17), (.49, 98, .23)]:
    add(a, glow(d, f, f * .95, .11), at); add(a, cloth(d, .07), at)
clip('talk', 'Lun gestus', a, .3, 'Tre lave, varme gestusresonanser og stof. Rigtig tale ligger i egne WAV-filer.')
a = silence(.4); add(a, cloth(.25, .22)); add(a, bell(.29, 783.99, .12, .4), .08)
clip('point', 'Se lige her', a, .38, 'En tydelig, blød klokkemarkering og et ærmestrøg.')
a = silence(1.34); add(a, melody([293.66, 369.99, 440, 587.33], .16, .68, 1.32, .15)); add(a, jingle(.75, 6, .045, 1396.91), .38)
clip('celebrate', 'Glædelig jubel', a, .62, 'En nykomponeret stigende D-durfigur med små juleklokker; ingen kendt julesang.')
a = melody([220, 293.66, 369.99], .24, .6, 1.18, .13); add(a, glow(.75, 146.83, 164.81, .025), .12)
clip('encourage', 'Et varmt hep', a, .43, 'Tre venlige klokketoner med et roligt, varmt fundament.')
a = silence(.5); add(a, wind(.33, .38, .055, .1, True)); add(a, bell(.3, 987.77, .12, .4), .12)
clip('surprised', 'Ho, hvad var det?', a, .4, 'En kort indånding og en løftet, rund klokkeresonans.')
a = silence(1.62); add(a, melody([220, 293.66, 369.99, 440, 587.33], .17, .82, 1.6, .14)); add(a, jingle(.95, 8, .035, 1567.98), .48)
clip('unlock', 'Julemanden er låst op', a, .66, 'Et færdigt, originalt klokkemotiv med glitrende slutklang til oplåsningen.')
a = snowstep(.27, 73, .36); add(a, jingle(.18, 2, .014, 1318.51), .03)
clip('walk', 'Et støvletrin i sne', a, .55, 'Én komprimeret snelyd og en diskret seletøjsklokke pr. fodkontakt.')
a = snowstep(.19, 88, .4); add(a, jingle(.15, 2, .019, 1396.91), .014)
clip('run', 'Et hurtigt vintertrin', a, .59, 'Én fastere, kortere snestøvlekontakt med højere klokkeaccent.')
a = silence(.52); add(a, snowstep(.18, 83, .2)); add(a, wind(.41, .5, .04, .1, True), .07); add(a, bell(.3, 659.26, .07), .14)
clip('jump', 'Julehop', a, .51, 'Et let støvleafsæt, en luftbue og en kort klokkeløftning.')
a = silence(.46); add(a, snowstep(.37, 56, .5)); add(a, cloth(.32, .25), .05); add(a, jingle(.23, 3, .024, 987.77), .05)
clip('land', 'Støvlerne lander', a, .68, 'Blød, tung snelanding med tøj og en afdæmpet klokkeklang.')
a = silence(.61); add(a, cloth(.54, .45)); add(a, snowstep(.22, 67, .12), .31)
clip('sit', 'En lun pause', a, .34, 'Pelskant og frakke falder til ro uden lyd af en bestemt stol.')
a = silence(1.72); add(a, wind(1.6, .22, .016, .1, True)); add(a, glow(1.25, 65.41, 60, .026), .15)
clip('sleep', 'Søvnig julepause', a, .22, 'En enkelt langsom udånding med en mild snorkeresonans; ikke automatisk loop.')
a = silence(.44); add(a, cloth(.3, .22)); add(a, bell(.28, 698.46, .09, .25), .11)
clip('pickup', 'Et forsigtigt greb', a, .35, 'Generisk handske-/ærmebevægelse og en lille accent; genstandens materiale bestemmes af spillet.')
a = silence(.43); add(a, bell(.18, 523.25, .1, .25)); add(a, bell(.26, 659.26, .09, .35), .13); add(a, cloth(.28, .1), .03)
clip('interact', 'Julemanden undersøger', a, .34, 'To dæmpede, taktile markeringer til en generisk interaktion.')
a = silence(.38); add(a, snowstep(.28, 104, .33)); add(a, cloth(.26, .32), .04)
clip('hit', 'Polstret træffer', a, .63, 'En kort, blød støvle-/stofkontakt uden skrig eller metal.')
a = silence(1.23); add(a, cloth(.86, .38)); add(a, melody([392, 329.63, 246.94], .25, .4, 1.12, .085), .03); add(a, snowstep(.28, 52, .18), .84)
clip('knockout', 'Midlertidigt ude af legen', a, .52, 'Et originalt faldende klokkemotiv, frakkebevægelse og en polstret afslutning.')
a = silence(1.38); add(a, wind(1.05, .28, .023, .1, True)); add(a, melody([246.94, 329.63, 493.88], .23, .75, 1.35, .14), .04)
clip('respawn', 'Julemanden vender tilbage', a, .59, 'Et luftigt, stigende klokkemotiv til en frisk begyndelse.')
a = silence(.86); add(a, wind(.5, .65, .055, .4, True)); add(a, bell(.58, 1174.66, .19), .09); add(a, bell(.49, 1567.98, .08), .16); add(a, snowstep(.22, 92, .12), .1)
clip('ability_1_pulse', 'Stjerneskud', a, .72, 'Et klart, gyldent stjerneanslag med kort luftsus. Projektilet findes i sit eget lag.')
a = silence(1.22); add(a, wind(1.02, .5, .03, .1, True))
for at, f in [(.08, 880), (.3, 739.99), (.53, 587.33), (.76, 440)]: add(a, bell(.4, f, .14, .4), at)
add(a, cloth(.27, .25), .87)
clip('ability_2_magnet', 'Gavegreb', a, .65, 'Fire klokketoner trækker sig ned og ind mod et blødt, fælles greb. Ingen bestemt gave er indbygget i karakteren.')
a = silence(1.44); add(a, glow(1.23, 146.83, gain=.08), .04); add(a, wind(.85, .25, .02, .1, True))
for at, f in [(.06, 293.66), (.18, 369.99), (.3, 440), (.42, 587.33)]: add(a, bell(.96, f, .13, .3), at)
clip('ability_3_shield', 'Julefred', a, .65, 'En varm, beskyttende klokkeakkord åbner sig omkring en rolig bas.')
a = silence(.9); add(a, snowstep(.24, 61, .32)); add(a, wind(.72, .85, .065, .5, True), .08); add(a, jingle(.73, 9, .055, 1567.98), .06)
clip('ability_4_dash', 'Kanesus', a, .73, 'Hurtigt luftsus med en nykomponeret rytme af små sledeklokker og et blødt afsæt.')
a = silence(1.76); add(a, melody([220, 293.66, 369.99, 440, 587.33], .25, .74, 1.74, .13)); add(a, glow(1.25, 110, 146.83, .045), .17); add(a, cloth(1.2, .09), .12)
clip('ability_5_repair', 'Julevarme', a, .61, 'Fem roligt stigende klokketoner med et varmt efterklangsfundament til heling.')
a = silence(2.3); add(a, glow(2.12, 55, 110, .12)); add(a, wind(1.95, .3, .023, .1, True), .15)
for index in range(10):
    add(a, bell(.42, [220,293.66,369.99,440,587.33][index%5], .07+index*.008, .5), .1+index*.205)
add(a, jingle(1.15, 12, .022, 1760), 1.08)
clip('ultimate_charge', 'Den store gavefest · opladning', a, .7, 'En voksende original klokkefigur, samlet vintervind og varm dybde; ingen laser eller robotvocoder.')
a = silence(2.86); add(a, snowstep(.48, 44, .35)); add(a, glow(1.5, 146.83, 110, .15), .02); add(a, wind(1.8, .7, .04, 1.2), .04)
for at, f in [(.1,293.66),(.19,369.99),(.28,440),(.37,587.33),(.46,739.99)]: add(a, bell(2.15, f, .15, .55), at)
add(a, jingle(1.8, 16, .035, 1567.98), .33)
clip('ultimate_release', 'Den store gavefest · udløsning', a, .76, 'Stor, venlig fest af klokker, vind og blødt impact med lang gylden hale. Nykomponeret uden melodisamples.')


def write_wav(path, data, peak=.7):
    if not data or not any(abs(v) > .000001 for v in data):
        raise ValueError('Empty audio: ' + str(path))
    dc = sum(data) / len(data)
    data = [v-dc for v in data]
    fade = min(round(.012 * SR), len(data)//4)
    for i in range(fade):
        gain = .5-.5*math.cos(math.pi*i/fade)
        data[i] *= gain
        data[-i-1] *= gain
    scale = peak / max(abs(v) for v in data)
    pcm = array.array('h', (round(max(-.999,min(.999,v*scale))*32767) for v in data))
    with wave.open(str(path),'wb') as target:
        target.setnchannels(1);target.setsampwidth(2);target.setframerate(SR);target.writeframes(pcm.tobytes())
    return round(len(data)/SR,4)


def read_wav(path):
    with wave.open(str(path),'rb') as source:
        if (source.getnchannels(),source.getsampwidth(),source.getframerate()) != (1,2,SR):
            raise ValueError('Expected mono PCM16/44100: ' + str(path))
        pcm=array.array('h');pcm.frombytes(source.readframes(source.getnframes()))
    return [v/32768 for v in pcm]


def package_speech():
    voice = json.loads((RAW/'voice.json').read_text(encoding='utf-8-sig')) if (RAW/'voice.json').exists() else {}
    for key,_,_ in PHRASES:
        source=RAW/(key+'.wav')
        if not source.exists():
            continue
        intermediate=RAW/(key+'-44100.wav')
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(source),
                        '-af','aresample=44100:resampler=soxr:precision=24,highpass=f=55,equalizer=f=185:t=q:w=0.9:g=2.4,equalizer=f=3100:t=q:w=0.8:g=-1.2,lowpass=f=10000',
                        '-ac','1','-c:a','pcm_s16le',str(intermediate)],check=True)
        data=read_wav(intermediate)
        audible=[i for i,v in enumerate(data) if abs(v)>.0025]
        if not audible:
            raise ValueError('Silent speech: '+key)
        data=data[max(0,audible[0]-round(.075*SR)):min(len(data),audible[-1]+round(.16*SR))]
        write_wav(OUT/(key+'.wav'),silence(.1)+data+silence(.14),.7)
    return voice


def main():
    sounds=[]
    for key,data in clips.items():
        duration=write_wav(OUT/(key+'.wav'),data,peaks[key])
        sounds.append(dict(id=key,label=labels[key],file=key+'.wav',path='audio/'+key+'.wav',
                           duration=duration,durationSeconds=duration,loop=False,kind='sfx',
                           source='original-local-santa-bell-sleigh-snow-cloth-synthesis',gain=.65,description=descriptions[key]))
    voice=package_speech()
    phrases=[]
    for key,event,text in PHRASES:
        file=OUT/(key+'.wav');exists=file.exists()
        duration=round(len(read_wav(file))/SR,4) if exists else 0
        phrases.append(dict(id=key,event=event,text=text,language='da-DK',file=key+'.wav' if exists else None,
                            path='audio/'+key+'.wav' if exists else None,available=exists,duration=duration,durationSeconds=duration,
                            source='local-Windows-OneCore-da-DK-warm-Santa-character-processing' if exists else None))
    ready=all(x['available'] for x in phrases)
    manifest=dict(schemaVersion=1,avatarId='santa',name='Julemanden',sampleRate=SR,channels=1,format='PCM16 WAV',
                  sfx=sounds,sounds={x['id']:x for x in sounds},phrases=phrases,
                  speech=dict(available=ready,language='da-DK',voice=voice.get('voice'),voiceGender=voice.get('gender'),
                              installedDanishVoices=voice.get('installedDanishVoices'),speakingRate=voice.get('speakingRate'),
                              audioPitch=voice.get('audioPitch'),status='ready' if ready else 'local-speech-not-yet-rendered',
                              processing='Local speech pitch/rate control, warm EQ, high-quality resampling. No cloning, API or robot vocoder.'),
                  playback=dict(defaultMuted=True,maxConcurrentSfx=3,phraseCooldownSeconds=18,stopPreviousPhrase=True,
                                idleSoundsAutomatic=False,footsteps='one clip per foot-contact event'),
                  license=dict(proceduralSfx='Original newly authored project assets; no sampled recordings or existing song melodies.',
                               speech='Synthesized locally with an installed Danish Windows OneCore voice. No API/network call/voice cloning.'))
    (OUT/'audio-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (OUT/'phrases.json').write_text(json.dumps(phrases,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    full,cues=silence(.2),[]
    for key in clips:
        cues.append(dict(id=key,label=labels[key],startSeconds=round(len(full)/SR,3)))
        full+=read_wav(OUT/(key+'.wav'))+silence(.3)
    write_wav(OUT/'all-sfx-preview.wav',full,.76)
    (OUT/'all-sfx-preview-cues.json').write_text(json.dumps(cues,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    demo,cues=silence(.2),[]
    for key in ['wave','walk','run','jump','land','ability_1_pulse','ability_2_magnet','ability_3_shield','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release','celebrate']:
        cues.append(dict(id=key,startSeconds=round(len(demo)/SR,3)))
        demo+=read_wav(OUT/(key+'.wav'))+silence(.32)
    for item in phrases:
        if item['available']:
            cues.append(dict(id=item['id'],startSeconds=round(len(demo)/SR,3),text=item['text']))
            demo+=read_wav(OUT/item['file'])+silence(.45)
    write_wav(OUT/'audio-demo.wav',demo,.76)
    (OUT/'audio-demo-cues.json').write_text(json.dumps(cues,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(dict(sfx=len(sounds),spoken=sum(x['available'] for x in phrases),demoSeconds=round(len(demo)/SR,2)),ensure_ascii=False))


if __name__=='__main__':
    main()
