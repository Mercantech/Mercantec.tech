"""Original LUPP clay, elastic and idea-ping audio. No sampled recordings or API.

PCM export/local speech packaging architecture is shared; all28 action scores
below are authored for LUPP rather than copied or pitch-shifted Santa audio.
"""
from pathlib import Path
import array
import json
import math
import random
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'audio'
RAW = Path(__file__).resolve().parent / 'raw'
SR = 44100
TAU = math.tau
RNG = random.Random(20261110)
OUT.mkdir(parents=True, exist_ok=True)
PHRASES = [
    ('phrase_1_welcome', 'welcome', 'Jeg er LUPP. Hvad skal spilleren prøve først?'),
    ('phrase_2_start', 'start', 'En lille prototype kan gøre en stor idé tydelig.'),
    ('phrase_3_success', 'success', 'Prøv det af, lyt til feedback, og ret til.'),
    ('phrase_4_retry', 'retry', 'En fejl er en invitation til et nyt forsøg.'),
    ('phrase_5_milestone', 'milestone', 'Dit spil er ude i verden. Lad os se, hvad spillerne gør!'),
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

def air(duration, gain=.3, smooth=.05, decay=1, swell=False):
    values, low = [], 0
    for i in range(round(duration * SR)):
        t = i / SR
        low += smooth * (RNG.uniform(-1, 1) - low)
        shape = math.sin(math.pi * t / duration) ** 1.4 if swell else math.exp(-decay * t / duration)
        values.append(low * gain * shape * edge(t, duration, .008, min(.075, duration*.3)))
    return values

def elastic(duration, start, end=None, gain=.3, decay=4, wobble=0):
    end = start if end is None else end
    phase, values = 0, []
    for i in range(round(duration * SR)):
        t = i / SR
        frequency = start * (end / start) ** (t / duration)
        frequency *= 1 + wobble * math.sin(TAU * 8 * t) * math.exp(-2 * t / duration)
        phase += TAU * frequency / SR
        body = math.sin(phase) + .2*math.sin(2*phase) + .06*math.sin(3*phase)
        values.append(body * gain * math.exp(-decay*t/duration) * edge(t, duration, .002, min(.05,duration*.2)))
    return values

def clay(duration=.25, pitch=100, gain=.3):
    a = elastic(duration, pitch*1.65, pitch*.55, gain, 5, .035)
    add(a, air(duration*.82, gain*1.6, .14, 4), .003)
    add(a, elastic(duration*.35, pitch*2.3, pitch, gain*.16, 7), .018)
    return a

def mallet(duration, frequency, gain=.14):
    return [sum(math.sin(TAU*frequency*ratio*(i/SR))*level*math.exp(-speed*i/SR/duration)
                for ratio,level,speed in [(1,1,3.3),(2,.16,5.4),(3,.045,7.0)])
            *gain*edge(i/SR,duration,.004,min(.09,duration*.3))
            for i in range(round(duration*SR))]

def melody(frequencies, spacing=.16, duration=.4, total=None, gain=.14):
    a=silence(total or (len(frequencies)-1)*spacing+duration+.03)
    for i,f in enumerate(frequencies): add(a, mallet(duration,f,gain), i*spacing)
    return a

def pad(duration, start, end=None, gain=.1):
    end = start if end is None else end
    phase,a=0,[]
    for i in range(round(duration*SR)):
        t=i/SR;phase+=TAU*start*(end/start)**(t/duration)/SR
        a.append((math.sin(phase)+.14*math.sin(phase*2))*gain*edge(t,duration,.08,min(.3,duration*.3)))
    return a

clips, labels, peaks, descriptions = {}, {}, {}, {}
def clip(key,label,samples,peak,description):
    clips[key],labels[key],peaks[key],descriptions[key]=samples,label,peak,description

a=silence(.72);add(a,air(.66,.16,.018,.2,True));add(a,pad(.58,82,78,.025),.08)
clip('idle','Leret falder til ro',a,.13,'En enkelt diskret udånding og blød lerresonans; aldrig automatisk gentagelse.')
a=silence(.12);add(a,clay(.095,155,.13));add(a,air(.07,.11,.12,4),.02)
clip('blink','Et lille lerblink',a,.12,'Et meget lille blødt plip, uden klokke eller robotbip.')
a=silence(.64);add(a,air(.5,.27,.07,.3,True));add(a,clay(.18,146,.13),.07);add(a,elastic(.2,220,174,.08,5),.31)
clip('wave','Et venligt vink',a,.4,'To små lerplop og håndbevægelsens korte luftstrøg.')
a=melody([392,466.16,415.3],.25,.39,1.0,.11);add(a,elastic(.38,125,180,.035,2,.05),.42)
clip('think','En idé tager form',a,.33,'Tre bløde, spørgende filtmallet-toner med en lille elastisk undertone.')
a=silence(.88)
for at,f,d in [(0,210,.19),(.27,270,.16),(.51,235,.23)]:add(a,elastic(d,f,f*.82,.12,3,.08),at)
add(a,air(.58,.1,.035,.3,True),.12)
clip('talk','LUPP forklarer',a,.31,'Tre afrundede, levende lerresonanser. Den egentlige tale ligger i fem separate filer.')
a=silence(.34);add(a,clay(.17,170,.15));add(a,mallet(.23,880,.1),.08)
clip('point','Se her',a,.38,'En kort taktil pegeaccent og en lys, blød idémarkering.')
a=melody([329.63,440,554.37,659.26],.15,.62,1.23,.14)
for at,p in [(.03,92),(.25,115),(.57,150)]:add(a,clay(.24,p,.08),at)
clip('celebrate','Det lykkedes',a,.63,'Et originalt stigende spilmotiv og tre glade, hoppende lerplop; uden klokker eller samples.')
a=melody([261.63,349.23,440],.23,.55,1.08,.12);add(a,pad(.75,130.81,146.83,.027),.15)
clip('encourage','Prøv igen sammen',a,.43,'En varm, rolig opmuntring i tre bløde toner med kort elastisk afrunding.')
a=silence(.48);add(a,elastic(.32,180,430,.2,1.8,.09));add(a,air(.3,.35,.04,.2,True),.04)
clip('surprised','Hov, en ny mulighed',a,.42,'En kort, opadgående ler-svup med luft og et mildt overrasket præg.')
a=melody([261.63,329.63,392,523.25],.2,.83,1.55,.15);add(a,elastic(.52,110,220,.06,3,.025),.84)
clip('unlock','LUPP er låst op',a,.67,'Et originalt lyst belønningsmotiv med en blød, fjederagtig afslutning.')
clip('walk','Et blødt lertrin',clay(.235,92,.36),.55,'Én rund og let klæbrig fodkontakt; intet sne-, læder- eller metaltrin.')
a=clay(.16,113,.4);add(a,air(.095,.25,.2,4),.012)
clip('run','Et hurtigt lertrin',a,.6,'Én kort og fastere blød fodkontakt med lille elastisk tilbagespring.')
a=silence(.44);add(a,clay(.14,112,.16));add(a,elastic(.36,155,410,.22,1.4,.09),.04);add(a,air(.3,.35,.05,.1,True),.08)
clip('jump','Et elastisk afsæt',a,.54,'Lerets afsæt bøjer tonehøjden op som en lille fjeder.')
a=silence(.4);add(a,clay(.35,69,.48));add(a,elastic(.22,132,73,.09,5,.035),.06)
clip('land','Blød lerlanding',a,.69,'Et polstret lavt plop og et kort eftergivende tilbagespring.')
a=silence(.51);add(a,elastic(.41,121,63,.2,1.5,.055));add(a,air(.36,.25,.06,.3,True),.09)
clip('sit','LUPP sætter sig',a,.35,'Langsom blød kompression uden lyd af en bestemt stol eller genstand.')
a=silence(1.48);add(a,air(1.35,.2,.015,.1,True));add(a,pad(1.18,68,64,.024),.14)
clip('sleep','En rolig pause',a,.23,'En enkelt mild vejrtrækning med rolig lerresonans; ikke en automatisk lydsløjfe.')
a=silence(.35);add(a,clay(.18,137,.13));add(a,elastic(.22,290,235,.07,4),.1)
clip('pickup','Et forsigtigt lergreb',a,.35,'Generisk håndgreb og lille elastisk slip. Genstandens egen lyd vælges af spillet.')
a=silence(.37);add(a,clay(.12,186,.12));add(a,mallet(.24,622.25,.09),.1)
clip('interact','Afprøv en handling',a,.36,'To taktile prototypeklik med blød afrunding, uden en bestemt knap eller enhed.')
a=silence(.29);add(a,clay(.23,107,.4));add(a,air(.19,.34,.12,3),.015)
clip('hit','Et blødt sammenstød',a,.65,'Et kort polstret lerplop uden skrig, splinter eller hårde metalliske transienter.')
a=silence(1.14);add(a,melody([440,311.13,207.65],.24,.38,1.05,.09));add(a,elastic(.82,150,61,.12,2,.09),.13);add(a,clay(.24,55,.16),.83)
clip('knockout','LUPP tager en pause',a,.54,'Et faldende, fjederagtigt spilmotiv og en blød komisk landing.')
a=melody([246.94,329.63,493.88],.23,.7,1.2,.14);add(a,elastic(.83,74,151,.08,1.4,.05),.16)
clip('respawn','Klar til et nyt forsøg',a,.6,'Tre stigende filttoner og lerets rolige tilbagespring til en ny begyndelse.')
a=silence(.68);add(a,elastic(.42,275,825,.21,2.4,.05));add(a,mallet(.41,1174.66,.1),.13);add(a,air(.45,.38,.06,.3,True),.07)
clip('ability_1_pulse','Idépuls',a,.72,'En klar, elastisk idégnist og et kort blødt luftsus. Projektilet er et separat spilelement.')
a=silence(1.03);add(a,elastic(.83,610,185,.2,1.1,.13));add(a,air(.82,.48,.035,.1,True),.07)
for at,p in [(.15,140),(.43,118),(.72,92)]:add(a,clay(.21,p,.09),at)
clip('ability_2_magnet','Feedbackgreb',a,.66,'Et elastisk indadgående træk med tre tydelige, bløde grebspunkter.')
a=silence(1.25);add(a,pad(1.12,174.61,gain=.09));add(a,elastic(.67,350,260,.11,2,.03),.07)
for f,at in [(349.23,.03),(440,.17),(523.25,.29)]:add(a,mallet(.88,f,.1),at)
clip('ability_3_shield','Prototypeværn',a,.65,'En hul, rund beskyttelsesklang, der samler sig som en blød prototypeboble.')
a=silence(.7);add(a,air(.61,.78,.075,.2,True));add(a,elastic(.44,185,590,.16,1.7,.07),.03)
for at,p in [(0,75),(.14,118),(.31,170)]:add(a,clay(.17,p,.13),at)
clip('ability_4_dash','Iterationsryk',a,.74,'Tre hurtige forbedringsryk og et elastisk luftstrøg uden sledeklokker eller robotmotor.')
a=melody([293.66,369.99,493.88,587.33],.27,.65,1.55,.13);add(a,pad(1.18,98,146.83,.04),.18)
clip('ability_5_repair','Playtestpause',a,.61,'Et roligt, varmt forløb af bløde filttoner til en kort helende pause.')
a=silence(2.15);add(a,pad(1.99,65.41,130.81,.09));add(a,air(1.8,.25,.023,.1,True),.17)
for i in range(9):add(a,elastic(.34,196*(1.13**i),294*(1.09**i),.055+i*.006,2,.04),.11+i*.21)
clip('ultimate_charge','Game Jam · opladning',a,.71,'En voksende serie af elastiske prototypepulser, som samler idéer til en Game Jam.')
a=silence(2.52);add(a,clay(.45,48,.35));add(a,elastic(1.12,168,92,.16,2.4,.06),.02);add(a,air(1.46,.58,.05,1.5),.03)
for i,f in enumerate([261.63,329.63,392,523.25,659.26,783.99]):add(a,mallet(1.54,f,.13),.07+i*.11)
for at,p in [(.29,130),(.59,170),(.92,220)]:add(a,elastic(.4,p*1.4,p,.08,4,.08),at)
clip('ultimate_release','Game Jam · udløsning',a,.76,'En stor, venlig ler-pong med originale idétoner og tre prototype-efterklange; ingen kendt melodi eller samples.')

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
                        '-af','aresample=44100:resampler=soxr:precision=24,highpass=f=55,equalizer=f=260:t=q:w=0.9:g=1.0,equalizer=f=3100:t=q:w=0.8:g=0.7,lowpass=f=10000',
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
                           source='original-local-lupp-clay-plop-spring-mallet-synthesis',gain=.65,description=descriptions[key]))
    voice=package_speech()
    phrases=[]
    for key,event,text in PHRASES:
        file=OUT/(key+'.wav');exists=file.exists()
        duration=round(len(read_wav(file))/SR,4) if exists else 0
        phrases.append(dict(id=key,event=event,text=text,language='da-DK',file=key+'.wav' if exists else None,
                            path='audio/'+key+'.wav' if exists else None,available=exists,duration=duration,durationSeconds=duration,
                            source='local-Windows-OneCore-da-DK-lively-LUPP-character-processing' if exists else None))
    ready=all(x['available'] for x in phrases)
    manifest=dict(schemaVersion=1,avatarId='lupp',name='LUPP',sampleRate=SR,channels=1,format='PCM16 WAV',
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
