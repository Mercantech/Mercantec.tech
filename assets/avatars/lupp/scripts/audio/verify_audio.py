"""Check actual PCM data, source identity and manifest completeness."""
from pathlib import Path
import array
import hashlib
import json
import math
import wave

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'audio'
manifest=json.loads((OUT/'audio-manifest.json').read_text(encoding='utf8'))
expected=['idle','blink','wave','think','talk','point','celebrate','encourage','surprised','unlock','walk','run','jump','land','sit','sleep','pickup','interact','hit','knockout','respawn','ability_1_pulse','ability_2_magnet','ability_3_shield','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release']
assert manifest['avatarId']=='lupp'
assert [x['id'] for x in manifest['sfx']]==expected
assert len(manifest['phrases'])==5 and manifest['speech']['available']
items,errors,hashes=[],[],set()
reference_names=['robot-avatar-v3','raymond-avatar-v2','santa-avatar-v1']
reference_hashes={hashlib.sha256(p.read_bytes()).hexdigest() for name in reference_names for p in (ROOT.parent/name/'audio').glob('*.wav')}
for item in manifest['sfx']+manifest['phrases']:
    file=OUT/item['file']
    with wave.open(str(file),'rb') as stream:
        channels,width,rate,frames=stream.getnchannels(),stream.getsampwidth(),stream.getframerate(),stream.getnframes()
        pcm=array.array('h');pcm.frombytes(stream.readframes(frames))
    samples=[v/32768 for v in pcm]
    peak=max(abs(v) for v in samples)
    rms=math.sqrt(sum(v*v for v in samples)/len(samples))
    dc=sum(samples)/len(samples)
    duration=frames/rate
    digest=hashlib.sha256(file.read_bytes()).hexdigest()
    audible=[i for i,v in enumerate(samples) if abs(v)>.003]
    result=dict(id=item['id'],file=item['file'],sampleRate=rate,channels=channels,sampleWidthBytes=width,
                durationSeconds=round(duration,4),peak=round(peak,6),peakDbfs=round(20*math.log10(peak),2),
                rms=round(rms,6),rmsDbfs=round(20*math.log10(rms),2),dcOffset=round(dc,7),
                silenceRatio=round(sum(abs(v)<.0002 for v in samples)/len(samples),4),
                firstSample=pcm[0],lastSample=pcm[-1],sha256=digest)
    items.append(result)
    if (channels,width,rate)!=(1,2,44100):errors.append(item['id']+': format')
    if not audible or rms<.003:errors.append(item['id']+': silent/inaudible')
    if not .05<=duration<=9:errors.append(item['id']+': duration')
    if peak>.77 or abs(dc)>.002:errors.append(item['id']+': clipping/DC')
    if pcm[0] or pcm[-1]:errors.append(item['id']+': hard edge')
    if abs(duration-item['durationSeconds'])>.001:errors.append(item['id']+': manifest duration')
    if digest in hashes:errors.append(item['id']+': duplicate waveform')
    if digest in reference_hashes:errors.append(item['id']+': copied previous-avatar waveform')
    hashes.add(digest)
    if item in manifest['phrases'] and not 1<duration<9:errors.append(item['id']+': invalid speech length')
report=dict(passed=not errors,files=len(items),sfxCount=len(manifest['sfx']),spokenCount=len(manifest['phrases']),
            sampleRate=44100,channels=1,format='PCM16 WAV',maxPeak=max(x['peak'] for x in items),
            maxAbsDc=max(abs(x['dcOffset']) for x in items),allWaveformsDistinct=len(hashes)==len(items),
            noCopiedAvatarWaveforms=not any(x['sha256'] in reference_hashes for x in items),
            errors=errors,items=items,scope='Objective PCM/manifest validation only. Tools do not supply subjective listening or speech recognition.')
(OUT/'audio-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k!='items'},ensure_ascii=False,indent=2))
if errors:raise SystemExit(1)
